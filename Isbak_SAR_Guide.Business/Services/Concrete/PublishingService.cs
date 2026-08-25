using Isbak_SAR_Guide.Business.Common;
using Isbak_SAR_Guide.Business.DTOs.Publishing;
using Isbak_SAR_Guide.Business.Services.Abstract;
using Isbak_SAR_Guide.DataAccess.Repositories.Abstract;

namespace Isbak_SAR_Guide.Business.Services.Concrete;

public class PublishingService(IUnitOfWork unitOfWork) : IPublishingService
{
    // Iskelet asamasinda CS9113 (okunmayan parametre) vermemesi icin field'a
    // baglandi; D parcasi (6.3-d) bu field uzerinden calisacak.
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public Task<Result<PublishResultDto>> PublishAsync(
        int bookId,
        string publishedById,
        CancellationToken cancellationToken = default)
    {
        // D parcasinda (6.3-d) doldurulacak: agac topla -> serialize ->
        // checksum -> versiyon bump -> tek transaction'da yaz.
        throw new NotImplementedException();
    }
}
