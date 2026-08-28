using Isbak_SAR_Guide.Business.Common;
using Isbak_SAR_Guide.Business.Services.Abstract;
using Microsoft.Extensions.Options;

namespace Isbak_SAR_Guide.Business.Services.Concrete;

public class LocalFileStorageService(IOptions<StorageOptions> options) : IStorageService
{
    // BasePath, Program.cs'teki PostConfigure tarafindan zaten mutlak yola
    // cevrilmis olarak gelir (ContentRootPath'e gore - process CWD'sine
    // guvenilmez). GetFullPath burada sadece normalize eder (\..\ gibi
    // parcalari temizler) - hem yazma hem traversal kontrolu ayni mutlak
    // koku kullanmali, iki ayri cozumleme tutarsizlik riski tasir.
    private readonly string _baseAbsolutePath = Path.GetFullPath(options.Value.BasePath);

    public async Task SaveAsync(Stream content, string relativePath, CancellationToken cancellationToken = default)
    {
        var absolutePath = ResolveSafePath(relativePath);

        var directory = Path.GetDirectoryName(absolutePath)
            ?? throw new InvalidOperationException($"Gecersiz depolama yolu: {relativePath}");
        Directory.CreateDirectory(directory);

        await using var fileStream = new FileStream(absolutePath, FileMode.Create, FileAccess.Write);
        await content.CopyToAsync(fileStream, cancellationToken);
    }

    public Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var absolutePath = ResolveSafePath(relativePath);

        if (File.Exists(absolutePath))
        {
            File.Delete(absolutePath);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Ikinci savunma katmani (birincisi: MediaService relativePath'i HER ZAMAN
    /// kendisi uretir, kullanicidan gelen dosya adini asla kullanmaz). Yine de
    /// caginan taraf varsayimi bozarsa (orn. ileride baska bir caller eklenirse)
    /// birlesmis mutlak yolun _baseAbsolutePath disina cikmadigini burada da
    /// dogrularız - "../../etc/passwd" gibi bir relativePath sessizce
    /// yazilmak/silinmek yerine acikca reddedilir.
    /// </summary>
    private string ResolveSafePath(string relativePath)
    {
        var combined = Path.GetFullPath(Path.Combine(_baseAbsolutePath, relativePath));

        var baseWithSeparator = _baseAbsolutePath.EndsWith(Path.DirectorySeparatorChar)
            ? _baseAbsolutePath
            : _baseAbsolutePath + Path.DirectorySeparatorChar;

        if (!combined.StartsWith(baseWithSeparator, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException($"Depolama kokunun disina cikan yol reddedildi: {relativePath}");
        }

        return combined;
    }
}
