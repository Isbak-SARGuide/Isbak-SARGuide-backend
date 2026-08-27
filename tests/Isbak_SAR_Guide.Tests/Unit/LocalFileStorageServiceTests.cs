using System.Text;
using Isbak_SAR_Guide.Business.Common;
using Isbak_SAR_Guide.Business.Services.Concrete;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Isbak_SAR_Guide.Tests.Unit;

/// <summary>
/// Path traversal'a karsi IKINCI savunma katmani (birincisi: MediaService
/// relativePath'i her zaman kendisi uretir). Bu testler, o birinci katman
/// hic olmasa/bozulsa bile LocalFileStorageService'in kok disina yazmayi/
/// silmeyi REDDETTIGINI dogrudan kanitlar - dosya sistemine gercekten
/// dokunur (gecici bir klasorde), mock yok.
/// </summary>
public class LocalFileStorageServiceTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"lfs-tests-{Guid.NewGuid():N}");
    private readonly LocalFileStorageService _sut;

    public LocalFileStorageServiceTests()
    {
        Directory.CreateDirectory(_tempRoot);
        _sut = new LocalFileStorageService(Options.Create(new StorageOptions { BasePath = _tempRoot }));
    }

    [Theory]
    [InlineData("../escape.txt")]
    [InlineData("../../escape.txt")]
    [InlineData("sub/../../escape.txt")]
    public async Task SaveAsync_WithTraversalPath_ThrowsUnauthorizedAccessException(string maliciousRelativePath)
    {
        using var content = new MemoryStream("payload"u8.ToArray());

        await Should.ThrowAsync<UnauthorizedAccessException>(
            () => _sut.SaveAsync(content, maliciousRelativePath));

        // Kok disina hicbir sey yazilmadigini da dogrula - istisna atilip
        // dosyanin yine de olusmasi (orn. kismi yazma) daha kotu olurdu.
        Directory.GetFiles(_tempRoot, "*", SearchOption.AllDirectories).ShouldBeEmpty();
    }

    [Fact]
    public async Task DeleteAsync_WithTraversalPath_ThrowsUnauthorizedAccessException()
    {
        await Should.ThrowAsync<UnauthorizedAccessException>(
            () => _sut.DeleteAsync("../../etc/passwd"));
    }

    [Fact]
    public async Task SaveAsync_WithSafeRelativePath_WritesFileUnderBase()
    {
        var bytes = Encoding.UTF8.GetBytes("hello");
        using var content = new MemoryStream(bytes);

        await _sut.SaveAsync(content, "2026/08/safe.png");

        var expectedPath = Path.Combine(_tempRoot, "2026", "08", "safe.png");
        File.Exists(expectedPath).ShouldBeTrue();
        (await File.ReadAllBytesAsync(expectedPath)).ShouldBe(bytes);
    }

    [Fact]
    public async Task DeleteAsync_NonExistentFile_DoesNotThrow()
    {
        await Should.NotThrowAsync(() => _sut.DeleteAsync("2026/08/never-existed.png"));
    }

    [Fact]
    public async Task DeleteAsync_ExistingFile_RemovesIt()
    {
        using (var content = new MemoryStream("x"u8.ToArray()))
        {
            await _sut.SaveAsync(content, "2026/08/to-delete.png");
        }

        await _sut.DeleteAsync("2026/08/to-delete.png");

        File.Exists(Path.Combine(_tempRoot, "2026", "08", "to-delete.png")).ShouldBeFalse();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }
}
