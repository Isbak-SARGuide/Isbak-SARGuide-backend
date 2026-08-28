using Isbak_SAR_Guide.DataAccess.Context;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Isbak_SAR_Guide.DataAccess.HealthChecks;

/// <summary>
/// Readiness kontrolu - Postgres'e gercekten baglanabiliyor muyuz. API
/// katmani bu sinifi hic tanimaz (DataAccess'e dogrudan referans yasagi),
/// sadece "database" adiyla kaydedilmis bir IHealthCheck'i cagirir
/// (bkz. AddDataAccess() ve Program.cs'teki MapHealthChecks).
/// </summary>
public class DatabaseHealthCheck(Isbak_SAR_GuideDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);

        return canConnect
            ? HealthCheckResult.Healthy("Postgres baglantisi kuruldu.")
            : HealthCheckResult.Unhealthy("Postgres'e baglanilamadi.");
    }
}
