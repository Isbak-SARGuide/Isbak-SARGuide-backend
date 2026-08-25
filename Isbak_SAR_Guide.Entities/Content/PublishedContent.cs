namespace Isbak_SAR_Guide.Entities.Content;

/// <summary>
/// Tek bir Content'in belirli bir yayin (BookPublication) anindaki immutable
/// anlik goruntusu. Delta sorgusu (fromVersion -> toVersion) bu tablo uzerinden
/// tek satirlik WHERE ile calisir. IsDeleted burada tombstone anlamina gelir:
/// mobil "bu icerik silindi" bilgisini bu bayraktan ogrenir.
/// </summary>
public class PublishedContent
{
    public int Id { get; set; }

    public int BookPublicationId { get; set; }

    /// <summary>
    /// BookPublication.BookId ile ayni deger - denormalize edildi cunku bu
    /// satirlar hic guncellenmez (immutable), drift riski yok. Amac: mobil
    /// delta sorgusunu join'siz tek WHERE'e indirmek.
    /// </summary>
    public int BookId { get; set; }

    public int ContentId { get; set; }

    public int Version { get; set; }

    /// <summary>
    /// Content + Blocks'un tam anlik goruntusu (jsonb).
    /// </summary>
    public string PayloadJson { get; set; } = null!;

    public string Checksum { get; set; } = null!;

    public bool IsDeleted { get; set; }

    public BookPublication BookPublication { get; set; } = null!;
}
