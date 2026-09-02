using Isbak_SAR_Guide.Business.Common;
using Isbak_SAR_Guide.Business.DTOs.Users;

namespace Isbak_SAR_Guide.Business.Services.Abstract;

public interface IUserService
{
    Task<Result<UserDto>> CreateAsync(CreateUserDto dto, CancellationToken cancellationToken = default);

    Task<Result<PagedResult<UserDto>>> GetAllAsync(int page, int pageSize, CancellationToken cancellationToken = default);

    Task<Result<UserDto>> ChangeRoleAsync(string id, ChangeRoleDto dto, CancellationToken cancellationToken = default);

    /// <summary>Hard delete. Admin hesaplari silinemez (BookPublication.PublishedById immutable yayin gecmisini korur).</summary>
    Task<Result> DeleteAsync(string id, CancellationToken cancellationToken = default);

    Task<Result> ChangeOwnPasswordAsync(string userId, ChangePasswordDto dto, CancellationToken cancellationToken = default);
}
