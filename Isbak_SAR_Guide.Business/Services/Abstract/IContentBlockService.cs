using Isbak_SAR_Guide.Business.Common;
using Isbak_SAR_Guide.Business.DTOs.Common;
using Isbak_SAR_Guide.Business.DTOs.ContentBlocks;

namespace Isbak_SAR_Guide.Business.Services.Abstract;

public interface IContentBlockService
{
    Task<Result<PagedResult<ContentBlockDto>>> GetPagedAsync(
        int contentId, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<Result<ContentBlockDto>> GetByIdAsync(int contentId, int id, CancellationToken cancellationToken = default);

    Task<Result<ContentBlockDto>> CreateAsync(int contentId, CreateContentBlockDto dto, CancellationToken cancellationToken = default);

    Task<Result<ContentBlockDto>> UpdateAsync(int contentId, int id, UpdateContentBlockDto dto, CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(int contentId, int id, CancellationToken cancellationToken = default);

    Task<Result> ReorderAsync(int contentId, ReorderDto dto, CancellationToken cancellationToken = default);
}
