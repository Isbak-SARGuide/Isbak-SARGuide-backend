using Isbak_SAR_Guide.Business.Common;
using Isbak_SAR_Guide.Business.DTOs.Media;
using Isbak_SAR_Guide.Business.Services.Abstract;
using Isbak_SAR_Guide.DataAccess.Context;
using Isbak_SAR_Guide.DataAccess.Repositories.Abstract;
using Isbak_SAR_Guide.Entities.Content;
using Isbak_SAR_Guide.Entities.Content.Enums;
using Isbak_SAR_Guide.Tests.Unit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Isbak_SAR_Guide.Tests.Integration;

/// <summary>
/// Faz 6 (Media Pipeline) guvenlik/davranis testleri - ApiFactory gercek
/// Postgres + izole bir temp storage klasoru kullanir (bkz. ApiFactory),
/// bu yuzden LocalFileStorageService gercekten diske yazar/siler; mock yok.
/// Faz 12.7'den beri gorsel govdeleri TestImageFactory ile uretilir -
/// UploadAsync artik SkiaSharp'la GERCEKTEN decode/encode ettigi icin
/// (WebP donusumu + thumbnail) ImageSignatureDetectorTests.BuildMinimalPng
/// gibi sahte-header-gercek-piksel-yok yardimcilar yeterli degil (o
/// yardimcilar hala ImageSignatureDetector'in KENDI hand-rolled parser'ini
/// test etmeye devam ediyor, sadece burada kullanilmiyorlar).
/// </summary>
[Collection("Api")]
public class MediaServiceTests(ApiFactory factory)
{
    [Fact]
    public async Task UploadAsync_WithValidPng_CreatesWebPMediaAndWritesFileToDisk()
    {
        // Faz 12.7: STORAGE'A YAZILAN dosya artik her zaman WebP - orijinal
        // yukleme formati (burada PNG) sadece giris dogrulamasi icin onemli,
        // ContentType/StoragePath donusum SONRASI degerleri yansitir.
        var bytes = TestImageFactory.BuildRealPng(10, 20);

        var result = await UploadAsync(bytes, "foto.png");

        result.IsSuccess.ShouldBeTrue();
        result.Value.ContentType.ShouldBe("image/webp");
        result.Value.StoragePath.ShouldEndWith(".webp");
        result.Value.Width.ShouldBe(10);
        result.Value.Height.ShouldBe(20);
        result.Value.FileSize.ShouldBeGreaterThan(0);
        result.Value.ThumbnailStoragePath.ShouldNotBeNull();
        result.Value.ThumbnailStoragePath.ShouldEndWith("-thumb.webp");

        PhysicalFileExists(result.Value.StoragePath).ShouldBeTrue();
        PhysicalFileExists(result.Value.ThumbnailStoragePath!).ShouldBeTrue();
    }

    [Fact]
    public async Task UploadAsync_WithValidJpeg_DetectsDimensionsAndConvertsToWebP()
    {
        var bytes = TestImageFactory.BuildRealJpeg(640, 480);

        var result = await UploadAsync(bytes, "foto.jpg");

        result.IsSuccess.ShouldBeTrue();
        result.Value.ContentType.ShouldBe("image/webp");
        result.Value.Width.ShouldBe(640);
        result.Value.Height.ShouldBe(480);
    }

    [Fact]
    public async Task UploadAsync_WithValidGif_DetectsDimensionsAndConvertsToWebP()
    {
        var bytes = TestImageFactory.BuildRealGif1X1();

        var result = await UploadAsync(bytes, "animasyon.gif");

        result.IsSuccess.ShouldBeTrue();
        result.Value.ContentType.ShouldBe("image/webp");
        result.Value.Width.ShouldBe(1);
        result.Value.Height.ShouldBe(1);
    }

    [Fact]
    public async Task UploadAsync_ThumbnailIsSmallerThanFullDimensionSource()
    {
        // Kaynak, varsayilan ThumbnailMaxDimension'i (400) asan bir boyutta -
        // thumbnail dosyasinin fiziksel olarak ana dosyadan kucuk oldugunu
        // (yeniden boyutlandirmanin gercekten calistigini) kanitlar.
        var bytes = TestImageFactory.BuildRealPng(1200, 800);

        var result = await UploadAsync(bytes, "buyuk-foto.png");
        result.IsSuccess.ShouldBeTrue();

        using var scope = factory.Services.CreateScope();
        var storageOptions = scope.ServiceProvider.GetRequiredService<IOptions<StorageOptions>>();
        var basePath = storageOptions.Value.BasePath;

        var mainFileSize = new FileInfo(Path.Combine(basePath, result.Value.StoragePath)).Length;
        var thumbnailFileSize = new FileInfo(Path.Combine(basePath, result.Value.ThumbnailStoragePath!)).Length;

        thumbnailFileSize.ShouldBeLessThan(mainFileSize);
    }

    [Fact]
    public async Task UploadAsync_WithNonImageBytes_ReturnsValidationError()
    {
        byte[] bytes = [0x01, 0x02, 0x03, 0x04, 0x05];

        var result = await UploadAsync(bytes, "not-an-image.png");

        result.IsFailure.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.Validation);
    }

    [Fact]
    public async Task UploadAsync_WithSignatureValidButUndecodableBytes_ReturnsValidationErrorNotServerError()
    {
        // Faz 12.7 kod inceleme bulgusu: ImageSignatureDetectorTests.BuildMinimalPng
        // gercek bir imaj DEGIL - sadece Detect/IHDR'in ihtiyac duydugu baytlari
        // (sahte header, gercek piksel/zlib verisi yok) icerir. SKBitmap.Decode
        // boyle "imza dogru ama govde bozuk" bir dosyada BEKLENENIN AKSINE null
        // degil ArgumentNullException firlatiyordu - MediaService bunu
        // yakalamazsa saldirgan kontrollu boyle bir dosya 500'e (global exception
        // handler) duserdi, temiz bir 400 Validation yerine. Bu test dogrudan
        // o yakalamayi kanitlar.
        var bytes = ImageSignatureDetectorTests.BuildMinimalPng(10, 10);

        var result = await UploadAsync(bytes, "sahte-ama-imzali.png");

        result.IsFailure.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.Validation);
    }

    [Fact]
    public async Task UploadAsync_IgnoresDeclaredFileNameExtension_TrustsDetectedContentType()
    {
        // Dosya adi ".txt" diyor ama baytlar gercek bir PNG - detector'in
        // uzantiyi degil imzayi esas aldigini kanitlar. ContentType donusum
        // SONRASI degeri (webp) yansitir, orijinal PNG'yi degil.
        var bytes = TestImageFactory.BuildRealPng(4, 4);

        var result = await UploadAsync(bytes, "aslinda-metin.txt");

        result.IsSuccess.ShouldBeTrue();
        result.Value.ContentType.ShouldBe("image/webp");
    }

    [Fact]
    public async Task UploadAsync_ExceedingConfiguredMaxSize_ReturnsValidationError()
    {
        long maxSize;
        using (var scope = factory.Services.CreateScope())
        {
            maxSize = scope.ServiceProvider.GetRequiredService<IOptions<StorageOptions>>().Value.MaxFileSizeBytes;
        }

        var oversized = new byte[maxSize + 1];

        var result = await UploadAsync(oversized, "buyuk.png");

        result.IsFailure.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.Validation);
    }

    [Fact]
    public async Task UploadAsync_EmptyFile_ReturnsValidationError()
    {
        var result = await UploadAsync([], "bos.png");

        result.IsFailure.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.Validation);
    }

    [Fact]
    public async Task UploadAsync_DuplicateContent_ReturnsSameMediaWithoutWritingSecondFile()
    {
        var bytes = TestImageFactory.BuildRealPng(2, 2);

        var first = await UploadAsync(bytes, "birinci.png");
        var second = await UploadAsync(bytes, "ikinci.png");

        first.IsSuccess.ShouldBeTrue();
        second.IsSuccess.ShouldBeTrue();
        second.Value.Id.ShouldBe(first.Value.Id);
        second.Value.StoragePath.ShouldBe(first.Value.StoragePath);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Isbak_SAR_GuideDbContext>();
        var count = await dbContext.Set<Media>().CountAsync(m => m.Checksum == first.Value.Checksum);
        count.ShouldBe(1);
    }

    [Fact]
    public async Task DeleteAsync_WhenReferencedByContentBlock_ReturnsConflict()
    {
        var mediaId = await CreateReferencedMediaAsync();

        var result = await DeleteAsync(mediaId);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.Conflict);
    }

    [Fact]
    public async Task DeleteAsync_WhenNotReferenced_RemovesRowAndPhysicalFile()
    {
        var bytes = TestImageFactory.BuildRealPng(1, 1);
        var uploaded = await UploadAsync(bytes, "silinecek.png");
        uploaded.IsSuccess.ShouldBeTrue();

        var deleteResult = await DeleteAsync(uploaded.Value.Id);
        deleteResult.IsSuccess.ShouldBeTrue();

        PhysicalFileExists(uploaded.Value.StoragePath).ShouldBeFalse();
        PhysicalFileExists(uploaded.Value.ThumbnailStoragePath!).ShouldBeFalse();

        var getResult = await GetByIdAsync(uploaded.Value.Id);
        getResult.IsFailure.ShouldBeTrue();
        getResult.Error!.Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public async Task UploadAsync_SameContentAfterSoftDelete_SucceedsWithNewRow()
    {
        // Backend-Yapilacaklar.md #5: Checksum'un unique index'i eskiden
        // partial DEGILDI - soft-delete edilen bir Media'nin checksum'i
        // tabloyu isgal etmeye devam ediyordu. Ayni icerik tekrar yuklenince
        // unique violation'a carpiyor, "eszamanli yukleme yarisi" kurtarma
        // yolu devreye giriyordu, ama FindByChecksumAsync soft-delete
        // filtresine tabi oldugu icin "kazanani" hic bulamiyor, kalici
        // (retry'la duzelmeyen) Media.ConcurrentUploadUnresolved 500'u
        // uretiyordu. MakeMediaChecksumIndexPartial migration'i bunu cozer:
        // index artik sadece silinmemis satirlar arasinda tekil.
        var bytes = TestImageFactory.BuildRealPng(3, 3);
        var first = await UploadAsync(bytes, "tekrar-yuklenecek.png");
        first.IsSuccess.ShouldBeTrue();

        var deleteResult = await DeleteAsync(first.Value.Id);
        deleteResult.IsSuccess.ShouldBeTrue();

        var second = await UploadAsync(bytes, "tekrar-yuklenecek.png");

        second.IsSuccess.ShouldBeTrue(second.Error?.Message);
        second.Value.Id.ShouldNotBe(first.Value.Id);
    }

    [Fact]
    public async Task CleanupOrphansAsync_RemovesOnlyOldUnreferencedMedia()
    {
        // Farkli boyutlar kasitli: BuildMinimalPng'in urettigi baytlar sadece
        // genislik/yukseklige gore degisiyor - ayni boyut ayni checksum (dedup)
        // demek, testin "iki bagimsiz Media satiri" varsayimini bozardi.
        var oldOrphanId = await UploadAndBackdateAsync("eski-yetim.png", width: 101, hoursAgo: 48);
        var recentOrphanId = await UploadAndBackdateAsync("yeni-yetim.png", width: 102, hoursAgo: 1);
        var oldReferencedId = await CreateReferencedMediaAsync(backdateHoursAgo: 48);

        using var scope = factory.Services.CreateScope();
        var mediaService = scope.ServiceProvider.GetRequiredService<IMediaService>();
        var cleanupResult = await mediaService.CleanupOrphansAsync();

        cleanupResult.IsSuccess.ShouldBeTrue();
        cleanupResult.Value.ShouldBe(1);

        (await mediaService.GetByIdAsync(oldOrphanId)).IsFailure.ShouldBeTrue();
        (await mediaService.GetByIdAsync(recentOrphanId)).IsSuccess.ShouldBeTrue();
        (await mediaService.GetByIdAsync(oldReferencedId)).IsSuccess.ShouldBeTrue();
    }

    // ---- Yardımcılar ----

    private async Task<Business.Common.Result<MediaDto>> UploadAsync(byte[] bytes, string fileName)
    {
        using var scope = factory.Services.CreateScope();
        var mediaService = scope.ServiceProvider.GetRequiredService<IMediaService>();
        using var stream = new MemoryStream(bytes);
        return await mediaService.UploadAsync(stream, fileName, bytes.LongLength);
    }

    private async Task<Business.Common.Result<MediaDto>> GetByIdAsync(int id)
    {
        using var scope = factory.Services.CreateScope();
        var mediaService = scope.ServiceProvider.GetRequiredService<IMediaService>();
        return await mediaService.GetByIdAsync(id);
    }

    private async Task<Business.Common.Result> DeleteAsync(int id)
    {
        using var scope = factory.Services.CreateScope();
        var mediaService = scope.ServiceProvider.GetRequiredService<IMediaService>();
        return await mediaService.DeleteAsync(id);
    }

    private async Task<int> UploadAndBackdateAsync(string fileName, int width, int hoursAgo)
    {
        var bytes = TestImageFactory.BuildRealPng(width, height: 1);
        var uploaded = await UploadAsync(bytes, fileName);
        uploaded.IsSuccess.ShouldBeTrue();

        await BackdateAsync(uploaded.Value.Id, hoursAgo);
        return uploaded.Value.Id;
    }

    /// <summary>Bir ContentBlock'un referans verdigi, dilerse geriye tarihli bir Media kaydi olusturur.</summary>
    private async Task<int> CreateReferencedMediaAsync(int? backdateHoursAgo = null)
    {
        using var scope = factory.Services.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var book = new Book { Title = "Media Test Kitabı", Slug = $"media-test-{Guid.NewGuid():N}" };
        var module = new Module { Name = "Test Modülü", DisplayOrder = 0 };
        var media = new Media
        {
            FileName = "referenced.png",
            StoragePath = $"media/test/{Guid.NewGuid():N}.png",
            MediaType = MediaType.Image,
            ContentType = "image/png",
            FileSize = 1,
            Checksum = Guid.NewGuid().ToString("N"),
        };
        var content = new Content
        {
            Title = "Test İçeriği",
            DisplayOrder = 0,
            Blocks = { new ContentBlock { Type = ContentBlockType.Image, DisplayOrder = 0, Media = media } },
        };
        module.Contents.Add(content);
        book.Modules.Add(module);

        await unitOfWork.Books.AddAsync(book);
        await unitOfWork.SaveChangesAsync();

        if (backdateHoursAgo is { } hours)
        {
            await BackdateAsync(media.Id, hours);
        }

        return media.Id;
    }

    private async Task BackdateAsync(int mediaId, int hoursAgo)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Isbak_SAR_GuideDbContext>();
        var media = await dbContext.Set<Media>().SingleAsync(m => m.Id == mediaId);
        media.CreatedAt = DateTime.UtcNow.AddHours(-hoursAgo);
        await dbContext.SaveChangesAsync();
    }

    private bool PhysicalFileExists(string storagePath)
    {
        using var scope = factory.Services.CreateScope();
        var basePath = scope.ServiceProvider.GetRequiredService<IOptions<StorageOptions>>().Value.BasePath;
        return File.Exists(Path.Combine(basePath, storagePath));
    }
}
