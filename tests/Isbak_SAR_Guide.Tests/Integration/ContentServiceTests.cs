using Isbak_SAR_Guide.Business.Common;
using Isbak_SAR_Guide.Business.DTOs.Common;
using Isbak_SAR_Guide.Business.DTOs.Contents;
using Isbak_SAR_Guide.Business.Services.Abstract;
using Isbak_SAR_Guide.DataAccess.Repositories.Abstract;
using Isbak_SAR_Guide.Entities.Content;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Isbak_SAR_Guide.Tests.Integration;

/// <summary>
/// Faz 5 CMS: ContentService. Desen ModuleServiceTests.cs ile ayni; ek olarak
/// VariantGroupKey/VariantLabel'in birlikte doldurulma kuralini kapsar.
/// </summary>
[Collection("Api")]
public class ContentServiceTests(ApiFactory factory)
{
    [Fact]
    public async Task CreateAsync_MultipleContents_AssignsSequentialDisplayOrder()
    {
        var moduleId = await CreateModuleAsync();

        var first = await CreateAsync(moduleId, new CreateContentDto("Birinci İçerik", null));
        var second = await CreateAsync(moduleId, new CreateContentDto("İkinci İçerik", null));

        first.Value.DisplayOrder.ShouldBe(0);
        second.Value.DisplayOrder.ShouldBe(1);
    }

    [Fact]
    public async Task CreateAsync_WithOnlyVariantLabel_ReturnsValidationError()
    {
        var moduleId = await CreateModuleAsync();

        var result = await CreateAsync(moduleId, new CreateContentDto("Düğüm", null, VariantLabel: "F8"));

        result.IsFailure.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.Validation);
    }

    [Fact]
    public async Task CreateAsync_WithBothVariantFields_Succeeds()
    {
        var moduleId = await CreateModuleAsync();

        var result = await CreateAsync(moduleId, new CreateContentDto("Düğüm", null, VariantGroupKey: "knot-group", VariantLabel: "F8"));

        result.IsSuccess.ShouldBeTrue();
        result.Value.VariantGroupKey.ShouldBe("knot-group");
        result.Value.VariantLabel.ShouldBe("F8");
    }

    [Fact]
    public async Task CreateAsync_WithNonExistentModule_ReturnsNotFound()
    {
        var result = await CreateAsync(moduleId: 999_999, new CreateContentDto("İçerik", null));

        result.IsFailure.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public async Task UpdateAsync_WithValidDto_UpdatesFields()
    {
        var moduleId = await CreateModuleAsync();
        var content = await CreateAsync(moduleId, new CreateContentDto("Eski Başlık", "Eski özet"));

        using var scope = factory.Services.CreateScope();
        var contentService = scope.ServiceProvider.GetRequiredService<IContentService>();
        var result = await contentService.UpdateAsync(
            moduleId, content.Value.Id, new UpdateContentDto("Yeni Başlık", "Yeni özet", true, null, null));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Title.ShouldBe("Yeni Başlık");
        result.Value.IsPublished.ShouldBeTrue();
    }

    [Fact]
    public async Task DeleteAsync_RemovesContent_SubsequentGetReturnsNotFound()
    {
        var moduleId = await CreateModuleAsync();
        var content = await CreateAsync(moduleId, new CreateContentDto("Silinecek", null));

        using var scope = factory.Services.CreateScope();
        var contentService = scope.ServiceProvider.GetRequiredService<IContentService>();

        (await contentService.DeleteAsync(moduleId, content.Value.Id)).IsSuccess.ShouldBeTrue();

        var getResult = await contentService.GetByIdAsync(moduleId, content.Value.Id);
        getResult.IsFailure.ShouldBeTrue();
        getResult.Error!.Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public async Task ReorderAsync_WithValidPermutation_PersistsNewOrder()
    {
        var moduleId = await CreateModuleAsync();
        var c1 = await CreateAsync(moduleId, new CreateContentDto("C1", null));
        var c2 = await CreateAsync(moduleId, new CreateContentDto("C2", null));

        using var scope = factory.Services.CreateScope();
        var contentService = scope.ServiceProvider.GetRequiredService<IContentService>();

        var reorderResult = await contentService.ReorderAsync(moduleId, new ReorderDto([c2.Value.Id, c1.Value.Id]));
        reorderResult.IsSuccess.ShouldBeTrue();

        var paged = await contentService.GetPagedAsync(moduleId, page: 1, pageSize: 10, isPublished: null);
        paged.Value.Items.Select(c => c.Id).ShouldBe([c2.Value.Id, c1.Value.Id]);
    }

    [Fact]
    public async Task ReorderAsync_WithMissingId_ReturnsValidationError()
    {
        var moduleId = await CreateModuleAsync();
        var c1 = await CreateAsync(moduleId, new CreateContentDto("C1", null));
        await CreateAsync(moduleId, new CreateContentDto("C2", null));

        using var scope = factory.Services.CreateScope();
        var contentService = scope.ServiceProvider.GetRequiredService<IContentService>();

        var result = await contentService.ReorderAsync(moduleId, new ReorderDto([c1.Value.Id]));

        result.IsFailure.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.Validation);
    }

    // ---- Yardımcılar ----

    private async Task<Result<ContentDto>> CreateAsync(int moduleId, CreateContentDto dto)
    {
        using var scope = factory.Services.CreateScope();
        var contentService = scope.ServiceProvider.GetRequiredService<IContentService>();
        return await contentService.CreateAsync(moduleId, dto);
    }

    private async Task<int> CreateModuleAsync()
    {
        using var scope = factory.Services.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var book = new Book
        {
            Title = "CMS Test Kitabı",
            Slug = $"cms-test-{Guid.NewGuid():N}",
        };
        var module = new Module { Name = "Test Modülü", DisplayOrder = 0 };
        book.Modules.Add(module);

        await unitOfWork.Books.AddAsync(book);
        await unitOfWork.SaveChangesAsync();
        return module.Id;
    }
}
