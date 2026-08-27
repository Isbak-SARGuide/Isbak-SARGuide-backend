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
        Modules = new ModuleRepository(dbContext);
        Contents = new ContentRepository(dbContext);
        ContentBlocks = new ContentBlockRepository(dbContext);
        Media = new MediaRepository(dbContext);
        // Ayni dbContext instance'i sart: BeginTransactionAsync o context'in
        // baglantisinda transaction acar; farkli bir context kullansaydi
        // Publications'in (ve ReorderHelper'in Modules/Contents/ContentBlocks'a
        // yazdiklarinin) transaction'in disinda kalirdi.
        Publications = new PublicationRepository(dbContext);
        RefreshTokens = new RefreshTokenRepository(dbContext);
    }

    public IBookRepository Books { get; }

    public IModuleRepository Modules { get; }

    public IContentRepository Contents { get; }

    public IContentBlockRepository ContentBlocks { get; }

    public IMediaRepository Media { get; }

    public IPublicationRepository Publications { get; }

    public IRefreshTokenRepository RefreshTokens { get; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _dbContext.SaveChangesAsync(cancellationToken);

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        _dbContext.Database.BeginTransactionAsync(cancellationToken);
}
