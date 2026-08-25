namespace Microsoft.Extensions.DependencyInjection;

public static class DatabaseSeederExtensions
{
    /// <summary>
    /// Root IServiceProvider'dan yeni bir scope acar ve seed'i calistirir.
    /// Sadece Development ortaminda cagrilmalidir (bkz. Program.cs).
    /// </summary>
    public static async Task SeedDatabaseAsync(this IServiceProvider rootServices)
    {
        using var scope = rootServices.CreateScope();
        await Isbak_SAR_Guide.DataAccess.Seed.DatabaseSeeder.SeedAsync(scope.ServiceProvider);
    }
}
