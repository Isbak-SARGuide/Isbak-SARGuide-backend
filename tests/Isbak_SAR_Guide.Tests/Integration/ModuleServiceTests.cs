using Isbak_SAR_Guide.Business.Common;
using Isbak_SAR_Guide.Business.DTOs.Common;
using Isbak_SAR_Guide.Business.DTOs.Contents;
using Isbak_SAR_Guide.Business.DTOs.Modules;
using Isbak_SAR_Guide.Business.Services.Abstract;
using Isbak_SAR_Guide.DataAccess.Repositories.Abstract;
using Isbak_SAR_Guide.Entities.Content;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Isbak_SAR_Guide.Tests.Integration;

/// <summary>
/// Faz 5 CMS: ModuleService. Bilerek HTTP degil dogrudan IModuleService test
/// edilir (PublishingTests.cs'teki gerekce ayni). Reorder testleri ozellikle
/// onemli: gercek Postgres'teki (BookId, DisplayOrder) unique+partial index'e
/// karsi calisiyor - ReorderHelper'in iki fazli negatif-DisplayOrder numarasi
/// mock'ta degil burada kanitlanir.
/// </summary>
[Collection("Api")]
public class ModuleServiceTests(ApiFactory factory)
{
    [Fact]
    public async Task CreateAsync_MultipleModules_AssignsSequentialDisplayOrder()
    {
        var bookId = await CreateBookAsync();

        var first = await CreateAsync(bookId, new CreateModuleDto("Birinci Modül", null));
        var second = await CreateAsync(bookId, new CreateModuleDto("İkinci Modül", null));

        first.Value.DisplayOrder.ShouldBe(0);
        second.Value.DisplayOrder.ShouldBe(1);
    }

    [Fact]
    public async Task CreateAsync_WithEmptyName_ReturnsValidationError()
    {
        var bookId = await CreateBookAsync();

        var result = await CreateAsync(bookId, new CreateModuleDto("", null));

        result.IsFailure.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.Validation);
    }

    [Fact]
    public async Task CreateAsync_WithNonExistentBook_ReturnsNotFound()
    {
        var result = await CreateAsync(bookId: 999_999, new CreateModuleDto("Modül", null));

        result.IsFailure.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public async Task GetByIdAsync_WithWrongBookId_ReturnsNotFound()
    {
        var bookId = await CreateBookAsync();
        var otherBookId = await CreateBookAsync();
        var module = await CreateAsync(bookId, new CreateModuleDto("Modül", null));

        using var scope = factory.Services.CreateScope();
        var moduleService = scope.ServiceProvider.GetRequiredService<IModuleService>();
        var result = await moduleService.GetByIdAsync(otherBookId, module.Value.Id);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public async Task UpdateAsync_WithValidDto_UpdatesFields()
    {
        var bookId = await CreateBookAsync();
        var module = await CreateAsync(bookId, new CreateModuleDto("Eski Ad", "Eski açıklama"));

        using var scope = factory.Services.CreateScope();
        var moduleService = scope.ServiceProvider.GetRequiredService<IModuleService>();
        var result = await moduleService.UpdateAsync(bookId, module.Value.Id, new UpdateModuleDto("Yeni Ad", "Yeni açıklama", true));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Name.ShouldBe("Yeni Ad");
        result.Value.Description.ShouldBe("Yeni açıklama");
        result.Value.IsPublished.ShouldBeTrue();
        // DisplayOrder update DTO'sunda yok - dokunulmamis kalmali.
        result.Value.DisplayOrder.ShouldBe(0);
    }

    [Fact]
    public async Task DeleteAsync_RemovesModule_SubsequentGetReturnsNotFound()
    {
        var bookId = await CreateBookAsync();
        var module = await CreateAsync(bookId, new CreateModuleDto("Silinecek", null));

        using var scope = factory.Services.CreateScope();
        var moduleService = scope.ServiceProvider.GetRequiredService<IModuleService>();

        var deleteResult = await moduleService.DeleteAsync(bookId, module.Value.Id);
        deleteResult.IsSuccess.ShouldBeTrue();

        var getResult = await moduleService.GetByIdAsync(bookId, module.Value.Id);
        getResult.IsFailure.ShouldBeTrue();
        getResult.Error!.Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public async Task DeleteAsync_MiddleModule_CompactsRemainingSiblingsDisplayOrder()
    {
        // ContentServiceTests.DeleteAsync_MiddleContent_CompactsRemainingSiblingsDisplayOrder'daki
        // ayni gerekce - A(0),B(1),C(2), B silinince A,C bosluksuz (0,1) olmali.
        var bookId = await CreateBookAsync();
        var a = await CreateAsync(bookId, new CreateModuleDto("A", null));
        var b = await CreateAsync(bookId, new CreateModuleDto("B", null));
        var c = await CreateAsync(bookId, new CreateModuleDto("C", null));

        using (var deleteScope = factory.Services.CreateScope())
        {
            var moduleService = deleteScope.ServiceProvider.GetRequiredService<IModuleService>();
            (await moduleService.DeleteAsync(bookId, b.Value.Id)).IsSuccess.ShouldBeTrue();
        }

        using var scope = factory.Services.CreateScope();
        var verifyService = scope.ServiceProvider.GetRequiredService<IModuleService>();
        var paged = await verifyService.GetPagedAsync(bookId, page: 1, pageSize: 10, isPublished: null);

        var items = paged.Value.Items.OrderBy(x => x.DisplayOrder).ToList();
        items.Select(x => x.Id).ShouldBe([a.Value.Id, c.Value.Id]);
        items.Select(x => x.DisplayOrder).ShouldBe([0, 1]);
    }

    [Fact]
    public async Task GetPagedAsync_ReturnsRequestedPageAndTotalCount()
    {
        var bookId = await CreateBookAsync();
        await CreateAsync(bookId, new CreateModuleDto("M1", null));
        await CreateAsync(bookId, new CreateModuleDto("M2", null));
        await CreateAsync(bookId, new CreateModuleDto("M3", null));

        using var scope = factory.Services.CreateScope();
        var moduleService = scope.ServiceProvider.GetRequiredService<IModuleService>();
        var result = await moduleService.GetPagedAsync(bookId, page: 2, pageSize: 2, isPublished: null);

        result.IsSuccess.ShouldBeTrue();
        result.Value.TotalCount.ShouldBe(3);
        result.Value.Items.Count.ShouldBe(1);
        result.Value.Items[0].Name.ShouldBe("M3");
    }

    [Fact]
    public async Task GetPagedAsync_ReturnsContentCount_ExcludingSoftDeletedContent()
    {
        var bookId = await CreateBookAsync();
        var module = await CreateAsync(bookId, new CreateModuleDto("Modül", null));

        // Her adim KENDI scope'unda (ayri DbContext) - gercekte her HTTP istegi
        // ayri bir scope alir. Content2 (create1 sirasinda tracked) ile
        // DeleteAsync'in kompaksiyon adiminin AsNoTracking okudugu "kalan
        // kardesler" listesi AYNI scope'ta olsaydi, ayni Id icin iki farkli
        // instance tracked olmaya calisilir ve EF identity conflict'i firlatirdi -
        // gercek uretimde olmayan bir durum (bkz. UserServiceTests'teki ayni
        // gerekce).
        Result<ContentDto> content2;
        using (var createScope = factory.Services.CreateScope())
        {
            var contentService = createScope.ServiceProvider.GetRequiredService<IContentService>();
            await contentService.CreateAsync(module.Value.Id, new CreateContentDto("C1", null));
            content2 = await contentService.CreateAsync(module.Value.Id, new CreateContentDto("C2", null));
            await contentService.CreateAsync(module.Value.Id, new CreateContentDto("C3", null));
        }

        using (var deleteScope = factory.Services.CreateScope())
        {
            var contentService = deleteScope.ServiceProvider.GetRequiredService<IContentService>();
            (await contentService.DeleteAsync(module.Value.Id, content2.Value.Id)).IsSuccess.ShouldBeTrue();
        }

        using var scope = factory.Services.CreateScope();
        var moduleService = scope.ServiceProvider.GetRequiredService<IModuleService>();
        var result = await moduleService.GetPagedAsync(bookId, page: 1, pageSize: 10, isPublished: null);

        result.IsSuccess.ShouldBeTrue();
        var dto = result.Value.Items.Single(m => m.Id == module.Value.Id);
        // C2 soft-delete edildi - sayilmamali. C1 ve C3 kalir.
        dto.ContentCount.ShouldBe(2);
    }

    [Fact]
    public async Task ReorderAsync_WithValidPermutation_PersistsNewOrderDespiteUniqueIndex()
    {
        var bookId = await CreateBookAsync();
        var m1 = await CreateAsync(bookId, new CreateModuleDto("M1", null));
        var m2 = await CreateAsync(bookId, new CreateModuleDto("M2", null));
        var m3 = await CreateAsync(bookId, new CreateModuleDto("M3", null));

        using var scope = factory.Services.CreateScope();
        var moduleService = scope.ServiceProvider.GetRequiredService<IModuleService>();

        // Ters cevir: M3, M1, M2. Tek adimda yazilsaydi (ParentId, DisplayOrder)
        // unique+partial index'e gecici carpardi - ReorderHelper'in negatif
        // ara-adimi bunu onlemeli.
        var reorderResult = await moduleService.ReorderAsync(
            bookId, new ReorderDto([m3.Value.Id, m1.Value.Id, m2.Value.Id]));

        reorderResult.IsSuccess.ShouldBeTrue();

        var paged = await moduleService.GetPagedAsync(bookId, page: 1, pageSize: 10, isPublished: null);
        paged.Value.Items.Select(m => m.Id).ShouldBe([m3.Value.Id, m1.Value.Id, m2.Value.Id]);
    }

    [Fact]
    public async Task ReorderAsync_WithMissingId_ReturnsValidationError()
    {
        var bookId = await CreateBookAsync();
        var m1 = await CreateAsync(bookId, new CreateModuleDto("M1", null));
        await CreateAsync(bookId, new CreateModuleDto("M2", null));

        using var scope = factory.Services.CreateScope();
        var moduleService = scope.ServiceProvider.GetRequiredService<IModuleService>();

        // Sadece m1 verildi, m2 eksik - sibling-set esitligi basarisiz olmali.
        var result = await moduleService.ReorderAsync(bookId, new ReorderDto([m1.Value.Id]));

        result.IsFailure.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.Validation);
    }

    [Fact]
    public async Task ReorderAsync_WithIdFromAnotherBook_ReturnsValidationError()
    {
        var bookId = await CreateBookAsync();
        var otherBookId = await CreateBookAsync();
        var m1 = await CreateAsync(bookId, new CreateModuleDto("M1", null));
        var foreign = await CreateAsync(otherBookId, new CreateModuleDto("Yabancı", null));

        using var scope = factory.Services.CreateScope();
        var moduleService = scope.ServiceProvider.GetRequiredService<IModuleService>();

        var result = await moduleService.ReorderAsync(bookId, new ReorderDto([m1.Value.Id, foreign.Value.Id]));

        result.IsFailure.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.Validation);
    }

    // ---- Yardımcılar ----

    private async Task<Result<ModuleDto>> CreateAsync(int bookId, CreateModuleDto dto)
    {
        using var scope = factory.Services.CreateScope();
        var moduleService = scope.ServiceProvider.GetRequiredService<IModuleService>();
        return await moduleService.CreateAsync(bookId, dto);
    }

    private async Task<int> CreateBookAsync()
    {
        using var scope = factory.Services.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var book = new Book
        {
            Title = "CMS Test Kitabı",
            Slug = $"cms-test-{Guid.NewGuid():N}",
        };

        await unitOfWork.Books.AddAsync(book);
        await unitOfWork.SaveChangesAsync();
        return book.Id;
    }
}
