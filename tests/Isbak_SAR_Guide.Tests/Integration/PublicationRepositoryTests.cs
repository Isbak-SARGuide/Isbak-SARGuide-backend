using Isbak_SAR_Guide.DataAccess.Context;
using Isbak_SAR_Guide.DataAccess.Repositories.Abstract;
using Isbak_SAR_Guide.Entities.Content;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Isbak_SAR_Guide.Tests.Integration;

/// <summary>
/// PublicationRepository'nin uc kritik davranisini dogrular:
/// 1) hic yayin yokken 0 donmesi (bos kume tuzagi),
/// 2) en buyuk versiyonun donmesi,
/// 3) PublishedContents cocuklarinin navigation uzerinden ayni SaveChanges'te
///    yazilmasi (ayri PublishedContent repo'su olmamasinin dayanagi).
/// Paylasilan seed'e dokunmaz - her test kendi kitabini yaratir.
/// </summary>
[Collection("Api")]
public class PublicationRepositoryTests(ApiFactory factory)
{
    [Fact]
    public async Task GetLatestVersionAsync_WhenBookHasNoPublications_ReturnsZero()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var book = await CreateBookAsync(unitOfWork);
    
        // Act
        var version = await unitOfWork.Publications.GetLatestVersionAsync(book.Id);

        // Assert
        version.ShouldBe(0);
    }

    [Fact]
    public async Task GetLatestVersionAsync_WhenPublicationsExist_ReturnsMaxVersion()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var book = await CreateBookAsync(unitOfWork);
        var adminId = await GetAdminIdAsync(scope);

        await unitOfWork.Publications.AddAsync(BuildPublication(book.Id, version: 1, adminId));
        await unitOfWork.Publications.AddAsync(BuildPublication(book.Id, version: 2, adminId));
        await unitOfWork.SaveChangesAsync();

        // Act
        var version = await unitOfWork.Publications.GetLatestVersionAsync(book.Id);

        // Assert
        version.ShouldBe(2);
    }

    [Fact]
    public async Task AddAsync_WithPublishedContents_InsertsChildrenInSameSaveChanges()
    {
        // Arrange
        int publicationId;
        using (var scope = factory.Services.CreateScope())
        {
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var book = await CreateBookAsync(unitOfWork);
            var adminId = await GetAdminIdAsync(scope);

            var publication = BuildPublication(book.Id, version: 1, adminId);
            publication.PublishedContents.Add(BuildPublishedContent(book.Id, contentId: 101));
            publication.PublishedContents.Add(BuildPublishedContent(book.Id, contentId: 102));

            // Act
            await unitOfWork.Publications.AddAsync(publication);
            await unitOfWork.SaveChangesAsync();
            publicationId = publication.Id;
        }

        // Assert - taze bir context'ten okunur ki change tracker'daki degil,
        // gercekten veritabanina yazilmis satirlar dogrulanmis olsun.
        using (var verifyScope = factory.Services.CreateScope())
        {
            var dbContext = verifyScope.ServiceProvider.GetRequiredService<Isbak_SAR_GuideDbContext>();

            var children = await dbContext.Set<PublishedContent>()
                .Where(pc => pc.BookPublicationId == publicationId)
                .ToListAsync();

            children.Count.ShouldBe(2);
            children.Select(pc => pc.ContentId).ShouldBe([101, 102], ignoreOrder: true);
        }
    }

    private static async Task<Book> CreateBookAsync(IUnitOfWork unitOfWork)
    {
        var book = new Book
        {
            Title = "Publication Test Kitabı",
            Slug = $"publication-test-{Guid.NewGuid():N}",
        };

        await unitOfWork.Books.AddAsync(book);
        await unitOfWork.SaveChangesAsync();
        return book;
    }

    private static async Task<string> GetAdminIdAsync(IServiceScope scope)
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<Isbak_SAR_GuideDbContext>();
        var admin = await dbContext.Users.FirstAsync(u => u.UserName == "admin");
        return admin.Id;
    }

    private static BookPublication BuildPublication(int bookId, int version, string publishedById) => new()
    {
        BookId = bookId,
        Version = version,
        ManifestJson = "{}",
        SnapshotJson = "{}",
        Checksum = "test-checksum",
        PublishedAt = DateTime.UtcNow,
        PublishedById = publishedById,
    };

    private static PublishedContent BuildPublishedContent(int bookId, int contentId) => new()
    {
        BookId = bookId,
        ContentId = contentId,
        Version = 1,
        PayloadJson = "{}",
        Checksum = "test-checksum",
        IsDeleted = false,
    };
}
