using Isbak_SAR_Guide.Business.Common;
using Isbak_SAR_Guide.Business.DTOs.Auth;

namespace Isbak_SAR_Guide.Business.Services.Abstract;

public interface IAuthService
{
    Task<Result<LoginResponseDto>> LoginAsync(LoginDto dto, CancellationToken cancellationToken = default);
}
