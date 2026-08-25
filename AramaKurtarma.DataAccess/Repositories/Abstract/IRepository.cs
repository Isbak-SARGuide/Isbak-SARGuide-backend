using AramaKurtarma.Entities.Common;

namespace AramaKurtarma.DataAccess.Repositories.Abstract;

/// <summary>
/// Generic CRUD sozlesmesi. Ozel sorgular (Include, filtreleme vb.) gerektiginde
/// entity'ye ozel bir arayuz (orn. IBookRepository : IRepository&lt;Book&gt;)
/// eklenir - burasi bilerek minimal tutuldu.
/// </summary>
public interface IRepository<T> where T : BaseEntity
{
    Task<T?> FindByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<T>> FindAllAsync(CancellationToken cancellationToken = default);

    Task AddAsync(T entity, CancellationToken cancellationToken = default);

    void Update(T entity);

    void Remove(T entity);
}
