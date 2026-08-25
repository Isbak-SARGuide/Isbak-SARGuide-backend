namespace Isbak_SAR_Guide.DataAccess.Repositories.Abstract;

/// <summary>
/// Delta sorgusunun bir content icin dondurdugu tek satir: content basina
/// EN SON durum (greatest-per-group), PayloadJson ham metniyle birlikte -
/// yazici bunu deserialize etmeden WriteRawValue ile gomer.
/// </summary>
public sealed record PublishedContentChange(int ContentId, string PayloadJson, bool IsDeleted);
