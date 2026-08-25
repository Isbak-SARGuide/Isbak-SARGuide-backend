using Isbak_SAR_Guide.DataAccess.Context;
using Isbak_SAR_Guide.DataAccess.Repositories.Abstract;
using Isbak_SAR_Guide.Entities.Content;
using Microsoft.EntityFrameworkCore.Storage;

namespace Isbak_SAR_Guide.DataAccess.Repositories.Concrete;

public class UnitOfWork : IUnitOfWork
{
    private readonly Isbak_SAR_GuideDbContext _dbContext;

    public UnitOfWork(Isbak_SAR_GuideDbContext dbContext)
    {
        _dbContext = dbContext;

        Books = new BookRepository(dbContext);
        Modules = new EfRepository<Module>(dbContext);
        Contents = new EfRepository<Content>(dbContext);
        ContentBlocks = new EfRepository<ContentBlock>(dbContext);
        Media = new EfRepository<Media>(dbContext);
    }

    public IBookRepository Books { get; }

    public IRepository<Module> Modules { get; }

    public IRepository<Content> Contents { get; }

    public IRepository<ContentBlock> ContentBlocks { get; }

    public IRepository<Media> Media { get; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _dbContext.SaveChangesAsync(cancellationToken);

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        _dbContext.Database.BeginTransactionAsync(cancellationToken);
}
