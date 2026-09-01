using Isbak_SAR_Guide.Business.Common;
using Isbak_SAR_Guide.Business.DTOs.Books;
using Isbak_SAR_Guide.Business.Services.Abstract;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Isbak_SAR_Guide.Tests.Integration;

/// <summary>
/// Faz 12.5: BookService'in kendi CRUD'u hicbir yerde test edilmiyordu -
/// Module/Content/ContentBlock testleri her zaman unitOfWork.Books.AddAsync
/// ile DOGRUDAN bir test kitabi olusturuyor (BookService.CreateAsync'i hic
/// cagirmadan), BooksController de sadece GetAll icin bir smoke testi var.
/// Bu dosya BookService'in CreateAsync/UpdateAsync/DeleteAsync/GetByIdAsync'ini
/// ModuleServiceTests.cs'teki desenle (bilerek HTTP degil, dogrudan IBookService)
/// kapatir.
/// </summary>
[Collection("Api")]
public class BookServiceTests(ApiFactory factory)
{
    [Fact]
    public async Task CreateAsync_WithValidDto_ReturnsCreatedBook()
    {
        var result = await CreateAsync(NewBookDto());

        result.IsSuccess.ShouldBeTrue();
        result.Value.Title.ShouldBe("Test Kitabı");
        result.Value.LanguageCode.ShouldBe("tr");
        result.Value.IsPublished.ShouldBeFalse();
    }

    [Fact]
    public async Task CreateAsync_WithEmptyTitle_ReturnsValidationError()
    {
        var dto = NewBookDto() with { Title = "" };

        var result = await CreateAsync(dto);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.Validation);
    }

    [Fact]
    public async Task CreateAsync_WithInvalidSlugFormat_ReturnsValidationError()
    {
        var dto = NewBookDto() with { Slug = "Not A Valid Slug!" };

        var result = await CreateAsync(dto);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.Validation);
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateSlug_ReturnsConflict()
    {
        var dto = NewBookDto();
        var first = await CreateAsync(dto);
        first.IsSuccess.ShouldBeTrue();

        // Ayni slug, farkli baslikla - ikinci satir unique index'e (Slug) carpmalı.
        var second = await CreateAsync(dto with { Title = "Başka Başlık" });

        second.IsFailure.ShouldBeTrue();
        second.Error!.Type.ShouldBe(ErrorType.Conflict);
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistentId_ReturnsNotFound()
    {
        using var scope = factory.Services.CreateScope();
        var bookService = scope.ServiceProvider.GetRequiredService<IBookService>();

        var result = await bookService.GetByIdAsync(999_999);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public async Task UpdateAsync_WithValidDto_UpdatesFields()
    {
        var created = await CreateAsync(NewBookDto());

        using var scope = factory.Services.CreateScope();
        var bookService = scope.ServiceProvider.GetRequiredService<IBookService>();
        var updateDto = new UpdateBookDto("Güncel Başlık", created.Value.Slug, "Güncel açıklama", "en");
        var result = await bookService.UpdateAsync(created.Value.Id, updateDto);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Title.ShouldBe("Güncel Başlık");
        result.Value.Description.ShouldBe("Güncel açıklama");
        result.Value.LanguageCode.ShouldBe("en");
    }

    [Fact]
    public async Task UpdateAsync_WithNonExistentId_ReturnsNotFound()
    {
        using var scope = factory.Services.CreateScope();
        var bookService = scope.ServiceProvider.GetRequiredService<IBookService>();

        var result = await bookService.UpdateAsync(999_999, NewUpdateBookDto());

        result.IsFailure.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public async Task UpdateAsync_WithSlugTakenByAnotherBook_ReturnsConflict()
    {
        var first = await CreateAsync(NewBookDto());
        var second = await CreateAsync(NewBookDto());

        using var scope = factory.Services.CreateScope();
        var bookService = scope.ServiceProvider.GetRequiredService<IBookService>();

        // second'ı first'ün slug'ına tasimaya calis - unique index'e carpmalı.
        var updateDto = new UpdateBookDto("Ad", first.Value.Slug, null, "tr");
        var result = await bookService.UpdateAsync(second.Value.Id, updateDto);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.Conflict);
    }

    [Fact]
    public async Task DeleteAsync_RemovesBook_SubsequentGetReturnsNotFound()
    {
        var created = await CreateAsync(NewBookDto());

        using var scope = factory.Services.CreateScope();
        var bookService = scope.ServiceProvider.GetRequiredService<IBookService>();

        var deleteResult = await bookService.DeleteAsync(created.Value.Id);
        deleteResult.IsSuccess.ShouldBeTrue();

        var getResult = await bookService.GetByIdAsync(created.Value.Id);
        getResult.IsFailure.ShouldBeTrue();
        getResult.Error!.Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public async Task DeleteAsync_WithNonExistentId_ReturnsNotFound()
    {
        using var scope = factory.Services.CreateScope();
        var bookService = scope.ServiceProvider.GetRequiredService<IBookService>();

        var result = await bookService.DeleteAsync(999_999);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public async Task GetAllAsync_IncludesNewlyCreatedBook()
    {
        var created = await CreateAsync(NewBookDto());

        using var scope = factory.Services.CreateScope();
        var bookService = scope.ServiceProvider.GetRequiredService<IBookService>();
        var result = await bookService.GetAllAsync();

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldContain(b => b.Id == created.Value.Id);
    }

    // ---- Yardımcılar ----

    private static CreateBookDto NewBookDto() =>
        new(Title: "Test Kitabı", Slug: $"test-kitabi-{Guid.NewGuid():N}", Description: null);

    private static UpdateBookDto NewUpdateBookDto() =>
        new(Title: "Ad", Slug: $"ad-{Guid.NewGuid():N}", Description: null, LanguageCode: "tr");

    private async Task<Result<BookDto>> CreateAsync(CreateBookDto dto)
    {
        using var scope = factory.Services.CreateScope();
        var bookService = scope.ServiceProvider.GetRequiredService<IBookService>();
        return await bookService.CreateAsync(dto);
    }
}
