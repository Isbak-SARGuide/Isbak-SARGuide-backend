using Isbak_SAR_Guide.Entities.Content;

namespace Isbak_SAR_Guide.DataAccess.Repositories.Abstract;

public interface IBookRepository : IRepository<Book>
{
    /// <summary>
    /// Kitabin tum agacini (Moduller -> Contents -> Blocks -> Media) tek
    /// sorguda ceker. Sync snapshot uretimi icin gerekli - genel amacli
    /// IRepository&lt;T&gt; bilerek boyle bir metot barindirmiyor.
    /// </summary>
    Task<Book?> GetWithFullTreeAsync(int id, CancellationToken cancellationToken = default);
}
