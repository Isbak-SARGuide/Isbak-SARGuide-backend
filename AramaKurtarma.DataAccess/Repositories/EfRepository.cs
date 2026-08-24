using AramaKurtarma.DataAccess.Context;
using AramaKurtarma.Entities.Common;
using Microsoft.EntityFrameworkCore;

namespace AramaKurtarma.DataAccess.Repositories;

public class EfRepository<T>(AramaKurtarmaDbContext dbContext) : IRepository<T>
    where T : BaseEntity
{
    protected readonly DbSet<T> DbSet = dbContext.Set<T>();

    public async Task<T?> FindByIdAsync(int id, CancellationToken cancellationToken = default) =>
        await DbSet.FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);

    public async Task<IReadOnlyList<T>> FindAllAsync(CancellationToken cancellationToken = default) =>
        await DbSet.ToListAsync(cancellationToken);

    public async Task AddAsync(T entity, CancellationToken cancellationToken = default) =>
        await DbSet.AddAsync(entity, cancellationToken);

    public void Update(T entity) => DbSet.Update(entity);

    public void Remove(T entity) => DbSet.Remove(entity);
}
