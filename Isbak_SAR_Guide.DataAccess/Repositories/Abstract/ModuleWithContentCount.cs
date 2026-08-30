using Isbak_SAR_Guide.Entities.Content;

namespace Isbak_SAR_Guide.DataAccess.Repositories.Abstract;

/// <summary>
/// Faz 13.4: admin panelin modul listesi + icerik sayisi ihtiyacini TEK
/// sorguda karsilar (N+1'in kokeni: her modul icin ayri "kac content var"
/// cagrisi). Module entity'sinin kendisine ContentCount eklemek yerine ayri
/// bir projeksiyon kaydi - entity'ler anemik POCO, hesaplanmis alan tasimaz.
/// </summary>
public sealed record ModuleWithContentCount(Module Module, int ContentCount);
