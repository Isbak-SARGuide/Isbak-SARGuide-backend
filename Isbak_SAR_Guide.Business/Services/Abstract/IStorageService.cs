namespace Isbak_SAR_Guide.Business.Services.Abstract;

/// <summary>
/// Ham dosya yazma/silme soyutlamasi (Strategy - roadmap §2.2). Implementasyon
/// bilgisi (yerel disk, ileride MinIO) burasi disinda hicbir yere sizmaz.
/// Sadece caller'in ONCEDEN URETTIGI (kullanicidan gelen isim/uzanti DEGIL -
/// bkz. MediaService) guvenli bir goreli yolu bilir - path traversal
/// sorumlulugu caller'in guvenli yol uretmesinde, burasi sadece savunmanin
/// ikinci katmanidir (bkz. LocalFileStorageService).
/// </summary>
public interface IStorageService
{
    /// <summary>
    /// relativePath, '/' ile ayrilmis, kok dizinin disina cikmayan bir yol
    /// olmali (orn. "2026/08/&lt;guid&gt;.png"). Var olan klasorler otomatik olusturulur.
    /// </summary>
    Task SaveAsync(Stream content, string relativePath, CancellationToken cancellationToken = default);

    /// <summary>Dosya yoksa sessizce hicbir sey yapmaz - silme idempotent olmali.</summary>
    Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default);
}
