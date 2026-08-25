using Isbak_SAR_Guide.Business.Common;
using Isbak_SAR_Guide.Business.DTOs.Books;

namespace Isbak_SAR_Guide.Business.Services.Abstract;

public interface IBookService
{
    Task<Result<IReadOnlyList<BookDto>>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Result<BookDto>> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<Result<BookDto>> CreateAsync(CreateBookDto dto, CancellationToken cancellationToken = default);

    Task<Result<BookDto>> UpdateAsync(int id, UpdateBookDto dto, CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
