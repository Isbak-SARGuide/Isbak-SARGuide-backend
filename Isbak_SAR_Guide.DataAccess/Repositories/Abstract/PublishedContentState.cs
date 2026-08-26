namespace Isbak_SAR_Guide.DataAccess.Repositories.Abstract;

/// <summary>
/// Bir content'in yayin gunlugundeki EN SON durumu (en yuksek versiyonlu
/// satirin ozeti). Publish tek sorguyla bundan iki isi birden cikarir:
/// "icerik degisti mi?" kontrolu (Checksum karsilastirmasi) ve tombstone
/// diff'inin sol kumesi (IsDeleted=false olanlarin id'leri).
/// </summary>
public sealed record PublishedContentState(int ContentId, string Checksum, bool IsDeleted);
