using Isbak_SAR_Guide.Business.Common;
using Isbak_SAR_Guide.Business.DTOs.Media;

namespace Isbak_SAR_Guide.Business.Services.Abstract;

public interface IMediaService
{
    /// <summary>
    /// declaredFileName sadece FileName metadata'sina gider (goruntuleme amacli,
    /// asla bir dosya yoluna donusmez). declaredLength Content-Length gibi
    /// istemci beyanidir - guvenlik sinirlarindan biri, tek kaynak degil;
    /// gercek boyut yuklenen baytlardan yeniden dogrulanir.
    /// </summary>
    Task<Result<MediaDto>> UploadAsync(
        Stream content, string declaredFileName, long declaredLength, CancellationToken cancellationToken = default);

    Task<Result<MediaDto>> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Hicbir ContentBlock'un artik referans vermedigi eski medyayi diskten ve DB'den temizler.</summary>
    Task<Result<int>> CleanupOrphansAsync(CancellationToken cancellationToken = default);
}
