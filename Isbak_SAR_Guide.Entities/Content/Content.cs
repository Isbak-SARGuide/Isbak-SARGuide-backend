using Isbak_SAR_Guide.Entities.Common;

namespace Isbak_SAR_Guide.Entities.Content;

public class Content : BaseEntity
{
    public int ModuleId { get; set; }

    public string Title { get; set; } = null!;

    public string? Summary { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsPublished { get; set; }

    /// <summary>
    /// Ayni aileden birden fazla Content'i (orn. dugum varyantlari
    /// F8/F9/TH/ABK) mobilde sekmeli tek sayfada birlestirmek icin ortak
    /// anahtar. Null ise bu Content tekil bir konu (BSAFE'in 3 konusu gibi),
    /// mobil normal liste satiri olarak gosterir. Gruplama string parse ile
    /// degil bu alanla yapilir - baslik metni degisse bile gruplama bozulmaz.
    /// </summary>
    public string? VariantGroupKey { get; set; }

    /// <summary>
    /// VariantGroupKey doluysa sekmede gorunecek kisa etiket ("F8" gibi) -
    /// Title'dan ayri, cunku Title daha aciklayici kalabilir. Sekme sirasi
    /// DisplayOrder'dan gelir (grup icinde konumu belirtir).
    /// </summary>
    public string? VariantLabel { get; set; }

    public Module Module { get; set; } = null!;

    public ICollection<ContentBlock> Blocks { get; set; } = new List<ContentBlock>();
}
