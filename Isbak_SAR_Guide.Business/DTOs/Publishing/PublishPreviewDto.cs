namespace Isbak_SAR_Guide.Business.DTOs.Publishing;

/// <summary>
/// "Yayinla"ya basmadan ONCE gosterilecek onizleme - hicbir sey yazmaz,
/// PublishAsync cagrilsa ne olacagini anlatir. Kullanicinin bulgusu: Yayinla
/// hicbir geri bildirim olmadan direkt commit ediyordu; bu uc admin'e "onay"
/// icin gercek bir karar noktasi verir. BookMetadataChanged AYRI bir alan -
/// sadece Book'un kendi basligi/aciklamasi degisirse Modules/Contents
/// listelerinin ucu de bos kalabilir, HasChanges yine de true olur; bu bayrak
/// olmadan admin "degisti diyor ama liste bos" diye sasirirdi.
/// </summary>
public sealed record PublishPreviewDto(
    bool HasChanges,
    bool BookMetadataChanged,
    IReadOnlyList<PublishPreviewItemDto> AddedModules,
    IReadOnlyList<PublishPreviewItemDto> ChangedModules,
    IReadOnlyList<PublishPreviewItemDto> RemovedModules,
    IReadOnlyList<PublishPreviewItemDto> AddedContents,
    IReadOnlyList<PublishPreviewItemDto> ChangedContents,
    IReadOnlyList<PublishPreviewItemDto> RemovedContents);
