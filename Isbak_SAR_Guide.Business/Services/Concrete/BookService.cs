using FluentValidation;
using Isbak_SAR_Guide.Business.Common;
using Isbak_SAR_Guide.Business.DTOs.Books;
using Isbak_SAR_Guide.Business.Services.Abstract;
using Isbak_SAR_Guide.DataAccess.Repositories.Abstract;
using Isbak_SAR_Guide.Entities.Content;
using Mapster;

namespace Isbak_SAR_Guide.Business.Services.Concrete;

public class BookService(
    IUnitOfWork unitOfWork,
    IValidator<CreateBookDto> createValidator,
    IValidator<UpdateBookDto> updateValidator) : IBookService
{
    public async Task<Result<IReadOnlyList<BookDto>>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var allBooks = await unitOfWork.Books.FindAllAsync(cancellationToken);
        IReadOnlyList<BookDto> bookDtos = allBooks.Adapt<List<BookDto>>();
        return Result.Success(bookDtos);
    }

    public async Task<Result<BookDto>> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var book = await unitOfWork.Books.FindByIdAsync(id, cancellationToken);
        if (book is null)
        {
            return Result.Failure<BookDto>(Error.NotFound("Book.NotFound", $"Id={id} olan kitap bulunamadı."));

        }
        return Result.Success(book.Adapt<BookDto>());
    }

    public async Task<Result<BookDto>> CreateAsync(CreateBookDto dto, CancellationToken cancellationToken = default)
    {
        var validationResult = await createValidator.ValidateAsync(dto, cancellationToken);
        if (!validationResult.IsValid)
        {
            var message = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));
            return Result.Failure<BookDto>(Error.Validation("Book.ValidationFailed", message));
        }

        var book = dto.Adapt<Book>();

        await unitOfWork.Books.AddAsync(book, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(book.Adapt<BookDto>());
    }

    public async Task<Result<BookDto>> UpdateAsync(int id, UpdateBookDto dto, CancellationToken cancellationToken = default)
    {
        var validationResult = await updateValidator.ValidateAsync(dto, cancellationToken);
        if (!validationResult.IsValid)
        {
            var message = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));
            return Result.Failure<BookDto>(Error.Validation("Book.ValidationFailed", message));
        }

        var book = await unitOfWork.Books.FindByIdAsync(id, cancellationToken);
        if (book is null)
        {
            return Result.Failure<BookDto>(Error.NotFound("Book.NotFound", $"Id={id} olan kitap güncellenemedi."));
        }
        ;
        dto.Adapt(book);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(book.Adapt<BookDto>());
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var book = await unitOfWork.Books.FindByIdAsync(id, cancellationToken);

        if (book is null)
        {
            return Result.Failure(Error.NotFound("Book.NotFound", $"Id={id} olan kitap bulunamadı."));

        }
        unitOfWork.Books.Remove(book);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
