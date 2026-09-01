using Isbak_SAR_Guide.Business.Common;
using Isbak_SAR_Guide.Business.DTOs.Common;
using Isbak_SAR_Guide.Business.DTOs.Contents;

namespace Isbak_SAR_Guide.Business.Services.Abstract;

public interface IContentService
{
    Task<Result<PagedResult<ContentDto>>> GetPagedAsync(
        int moduleId, int page, int pageSize, bool? isPublished, CancellationToken cancellationToken = default);

    /// <summary>Kitap genelinde duz (flat) icerik listesi - GetPagedAsync'in modul-scope karsiligi, N+1'i onler.</summary>
    Task<Result<PagedResult<ContentDto>>> GetPagedByBookIdAsync(
        int bookId, int page, int pageSize, bool? isPublished, CancellationToken cancellationToken = default);

    Task<Result<ContentDto>> GetByIdAsync(int moduleId, int id, CancellationToken cancellationToken = default);

    Task<Result<ContentDto>> CreateAsync(int moduleId, CreateContentDto dto, CancellationToken cancellationToken = default);

    Task<Result<ContentDto>> UpdateAsync(int moduleId, int id, UpdateContentDto dto, CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(int moduleId, int id, CancellationToken cancellationToken = default);

    Task<Result> ReorderAsync(int moduleId, ReorderDto dto, CancellationToken cancellationToken = default);
}
