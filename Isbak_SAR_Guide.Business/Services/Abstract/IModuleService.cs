using Isbak_SAR_Guide.Business.Common;
using Isbak_SAR_Guide.Business.DTOs.Common;
using Isbak_SAR_Guide.Business.DTOs.Modules;

namespace Isbak_SAR_Guide.Business.Services.Abstract;

public interface IModuleService
{
    Task<Result<PagedResult<ModuleDto>>> GetPagedAsync(
        int bookId, int page, int pageSize, bool? isPublished, CancellationToken cancellationToken = default);

    Task<Result<ModuleDto>> GetByIdAsync(int bookId, int id, CancellationToken cancellationToken = default);

    Task<Result<ModuleDto>> CreateAsync(int bookId, CreateModuleDto dto, CancellationToken cancellationToken = default);

    Task<Result<ModuleDto>> UpdateAsync(int bookId, int id, UpdateModuleDto dto, CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(int bookId, int id, CancellationToken cancellationToken = default);

    Task<Result> ReorderAsync(int bookId, ReorderDto dto, CancellationToken cancellationToken = default);
}
