using AramaKurtarma.Business.Common;
using AramaKurtarma.Business.DTOs.Auth;

namespace AramaKurtarma.Business.Services.Abstract;

public interface IAuthService
{
    Task<Result<LoginResponseDto>> LoginAsync(LoginDto dto, CancellationToken cancellationToken = default);
}
