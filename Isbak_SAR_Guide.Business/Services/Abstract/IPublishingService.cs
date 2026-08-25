using Isbak_SAR_Guide.Business.Common;
using Isbak_SAR_Guide.Business.DTOs.Publishing;

namespace Isbak_SAR_Guide.Business.Services.Abstract;

public interface IPublishingService
{
    /// <summary>
    /// Kitabin o anki draft agacini immutable bir yayina cevirir.
    /// Sozlesme: kitap yoksa NotFound; basarida yeni versiyon
    /// max(BookPublication.Version) + 1'dir ve BookPublication +
    /// PublishedContent satirlari + Book.Version guncellemesi TEK
    /// transaction icinde yazilir - ya hepsi ya hicbiri.
    /// publishedById controller'dan parametre olarak gelir (User claim'i);
    /// Business, HTTP dunyasindan habersiz kalir.
    /// </summary>
    Task<Result<PublishResultDto>> PublishAsync(
        int bookId,
        string publishedById,
        CancellationToken cancellationToken = default);
}
