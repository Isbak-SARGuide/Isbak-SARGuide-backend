using Isbak_SAR_Guide.Entities.Content;
using Microsoft.EntityFrameworkCore.Storage;

namespace Isbak_SAR_Guide.DataAccess.Repositories.Abstract;

public interface IUnitOfWork
{
    IBookRepository Books { get; }

    IRepository<Module> Modules { get; }

    IRepository<Content> Contents { get; }

    IRepository<ContentBlock> ContentBlocks { get; }

    IRepository<Media> Media { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Faz 3 (Publishing) icin: bir transaction icinde birden fazla
    /// SaveChanges cagrisini atomik yapmak gerektiginde kullanilir.
    /// </summary>
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}
