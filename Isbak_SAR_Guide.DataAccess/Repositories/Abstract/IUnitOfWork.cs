using Isbak_SAR_Guide.Entities.Content;
using Microsoft.EntityFrameworkCore.Storage;

namespace Isbak_SAR_Guide.DataAccess.Repositories.Abstract;

public interface IUnitOfWork
{
    IBookRepository Books { get; }

    IModuleRepository Modules { get; }

    IContentRepository Contents { get; }

    IContentBlockRepository ContentBlocks { get; }

    IMediaRepository Media { get; }

    IPublicationRepository Publications { get; }

    IRefreshTokenRepository RefreshTokens { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Bir transaction icinde birden fazla SaveChanges cagrisini atomik yapmak
    /// gerektiginde kullanilir - once PublishingService (Faz 3), sonra
    /// ReorderHelper'in iki fazli Module/Content/ContentBlock reorder'i (Faz 5).
    /// </summary>
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}
