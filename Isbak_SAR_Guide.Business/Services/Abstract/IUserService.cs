using Isbak_SAR_Guide.Business.Common;
using Isbak_SAR_Guide.Business.DTOs.Users;

namespace Isbak_SAR_Guide.Business.Services.Abstract;

public interface IUserService
{
    Task<Result<UserDto>> CreateAsync(CreateUserDto dto, CancellationToken cancellationToken = default);

    Task<Result<PagedResult<UserDto>>> GetAllAsync(int page, int pageSize, CancellationToken cancellationToken = default);

    Task<Result<UserDto>> ChangeRoleAsync(string id, ChangeRoleDto dto, CancellationToken cancellationToken = default);

    /// <summary>actingUserId: kendi hesabini kilitlemeye calisan bir Admin'i reddetmek icin.</summary>
    Task<Result> DeactivateAsync(string id, string actingUserId, CancellationToken cancellationToken = default);

    /// <summary>DeactivateAsync'in tersi - LockoutEnd'i kaldirir. Idempotent: zaten aktif bir kullanici icin de basarili doner.</summary>
    Task<Result> ActivateAsync(string id, CancellationToken cancellationToken = default);

    Task<Result> ChangeOwnPasswordAsync(string userId, ChangePasswordDto dto, CancellationToken cancellationToken = default);
}
