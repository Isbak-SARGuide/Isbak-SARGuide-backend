using System.Linq.Expressions;
using Isbak_SAR_Guide.DataAccess.Context;
using Isbak_SAR_Guide.DataAccess.Repositories.Abstract;
using Isbak_SAR_Guide.Entities.Common;
using Microsoft.EntityFrameworkCore;

namespace Isbak_SAR_Guide.DataAccess.Repositories.Concrete;

public class EfRepository<T>(Isbak_SAR_GuideDbContext dbContext) : IRepository<T>
    where T : BaseEntity
{
    protected readonly DbSet<T> DbSet = dbContext.Set<T>();

    public async Task<T?> FindByIdAsync(int id, CancellationToken cancellationToken = default) =>
        await DbSet.FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);

    // AsNoTracking: butun projede FindAllAsync'in tek amaci listeleme/DTO'ya
    // cevirme (BookService.GetAllAsync, seed kontrolu) - donen entity'ler hicbir
    // yerde dogrudan mutate edilip SaveChanges'e verilmiyor. FindByIdAsync ise
    // KASITLI OLARAK tracked kaliyor - CRUD servislerindeki Update akislarinin
    // cogu "FindByIdAsync -> dto.Adapt(entity) -> SaveChanges" seklinde change
    // tracker'a guveniyor.
    public async Task<IReadOnlyList<T>> FindAllAsync(CancellationToken cancellationToken = default) =>
        await DbSet.AsNoTracking().ToListAsync(cancellationToken);

    public async Task AddAsync(T entity, CancellationToken cancellationToken = default) =>
        await DbSet.AddAsync(entity, cancellationToken);

    public void Update(T entity) => DbSet.Update(entity);

    // Faz 13.5: ReorderHelper'in AsNoTracking'le gelen kardesleri Update()'le
    // isaretlemesi TUM kolonlari (orn. ContentBlock.DataJson - jsonb) UPDATE'e
    // dahil ediyordu, oysa reorder sadece DisplayOrder degistirir. Tek kolonu
    // isaretlemek UPDATE'i o kolonla sinirlar - entity zaten tracked degilse
    // Entry() onu Unchanged olarak attach eder, sonra Property().IsModified
    // state'i Modified'a cevirir (tum entry'yi degil, sadece o kolonu).
    public void UpdateProperty<TProperty>(T entity, Expression<Func<T, TProperty>> propertyExpression) =>
        dbContext.Entry(entity).Property(propertyExpression).IsModified = true;

    public void Remove(T entity) => DbSet.Remove(entity);
}
