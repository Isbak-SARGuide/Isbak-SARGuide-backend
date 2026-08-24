using AramaKurtarma.DataAccess.Context;
using AramaKurtarma.Entities.Content;
using Microsoft.EntityFrameworkCore.Storage;

namespace AramaKurtarma.DataAccess.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly AramaKurtarmaDbContext _dbContext;

    public UnitOfWork(AramaKurtarmaDbContext dbContext)
    {
        _dbContext = dbContext;

        Books = new EfRepository<Book>(dbContext);
        Modules = new EfRepository<Module>(dbContext);
        Contents = new EfRepository<Content>(dbContext);
        ContentBlocks = new EfRepository<ContentBlock>(dbContext);
        Media = new EfRepository<Media>(dbContext);
    }

    public IRepository<Book> Books { get; }

    public IRepository<Module> Modules { get; }

    public IRepository<Content> Contents { get; }

    public IRepository<ContentBlock> ContentBlocks { get; }

    public IRepository<Media> Media { get; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _dbContext.SaveChangesAsync(cancellationToken);

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        _dbContext.Database.BeginTransactionAsync(cancellationToken);
}
