using AramaKurtarma.DataAccess.Context;
using AramaKurtarma.DataAccess.Repositories.Abstract;
using AramaKurtarma.Entities.Content;
using Microsoft.EntityFrameworkCore;

namespace AramaKurtarma.DataAccess.Repositories.Concrete;

public class BookRepository(AramaKurtarmaDbContext dbContext)
    : EfRepository<Book>(dbContext), IBookRepository
{
    public async Task<Book?> GetWithFullTreeAsync(int id, CancellationToken cancellationToken = default) =>
        await DbSet
            .Include(book => book.Modules.OrderBy(m => m.DisplayOrder))
                .ThenInclude(module => module.Contents.OrderBy(c => c.DisplayOrder))
                    .ThenInclude(content => content.Blocks.OrderBy(b => b.DisplayOrder))
                        .ThenInclude(block => block.Media)
            .FirstOrDefaultAsync(book => book.Id == id, cancellationToken);
}
