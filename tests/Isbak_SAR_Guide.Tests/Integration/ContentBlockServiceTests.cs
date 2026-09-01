using Isbak_SAR_Guide.Business.Common;
using Isbak_SAR_Guide.Business.DTOs.Common;
using Isbak_SAR_Guide.Business.DTOs.ContentBlocks;
using Isbak_SAR_Guide.Business.Services.Abstract;
using Isbak_SAR_Guide.DataAccess.Repositories.Abstract;
using Isbak_SAR_Guide.Entities.Content;
using Isbak_SAR_Guide.Entities.Content.Enums;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Isbak_SAR_Guide.Tests.Integration;

/// <summary>
/// Faz 5 CMS: ContentBlockService. Desen ModuleServiceTests.cs ile ayni; ek
/// olarak DataJson'in gecerli JSON zorunlulugunu ve MediaId FK dogrulamasini
/// kapsar. ContentBlock'ta unique DisplayOrder index'i YOK (bkz.
/// ContentBlockConfiguration) - reorder testi burada sadece siranin dogru
/// uygulandigini kanitlar, constraint kacisini degil.
/// </summary>
[Collection("Api")]
public class ContentBlockServiceTests(ApiFactory factory)
{
    [Fact]
    public async Task CreateAsync_WithValidDataJson_RoundTripsVerbatim()
    {
        var contentId = await CreateContentAsync();
        const string dataJson = "{\"rows\":[1,2,3]}";

        var result = await CreateAsync(contentId, new CreateContentBlockDto(ContentBlockType.Table, null, dataJson, null));

        result.IsSuccess.ShouldBeTrue();
        result.Value.DataJson.ShouldBe(dataJson);
    }

    [Fact]
    public async Task CreateAsync_WithInvalidJson_ReturnsValidationError()
    {
        var contentId = await CreateContentAsync();

        var result = await CreateAsync(contentId, new CreateContentBlockDto(ContentBlockType.Table, null, "{not-json", null));

        result.IsFailure.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.Validation);
    }

    [Fact]
    public async Task CreateAsync_WithOutOfRangeType_ReturnsValidationError()
    {
        var contentId = await CreateContentAsync();

        var result = await CreateAsync(contentId, new CreateContentBlockDto((ContentBlockType)99, "metin", null, null));

        result.IsFailure.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.Validation);
    }

    [Fact]
    public async Task CreateAsync_WithNonExistentMediaId_ReturnsValidationError()
    {
        var contentId = await CreateContentAsync();

        var result = await CreateAsync(contentId, new CreateContentBlockDto(ContentBlockType.Image, null, null, 999_999));

        result.IsFailure.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.Validation);
    }

    [Fact]
    public async Task CreateAsync_WithExistingMediaId_Succeeds()
    {
        var contentId = await CreateContentAsync();
        var mediaId = await CreateMediaAsync();

        var result = await CreateAsync(contentId, new CreateContentBlockDto(ContentBlockType.Image, null, null, mediaId));

        result.IsSuccess.ShouldBeTrue();
        result.Value.MediaId.ShouldBe(mediaId);
    }

    [Fact]
    public async Task CreateAsync_WithNonExistentContent_ReturnsNotFound()
    {
        var result = await CreateAsync(contentId: 999_999, new CreateContentBlockDto(ContentBlockType.Text, "metin", null, null));

        result.IsFailure.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public async Task ReorderAsync_WithValidPermutation_PersistsNewOrder()
    {
        var contentId = await CreateContentAsync();
        var b1 = await CreateAsync(contentId, new CreateContentBlockDto(ContentBlockType.Text, "B1", null, null));
        var b2 = await CreateAsync(contentId, new CreateContentBlockDto(ContentBlockType.Text, "B2", null, null));

        using var scope = factory.Services.CreateScope();
        var blockService = scope.ServiceProvider.GetRequiredService<IContentBlockService>();

        var reorderResult = await blockService.ReorderAsync(contentId, new ReorderDto([b2.Value.Id, b1.Value.Id]));
        reorderResult.IsSuccess.ShouldBeTrue();

        var paged = await blockService.GetPagedAsync(contentId, page: 1, pageSize: 10);
        paged.Value.Items.Select(b => b.Id).ShouldBe([b2.Value.Id, b1.Value.Id]);
    }

    [Fact]
    public async Task ReorderAsync_DoesNotAlterSiblingsDataJson()
    {
        // 13.5: ReorderHelper artik her kardeste sadece DisplayOrder'i kirli
        // isaretliyor (UpdateProperty), tum entity'yi degil. DataJson jsonb
        // oldugu icin Postgres ZATEN ilk INSERT'te kendi kanonik bicimine
        // (anahtar sirasi/bosluk) donusturur - bu yuzden burada orijinal
        // gonderilen string'e degil, reorder ONCESI DB'den okunan kanonik
        // degere karsi karsilastiriyoruz: reorder bu degeri hic degistirmemeli,
        // tasinan b1 dahil.
        var contentId = await CreateContentAsync();
        const string tableJson = "{\"headers\":[\"a\",\"b\"],\"rows\":[[1,2]]}";
        const string warningJson = "{\"severity\":\"high\"}";
        var b1 = await CreateAsync(contentId, new CreateContentBlockDto(ContentBlockType.Table, null, tableJson, null));
        var b2 = await CreateAsync(contentId, new CreateContentBlockDto(ContentBlockType.Text, "B2", null, null));
        var b3 = await CreateAsync(contentId, new CreateContentBlockDto(ContentBlockType.Warning, null, warningJson, null));

        using var scope = factory.Services.CreateScope();
        var blockService = scope.ServiceProvider.GetRequiredService<IContentBlockService>();

        var beforeReorder = await blockService.GetPagedAsync(contentId, page: 1, pageSize: 10);
        var b1DataJsonBefore = beforeReorder.Value.Items.Single(b => b.Id == b1.Value.Id).DataJson;
        var b3DataJsonBefore = beforeReorder.Value.Items.Single(b => b.Id == b3.Value.Id).DataJson;

        // Sadece b1/b2'yi yer degistir - b3'un pozisyonu (index 2) ayni kalir.
        var reorderResult = await blockService.ReorderAsync(
            contentId, new ReorderDto([b2.Value.Id, b1.Value.Id, b3.Value.Id]));
        reorderResult.IsSuccess.ShouldBeTrue();

        var afterReorder = await blockService.GetPagedAsync(contentId, page: 1, pageSize: 10);
        afterReorder.Value.Items.Single(b => b.Id == b1.Value.Id).DataJson.ShouldBe(b1DataJsonBefore);
        afterReorder.Value.Items.Single(b => b.Id == b3.Value.Id).DataJson.ShouldBe(b3DataJsonBefore);
    }

    // ---- Yardımcılar ----

    private async Task<Result<ContentBlockDto>> CreateAsync(int contentId, CreateContentBlockDto dto)
    {
        using var scope = factory.Services.CreateScope();
        var blockService = scope.ServiceProvider.GetRequiredService<IContentBlockService>();
        return await blockService.CreateAsync(contentId, dto);
    }

    private async Task<int> CreateContentAsync()
    {
        using var scope = factory.Services.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var book = new Book
        {
            Title = "CMS Test Kitabı",
            Slug = $"cms-test-{Guid.NewGuid():N}",
        };
        var module = new Module { Name = "Test Modülü", DisplayOrder = 0 };
        var content = new Content { Title = "Test İçeriği", DisplayOrder = 0 };
        module.Contents.Add(content);
        book.Modules.Add(module);

        await unitOfWork.Books.AddAsync(book);
        await unitOfWork.SaveChangesAsync();
        return content.Id;
    }

    private async Task<int> CreateMediaAsync()
    {
        using var scope = factory.Services.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var media = new Media
        {
            FileName = "test.png",
            StoragePath = $"test/{Guid.NewGuid():N}.png",
            MediaType = MediaType.Image,
            ContentType = "image/png",
            FileSize = 1024,
            Checksum = Guid.NewGuid().ToString("N"),
        };

        await unitOfWork.Media.AddAsync(media);
        await unitOfWork.SaveChangesAsync();
        return media.Id;
    }
}
