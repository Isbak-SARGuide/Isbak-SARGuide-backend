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
using SkiaSharp;

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
        // tip SADECE baytlardan (magic byte) belirlenir. SkiaSharp'a asla
        // dogrulanmamis baytlar verilmez - imza tespiti ONCE gecmeli.
        var signature = ImageSignatureDetector.Detect(bytes);
        if (signature is null)
        {
            return Result.Failure<MediaDto>(Error.Validation(
                "Media.UnsupportedFormat",
                "Desteklenmeyen veya bozuk dosya formatı. Desteklenen: PNG, JPEG, GIF, WEBP."));
        }

        // Faz 12.7 (WebP + thumbnail, mobil optimizasyon): imza dogru olsa
        // bile baytlar bozuk/eksik olabilir. SKBitmap.Decode boyle durumda
        // BEKLENENIN AKSINE null degil ArgumentNullException firlatir (kodek
        // olusturulamadiginda SkiaSharp'in kendi ic null-kontrolsuz cagrisi) -
        // dar bir try/catch bunu da PublishingService.FinalizeAsync'teki "dar
        // try" ilkesiyle ayni sekilde temiz bir Validation hatasina cevirir,
        // saldirgan kontrollu (gecerli magic byte + bozuk govde) bir dosya
        // 500'e degil 400'e dusmeli.
        SKBitmap? originalBitmap;
        try
        {
            originalBitmap = SKBitmap.Decode(bytes);
        }
        catch (ArgumentNullException)
        {
            originalBitmap = null;
        }

        using var _ = originalBitmap;
        if (originalBitmap is null)
        {
            return Result.Failure<MediaDto>(Error.Validation(
                "Media.UnsupportedFormat",
                "Desteklenmeyen veya bozuk dosya formatı. Desteklenen: PNG, JPEG, GIF, WEBP."));
        }

        // STORAGE'A YAZILAN asil dosya: GIF DISINDA her format WebP'ye cevrilir
        // (Faz 12.7, mobil optimizasyon). GIF ISTISNA: SkiaSharp'in SKBitmap.Decode'u
        // animasyonlu bir GIF'in SADECE ILK KARESINI statik bitmap olarak okur -
        // WebP'ye cevirmek yuklenen dosya gercekten animasyonlu olsa bile
        // animasyonu backend'de kalici olarak yok ederdi (bug bulgusu - kullanici
        // "GIF eklerken calismiyor" diye bildirdi). GIF bu yuzden ORIJINAL
        // baytlariyla saklanir, checksum de o baytlardan hesaplanir.
        var isGif = signature.Value.ContentType == "image/gif";
        var webPQuality = storageOptions.Value.WebPQuality;
        var (storedBytes, storedContentType, storedExtension) = isGif
            ? (bytes, "image/gif", ".gif")
            : (EncodeWebP(originalBitmap, webPQuality), "image/webp", ".webp");
        var checksum = Convert.ToHexString(SHA256.HashData(storedBytes));

        var existing = await unitOfWork.Media.FindByChecksumAsync(checksum, cancellationToken);
        if (existing is not null)
        {
            // Dedup: ayni icerik zaten var, ikinci bir dosya/satir uretmeye gerek yok.
            return Result.Success(existing.Adapt<MediaDto>());
        }

        // Kucuk onizleme HER ZAMAN statik WebP - GIF icin bile: thumbnail zaten
        // tek kare gostermeyi amacliyor, animasyon burada beklenmiyor/gerekmiyor.
        var thumbnailBytes = EncodeThumbnail(originalBitmap, storageOptions.Value.ThumbnailMaxDimension, webPQuality);

        // relativePath hicbir zaman declaredFileName'den turemez - path
        // traversal'a karsi birincil savunma budur (LocalFileStorageService'teki
        // kok-disi kontrolu ikincildir). Ana dosya ve thumbnail AYNI guid'i
        // paylasir - kolayca eslestirilebilsinler diye.
        var now = DateTime.UtcNow;
        var guid = Guid.NewGuid().ToString("N");
        var relativePath = $"media/{now:yyyy}/{now:MM}/{guid}{storedExtension}";
        var thumbnailRelativePath = $"media/{now:yyyy}/{now:MM}/{guid}-thumb.webp";

        var media = new Entities.Content.Media
        {
            FileName = SanitizeFileName(declaredFileName),
            StoragePath = relativePath,
            ThumbnailStoragePath = thumbnailRelativePath,
            MediaType = MediaType.Image,
            ContentType = storedContentType,
            FileSize = storedBytes.LongLength,
            Checksum = checksum,
            Width = originalBitmap.Width,
            Height = originalBitmap.Height,
        };

        using (var mainStream = new MemoryStream(storedBytes))
        {
            await storageService.SaveAsync(mainStream, relativePath, cancellationToken);
        }

        using (var thumbnailStream = new MemoryStream(thumbnailBytes))
        {
            await storageService.SaveAsync(thumbnailStream, thumbnailRelativePath, cancellationToken);
        }

        try
        {
            await unitOfWork.Media.AddAsync(media, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex))
        {
            // Eszamanli ayni-dosya yuklemesi yarisi: baska bir istek bizden once
            // ayni checksum'i yazdi. Kendi kopyalarimizi (ana + thumbnail)
            // temizleyip kazanani donuyoruz - gercek dedup, yarista bile.
            logger.LogInformation(ex, "Checksum {Checksum} icin eszamanli yukleme yarisi - kazanan satir kullaniliyor.", checksum);
            await storageService.DeleteAsync(relativePath, cancellationToken);
            await storageService.DeleteAsync(thumbnailRelativePath, cancellationToken);

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
            // Unique-ihlali DISI herhangi bir DB hatasi: dosyalar diske yazildi
            // ama satir hic olusmadi. Temizlemezsek CleanupOrphansAsync bunu
            // asla bulamaz - o sadece VAR OLAN Media satirlarini tarar (Faz 8
            // mimari incelemesinde bulundu). Orijinal exception korunur, sadece
            // yeniden firlatilir - global handler yakalar.
            await storageService.DeleteAsync(relativePath, cancellationToken);
            await storageService.DeleteAsync(thumbnailRelativePath, cancellationToken);
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

        var thumbnailPath = media.ThumbnailStoragePath;

        unitOfWork.Media.Remove(media);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await storageService.DeleteAsync(media.StoragePath, cancellationToken);
        if (thumbnailPath is not null)
        {
            await storageService.DeleteAsync(thumbnailPath, cancellationToken);
        }

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
            if (orphan.ThumbnailStoragePath is not null)
            {
                await storageService.DeleteAsync(orphan.ThumbnailStoragePath, cancellationToken);
            }
        }

        return Result.Success(orphans.Count);
    }

    private static byte[] EncodeWebP(SKBitmap bitmap, int quality)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Webp, quality);
        return data.ToArray();
    }

    private static byte[] EncodeThumbnail(SKBitmap original, int maxDimension, int quality)
    {
        var (width, height) = ScaleToFit(original.Width, original.Height, maxDimension);

        using var resized = original.Resize(new SKImageInfo(width, height), SKSamplingOptions.Default);

        // Resize teorik olarak null donebilir (kaynak yetersizligi vb.) -
        // boyle bir durumda thumbnail'i tam boyuttan uretmek (buyuk ama
        // yine de gecerli bir WebP dosyasi) sessizce dosyasiz kalmaktan iyidir.
        return EncodeWebP(resized ?? original, quality);
    }

    private static (int Width, int Height) ScaleToFit(int width, int height, int maxDimension)
    {
        if (width <= maxDimension && height <= maxDimension)
        {
            // Zaten kucuk - thumbnail buyutme yapmaz, oldugu gibi kalir.
            return (width, height);
        }

        var scale = (double)maxDimension / Math.Max(width, height);
        return (Math.Max(1, (int)Math.Round(width * scale)), Math.Max(1, (int)Math.Round(height * scale)));
    }

    private static string SanitizeFileName(string declaredFileName)
    {
        // Path.GetFileName herhangi bir dizin bileseni ("../../x.png" gibi)
        // atar - FileName sadece goruntuleme metadata'si olsa da savunma ucuz.
        var name = Path.GetFileName(declaredFileName);
        return name.Length > 260 ? name[..260] : name;
    }
}
