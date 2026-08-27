namespace Isbak_SAR_Guide.Business.Common;

/// <summary>
/// Yerel disk depolama ayarlari (Faz 6). MinIO'ya gecis gerekirse sadece
/// IStorageService'in yeni bir implementasyonu eklenir, bu tip degismez.
/// </summary>
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>
    /// Fiziksel dosyalarin yazildigi kok klasor - API projesinin content root'una
    /// gore ("../storage", cunku API `Isbak_SAR_Guide.API/` icinden calistirilir
    /// ama `storage/` repo kokunde yasar - icerik import surecinden beri boyle,
    /// bkz. CLAUDE.md). Media.StoragePath bu kokten GORELI ve "media/" ile
    /// baslar (orn. "media/2026/08/&lt;guid&gt;.png") - zaten import edilmis 93
    /// medya satiri da bu sekilde ("media/{slug}/{dosya}"), yeni yuklemeler ayni
    /// sekli korur. Static file middleware bu kok klasoru web kokunde ("")
    /// servis eder, boylece StoragePath'e tek bir "/" eklemek dogru URL'i verir
    /// (SnapshotBuilder.BuildBlockDto).
    /// </summary>
    // set (init degil): API katmani, Program.cs'te bu goreli degeri
    // IWebHostEnvironment.ContentRootPath'e gore mutlak yola cevirmek icin
    // PostConfigure ile yeniden yazar (process CWD'sine guvenmek testlerde
    // yanlis sonuc verir - bkz. Program.cs).
    public required string BasePath { get; set; }

    public long MaxFileSizeBytes { get; init; } = 20 * 1024 * 1024;

    /// <summary>Yetim medya temizliginde bir dosyanin "yeterince eski" sayilmasi icin gereken sure.</summary>
    public int OrphanGraceHours { get; init; } = 24;
}
