using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Isbak_SAR_Guide.Business.Common;
using Isbak_SAR_Guide.Business.DTOs.Sync;
using Isbak_SAR_Guide.Business.Services.Abstract;
using Isbak_SAR_Guide.DataAccess.Repositories.Abstract;
using Isbak_SAR_Guide.Entities.Content;

namespace Isbak_SAR_Guide.Business.Services.Concrete;

/// <summary>
/// STUB (5.0): draft veriden calisir, gercek yayin/versiyon sistemi (Faz 3/4)
/// gelmeden mobil gelistiricinin sozlesme uzerinde ilerleyebilmesi icin.
/// GetChangesAsync bu yuzden her zaman "degisiklik yok" doner.
/// </summary>
public class SyncService(IUnitOfWork unitOfWork) : ISyncService
{
    public async Task<Result<SyncSnapshotDto>> GetSnapshotAsync(int bookId, CancellationToken cancellationToken = default)
    {
        var book = await unitOfWork.Books.GetWithFullTreeAsync(bookId, cancellationToken);

        if (book is null)
        {
            return Result.Failure<SyncSnapshotDto>(
                Error.NotFound("Sync.BookNotFound", $"Id={bookId} olan kitap bulunamadı."));
        }

        return Result.Success(BuildSnapshot(book));
    }

    public async Task<Result<SyncManifestDto>> GetManifestAsync(int bookId, CancellationToken cancellationToken = default)
    {
        var snapshotResult = await GetSnapshotAsync(bookId, cancellationToken);

        if (snapshotResult.IsFailure)
        {
            return Result.Failure<SyncManifestDto>(snapshotResult.Error!);
        }

        var snapshot = snapshotResult.Value;

        var media = snapshot.Contents
            .SelectMany(c => c.Blocks)
            .Select(b => b.Media)
            .OfType<MediaSummaryDto>()
            .DistinctBy(m => m.Id)
            .ToList();

        var manifest = new SyncManifestDto(
            snapshot.Book.Id,
            snapshot.Version,
            DateTime.UtcNow, // Gercek PublishedAt Faz 3'te (BookPublication.PublishedAt) gelecek
            snapshot.Contents.Count,
            media,
            ComputeChecksum(snapshot));

        return Result.Success(manifest);
    }

    public Task<Result<SyncChangesDto>> GetChangesAsync(int bookId, int fromVersion, CancellationToken cancellationToken = default)
    {
        // STUB: gercek delta hesabi PublishedContent.Version uzerinden Faz 4'te gelecek.
        var changes = new SyncChangesDto(fromVersion, fromVersion, [], [], [], []);
        return Task.FromResult(Result.Success(changes));
    }

    private static SyncSnapshotDto BuildSnapshot(Book book)
    {
        var bookDto = new SyncBookDto(
            book.Id, book.Title, book.Slug, book.Description, book.LanguageCode, book.Version);

        var modules = book.Modules
            .Select(m => new SyncModuleDto(m.Id, m.BookId, m.Name, m.Description, m.DisplayOrder))
            .ToList();

        var contents = book.Modules
            .SelectMany(m => m.Contents)
            .Select(c => new SyncContentDto(
                c.Id,
                c.ModuleId,
                c.Title,
                c.Summary,
                c.DisplayOrder,
                c.Blocks.Select(BuildBlockDto).ToList()))
            .ToList();

        return new SyncSnapshotDto(book.Version, bookDto, modules, contents);
    }

    private static SyncContentBlockDto BuildBlockDto(ContentBlock block)
    {
        var mediaDto = block.Media is null
            ? null
            : new MediaSummaryDto(block.Media.Id, block.Media.StoragePath, block.Media.Checksum, block.Media.FileSize);

        return new SyncContentBlockDto(block.Id, block.Type, block.Text, block.DataJson, mediaDto, block.DisplayOrder);
    }

    /// <summary>
    /// Snapshot'in JSON serilestirmesinin SHA-256 ozeti - icerik degismedigi
    /// surece ayni checksum uretilir, mobil bunu bozulma kontrolu icin kullanabilir.
    /// </summary>
    private static string ComputeChecksum(SyncSnapshotDto snapshot)
    {
        var json = JsonSerializer.Serialize(snapshot);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes);
    }
}
