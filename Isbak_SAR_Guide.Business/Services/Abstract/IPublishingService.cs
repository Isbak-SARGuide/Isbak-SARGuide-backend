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

    /// <summary>
    /// Faz 12.6: gecmis bir versiyonun icerigini YENI bir versiyon olarak
    /// tekrar yayinlar - publication modeli immutable oldugu icin "geri alma"
    /// var olan bir satiri degistirmek degil, eski icerigi tekrar yayinlamaktir
    /// (git revert gibi, git reset degil). Draft agacina (Module/Content/
    /// ContentBlock) HIC DOKUNMAZ - sadece mobilin gordugu yayin gecmisini
    /// etkiler; CMS'teki taslak toVersion'daki haline donmez.
    /// Sozlesme: kitap veya toVersion yoksa NotFound; toVersion >= mevcut en
    /// son versiyon ise Validation (geriye gitme semantigi - ileri veya ayni
    /// versiyona "rollback" anlamsiz); basarida yeni versiyon
    /// max(BookPublication.Version) + 1'dir, PublishAsync ile ayni transaction/
    /// conflict garantisi.
    /// </summary>
    Task<Result<PublishResultDto>> RollbackAsync(
        int bookId,
        int toVersion,
        string publishedById,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Follow-up (web ekibi geri bildirimi, Frontend-Notlar-ve-Oneriler.md
    /// madde 9b): kitabin tum yayin gecmisini (en yeniden eskiye) doner -
    /// RollbackAsync'in "toVersion" girdisini elle ezberlemek yerine gercek
    /// bir dropdown'a cevirir. Sozlesme: kitap yoksa NotFound; hic yayin
    /// yoksa bos liste (ilk yayindan once gecerli bir durum, hata degil).
    /// </summary>
    Task<Result<IReadOnlyList<PublicationSummaryDto>>> GetHistoryAsync(int bookId, CancellationToken cancellationToken = default);
}
