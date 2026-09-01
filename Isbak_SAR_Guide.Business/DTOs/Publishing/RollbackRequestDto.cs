namespace Isbak_SAR_Guide.Business.DTOs.Publishing;

/// <summary>
/// Faz 12.6: geri alinacak hedef versiyon. Ayri bir tip - tek int'i dogrudan
/// [FromBody] almak yerine, diger POST body'leriyle (CreateBookDto vb.)
/// tutarli bir sekil kurar ve ileride ek alan (orn. bir sebep metni)
/// eklemeyi kirici olmayan bir degisiklik yapar.
/// </summary>
public sealed record RollbackRequestDto(int ToVersion);
