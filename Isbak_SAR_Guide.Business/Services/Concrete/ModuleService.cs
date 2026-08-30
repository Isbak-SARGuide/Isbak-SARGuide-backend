using FluentValidation;
using Isbak_SAR_Guide.Business.Common;
using Isbak_SAR_Guide.Business.DTOs.Common;
using Isbak_SAR_Guide.Business.DTOs.Modules;
using Isbak_SAR_Guide.Business.Services.Abstract;
using Isbak_SAR_Guide.DataAccess.Repositories.Abstract;
using Isbak_SAR_Guide.Entities.Content;
using Mapster;
using Microsoft.Extensions.Logging;

namespace Isbak_SAR_Guide.Business.Services.Concrete;

public class ModuleService(
    IUnitOfWork unitOfWork,
    IValidator<CreateModuleDto> createValidator,
    IValidator<UpdateModuleDto> updateValidator,
    IValidator<ReorderDto> reorderValidator,
    ILogger<ModuleService> logger) : IModuleService
{
    public async Task<Result<PagedResult<ModuleDto>>> GetPagedAsync(
        int bookId, int page, int pageSize, bool? isPublished, CancellationToken cancellationToken = default)
    {
        var book = await unitOfWork.Books.FindByIdAsync(bookId, cancellationToken);
        if (book is null)
        {
            return Result.Failure<PagedResult<ModuleDto>>(Error.NotFound("Book.NotFound", $"Id={bookId} olan kitap bulunamadı."));
        }

        var (items, totalCount) = await unitOfWork.Modules.GetPagedAsync(bookId, page, pageSize, isPublished, cancellationToken);
        var dtos = items.Select(x => x.Module.Adapt<ModuleDto>() with { ContentCount = x.ContentCount }).ToList();
        var pagedResult = new PagedResult<ModuleDto>(dtos, totalCount, page, pageSize);
        return Result.Success(pagedResult);
    }

    public async Task<Result<ModuleDto>> GetByIdAsync(int bookId, int id, CancellationToken cancellationToken = default)
    {
        var module = await unitOfWork.Modules.FindByIdAsync(id, cancellationToken);
        if (module is null || module.BookId != bookId)
        {
            return Result.Failure<ModuleDto>(Error.NotFound("Module.NotFound", $"Id={id} olan modül bulunamadı."));
        }

        return Result.Success(module.Adapt<ModuleDto>());
    }

    public async Task<Result<ModuleDto>> CreateAsync(int bookId, CreateModuleDto dto, CancellationToken cancellationToken = default)
    {
        var validationResult = await createValidator.ValidateAsync(dto, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result.Failure<ModuleDto>(validationResult.ToValidationError("Module.ValidationFailed"));
        }

        var book = await unitOfWork.Books.FindByIdAsync(bookId, cancellationToken);
        if (book is null)
        {
            return Result.Failure<ModuleDto>(Error.NotFound("Book.NotFound", $"Id={bookId} olan kitap bulunamadı."));
        }

        var siblings = await unitOfWork.Modules.FindAllByBookIdAsync(bookId, cancellationToken);
        var nextDisplayOrder = siblings.Count == 0 ? 0 : siblings.Max(m => m.DisplayOrder) + 1;

        var module = dto.Adapt<Module>();
        module.BookId = bookId;
        module.DisplayOrder = nextDisplayOrder;

        await unitOfWork.Modules.AddAsync(module, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(module.Adapt<ModuleDto>());
    }

    public async Task<Result<ModuleDto>> UpdateAsync(int bookId, int id, UpdateModuleDto dto, CancellationToken cancellationToken = default)
    {
        var validationResult = await updateValidator.ValidateAsync(dto, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result.Failure<ModuleDto>(validationResult.ToValidationError("Module.ValidationFailed"));
        }

        var module = await unitOfWork.Modules.FindByIdAsync(id, cancellationToken);
        if (module is null || module.BookId != bookId)
        {
            return Result.Failure<ModuleDto>(Error.NotFound("Module.NotFound", $"Id={id} olan modül güncellenemedi."));
        }

        dto.Adapt(module);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(module.Adapt<ModuleDto>());
    }

    public async Task<Result> DeleteAsync(int bookId, int id, CancellationToken cancellationToken = default)
    {
        var module = await unitOfWork.Modules.FindByIdAsync(id, cancellationToken);
        if (module is null || module.BookId != bookId)
        {
            return Result.Failure(Error.NotFound("Module.NotFound", $"Id={id} olan modül bulunamadı."));
        }

        unitOfWork.Modules.Remove(module);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result> ReorderAsync(int bookId, ReorderDto dto, CancellationToken cancellationToken = default)
    {
        var validationResult = await reorderValidator.ValidateAsync(dto, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result.Failure(validationResult.ToValidationError("Module.ReorderValidationFailed"));
        }

        var book = await unitOfWork.Books.FindByIdAsync(bookId, cancellationToken);
        if (book is null)
        {
            return Result.Failure(Error.NotFound("Book.NotFound", $"Id={bookId} olan kitap bulunamadı."));
        }

        var siblings = await unitOfWork.Modules.FindAllByBookIdAsync(bookId, cancellationToken);

        return await ReorderHelper.ApplyAsync(
            unitOfWork,
            logger,
            siblings,
            dto.OrderedIds,
            getId: m => m.Id,
            setDisplayOrder: (m, order) => m.DisplayOrder = order,
            markDirty: unitOfWork.Modules.Update,
            mismatchError: Error.Validation("Module.ReorderMismatch", "OrderedIds, kitabın mevcut modül kümesiyle birebir eşleşmeli."),
            conflictError: Error.Conflict("Module.ReorderConflict", "Aynı anda başka bir sıralama işlemi yapıldı, lütfen tekrar deneyin."),
            cancellationToken);
    }
}
