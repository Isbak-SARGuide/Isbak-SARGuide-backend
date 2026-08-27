using System.Security.Cryptography;
using Isbak_SAR_Guide.Business.Common;
using Isbak_SAR_Guide.Business.DTOs.Media;
using Isbak_SAR_Guide.Business.Services.Abstract;
using Isbak_SAR_Guide.DataAccess.Common;
using Isbak_SAR_Guide.DataAccess.Repositories.Abstract;
using Isbak_SAR_Guide.Entities.Content.Enums;
using Mapster;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Isbak_SAR_Guide.Business.Services.Concrete;

public class MediaService(
    IUnitOfWork unitOfWork,
    IStorageService storageService,
    IOptions<StorageOptions> storageOptions,
    ILogger<MediaService> logger) : IMediaService
{
    public async Task<Result<MediaDto>> UploadAsync(
        Stream content, string declaredFileName, long declaredLength, CancellationToken cancellationToken = default)
    {
        var maxSize = storageOptions.Value.MaxFileSizeBytes;

        if (declaredLength <= 0)
        {
            return Result.Failure<MediaDto>(Error.Validation("Media.Empty", "Dosya boş olamaz."));
        }

        if (declaredLength > maxSize)
        {
            return Result.Failure<MediaDto>(Error.Validation("Media.TooLarge", $"Dosya boyutu {maxSize} bayt sınırını aşıyor."));
        }

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();

        // Beyan edilen Content-Length'e guvenilmez - gercek boyut, okunan
        // baytlardan yeniden dogrulanir (istemci yalan soyleyebilir).
        if (bytes.LongLength == 0)
        {
            return Result.Failure<MediaDto>(Error.Validation("Media.Empty", "Dosya boş olamaz."));
        }

        if (bytes.LongLength > maxSize)
        {
            return Result.Failure<MediaDto>(Error.Validation("Media.TooLarge", $"Dosya boyutu {maxSize} bayt sınırını aşıyor."));
        }

        // Uzantiya veya istemcinin bildirdigi Content-Type'a hic bakilmaz -
        // tip SADECE baytlardan (magic byte) belirlenir.
        var signature = ImageSignatureDetector.Detect(bytes);
        if (signature is null)
        {
            return Result.Failure<MediaDto>(Error.Validation(
                "Media.UnsupportedFormat",
                "Desteklenmeyen veya bozuk dosya formatı. Desteklenen: PNG, JPEG, GIF, WEBP."));
        }

        var checksum = Convert.ToHexString(SHA256.HashData(bytes));

        var existing = await unitOfWork.Media.FindByChecksumAsync(checksum, cancellationToken);
        if (existing is not null)
        {
            // Dedup: ayni icerik zaten var, ikinci bir dosya/satir uretmeye gerek yok.
            return Result.Success(existing.Adapt<MediaDto>());
        }

        var (width, height) = ReadDimensions(signature.Value.ContentType, bytes);

        // relativePath hicbir zaman declaredFileName'den turemez - path
        // traversal'a karsi birincil savunma budur (LocalFileStorageService'teki
        // kok-disi kontrolu ikincildir).
        var now = DateTime.UtcNow;
        var relativePath = $"media/{now:yyyy}/{now:MM}/{Guid.NewGuid():N}{signature.Value.Extension}";

        var media = new Entities.Content.Media
        {
            FileName = SanitizeFileName(declaredFileName),
            StoragePath = relativePath,
            MediaType = MediaType.Image,
            ContentType = signature.Value.ContentType,
            FileSize = bytes.LongLength,
            Checksum = checksum,
            Width = width,
            Height = height,
        };

        buffer.Position = 0;
        await storageService.SaveAsync(buffer, relativePath, cancellationToken);

        try
        {
            await unitOfWork.Media.AddAsync(media, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex))
        {
            // Eszamanli ayni-dosya yuklemesi yarisi: baska bir istek bizden once
            // ayni checksum'i yazdi. Kendi kopyamizi temizleyip kazanani donuyoruz
            // - gercek dedup, yarista bile.
            logger.LogInformation(ex, "Checksum {Checksum} icin eszamanli yukleme yarisi - kazanan satir kullaniliyor.", checksum);
            await storageService.DeleteAsync(relativePath, cancellationToken);

            var winner = await unitOfWork.Media.FindByChecksumAsync(checksum, cancellationToken);
            if (winner is null)
            {
                // Dar pencere: kazanan satir bu iki sorgu arasinda soft-delete'lendi.
                // winner! ile devam etmek NullReferenceException'a, orijinal
                // DbUpdateException'i gizleyerek dusmek olurdu - acikca reddet.
                return Result.Failure<MediaDto>(Error.Unexpected(
                    "Media.ConcurrentUploadUnresolved", "Eşzamanlı yükleme çözümlenemedi, lütfen tekrar deneyin."));
            }

            return Result.Success(winner.Adapt<MediaDto>());
        }
        catch
        {
            // Unique-ihlali DISI herhangi bir DB hatasi: dosya diske yazildi ama
            // satir hic olusmadi. Temizlemezsek CleanupOrphansAsync bunu asla
            // bulamaz - o sadece VAR OLAN Media satirlarini tarar (Faz 8 mimari
            // incelemesinde bulundu). Orijinal exception korunur, sadece yeniden
            // firlatilir - global handler yakalar.
            await storageService.DeleteAsync(relativePath, cancellationToken);
            throw;
        }

        return Result.Success(media.Adapt<MediaDto>());
    }

    public async Task<Result<MediaDto>> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var media = await unitOfWork.Media.FindByIdAsync(id, cancellationToken);
        if (media is null)
        {
            return Result.Failure<MediaDto>(Error.NotFound("Media.NotFound", $"Id={id} olan medya bulunamadı."));
        }

        return Result.Success(media.Adapt<MediaDto>());
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var media = await unitOfWork.Media.FindByIdAsync(id, cancellationToken);
        if (media is null)
        {
            return Result.Failure(Error.NotFound("Media.NotFound", $"Id={id} olan medya bulunamadı."));
        }

        // Soft-delete interceptor'i FK'daki SetNull cascade'ini tetiklemez
        // (gercek DELETE degil) - hala kullanimdaysa acikca reddet, aksi halde
        // bir ContentBlock gorunmez bir Media'ya isaret eder kalirdi.
        var inUse = await unitOfWork.ContentBlocks.AnyWithMediaIdAsync(id, cancellationToken);
        if (inUse)
        {
            return Result.Failure(Error.Conflict(
                "Media.InUse",
                "Bu medya en az bir içerik bloğu tarafından kullanılıyor, önce o blokları güncelleyin veya silin."));
        }

        unitOfWork.Media.Remove(media);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await storageService.DeleteAsync(media.StoragePath, cancellationToken);

        return Result.Success();
    }

    public async Task<Result<int>> CleanupOrphansAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.AddHours(-storageOptions.Value.OrphanGraceHours);
        var orphans = await unitOfWork.Media.FindOrphansAsync(cutoff, cancellationToken);

        foreach (var orphan in orphans)
        {
            unitOfWork.Media.Remove(orphan);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        foreach (var orphan in orphans)
        {
            await storageService.DeleteAsync(orphan.StoragePath, cancellationToken);
        }

        return Result.Success(orphans.Count);
    }

    private static (int? Width, int? Height) ReadDimensions(string contentType, byte[] bytes)
    {
        var dimensions = contentType switch
        {
            "image/png" => ImageSignatureDetector.TryReadPngDimensions(bytes),
            "image/jpeg" => ImageSignatureDetector.TryReadJpegDimensions(bytes),
            "image/gif" => ImageSignatureDetector.TryReadGifDimensions(bytes),
            _ => null, // webp: boyut ayristirma MVP kapsami disi, alan nullable
        };

        return dimensions is { } d ? (d.Width, d.Height) : (null, null);
    }

    private static string SanitizeFileName(string declaredFileName)
    {
        // Path.GetFileName herhangi bir dizin bileseni ("../../x.png" gibi)
        // atar - FileName sadece goruntuleme metadata'si olsa da savunma ucuz.
        var name = Path.GetFileName(declaredFileName);
        return name.Length > 260 ? name[..260] : name;
    }
}
