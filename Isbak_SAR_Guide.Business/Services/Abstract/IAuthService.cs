using Isbak_SAR_Guide.Business.Common;
using Isbak_SAR_Guide.Business.DTOs.Auth;

namespace Isbak_SAR_Guide.Business.Services.Abstract;

public interface IAuthService
{
    Task<Result<LoginResponseDto>> LoginAsync(LoginDto dto, CancellationToken cancellationToken = default);

    /// <summary>Rotasyon: sunulan token iptal edilir, yeni bir access+refresh cifti doner.</summary>
    Task<Result<LoginResponseDto>> RefreshAsync(RefreshTokenRequestDto dto, CancellationToken cancellationToken = default);

    /// <summary>Acik logout - idempotent (zaten iptal/yok olan bir token icin de basarili doner).</summary>
    Task<Result> RevokeAsync(RefreshTokenRequestDto dto, CancellationToken cancellationToken = default);
}
