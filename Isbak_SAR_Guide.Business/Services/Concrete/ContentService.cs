using FluentValidation;
using Isbak_SAR_Guide.Business.Common;
using Isbak_SAR_Guide.Business.DTOs.Common;
using Isbak_SAR_Guide.Business.DTOs.Contents;
using Isbak_SAR_Guide.Business.Services.Abstract;
using Isbak_SAR_Guide.DataAccess.Repositories.Abstract;
using Isbak_SAR_Guide.Entities.Content;
using Mapster;
using Microsoft.Extensions.Logging;

namespace Isbak_SAR_Guide.Business.Services.Concrete;

public class ContentService(
    IUnitOfWork unitOfWork,
    IValidator<CreateContentDto> createValidator,
    IValidator<UpdateContentDto> updateValidator,
    IValidator<ReorderDto> reorderValidator,
    ILogger<ContentService> logger) : IContentService
{
    public async Task<Result<PagedResult<ContentDto>>> GetPagedAsync(
        int moduleId, int page, int pageSize, bool? isPublished, CancellationToken cancellationToken = default)
    {
        var module = await unitOfWork.Modules.FindByIdAsync(moduleId, cancellationToken);
        if (module is null)
        {
            return Result.Failure<PagedResult<ContentDto>>(Error.NotFound("Module.NotFound", $"Id={moduleId} olan modül bulunamadı."));
        }

        var (items, totalCount) = await unitOfWork.Contents.GetPagedAsync(moduleId, page, pageSize, isPublished, cancellationToken);
        var pagedResult = new PagedResult<ContentDto>(items.Adapt<List<ContentDto>>(), totalCount, page, pageSize);
        return Result.Success(pagedResult);
    }

    public async Task<Result<PagedResult<ContentDto>>> GetPagedByBookIdAsync(
        int bookId, int page, int pageSize, bool? isPublished, CancellationToken cancellationToken = default)
    {
        var book = await unitOfWork.Books.FindByIdAsync(bookId, cancellationToken);
        if (book is null)
        {
            return Result.Failure<PagedResult<ContentDto>>(Error.NotFound("Book.NotFound", $"Id={bookId} olan kitap bulunamadı."));
        }

        var (items, totalCount) = await unitOfWork.Contents.GetPagedByBookIdAsync(bookId, page, pageSize, isPublished, cancellationToken);
        var pagedResult = new PagedResult<ContentDto>(items.Adapt<List<ContentDto>>(), totalCount, page, pageSize);
        return Result.Success(pagedResult);
    }

    public async Task<Result<ContentDto>> GetByIdAsync(int moduleId, int id, CancellationToken cancellationToken = default)
    {
        var content = await unitOfWork.Contents.FindByIdAsync(id, cancellationToken);
        if (content is null || content.ModuleId != moduleId)
        {
            return Result.Failure<ContentDto>(Error.NotFound("Content.NotFound", $"Id={id} olan içerik bulunamadı."));
        }

        return Result.Success(content.Adapt<ContentDto>());
    }

    public async Task<Result<ContentDto>> CreateAsync(int moduleId, CreateContentDto dto, CancellationToken cancellationToken = default)
    {
        var validationResult = await createValidator.ValidateAsync(dto, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result.Failure<ContentDto>(validationResult.ToValidationError("Content.ValidationFailed"));
        }

        var module = await unitOfWork.Modules.FindByIdAsync(moduleId, cancellationToken);
        if (module is null)
        {
            return Result.Failure<ContentDto>(Error.NotFound("Module.NotFound", $"Id={moduleId} olan modül bulunamadı."));
        }

        var siblings = await unitOfWork.Contents.FindAllByModuleIdAsync(moduleId, cancellationToken);
        var nextDisplayOrder = siblings.Count == 0 ? 0 : siblings.Max(c => c.DisplayOrder) + 1;

        var content = dto.Adapt<Content>();
        content.ModuleId = moduleId;
        content.DisplayOrder = nextDisplayOrder;

        await unitOfWork.Contents.AddAsync(content, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(content.Adapt<ContentDto>());
    }

    public async Task<Result<ContentDto>> UpdateAsync(int moduleId, int id, UpdateContentDto dto, CancellationToken cancellationToken = default)
    {
        var validationResult = await updateValidator.ValidateAsync(dto, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result.Failure<ContentDto>(validationResult.ToValidationError("Content.ValidationFailed"));
        }

        var content = await unitOfWork.Contents.FindByIdAsync(id, cancellationToken);
        if (content is null || content.ModuleId != moduleId)
        {
            return Result.Failure<ContentDto>(Error.NotFound("Content.NotFound", $"Id={id} olan içerik güncellenemedi."));
        }

        dto.Adapt(content);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(content.Adapt<ContentDto>());
    }

    public async Task<Result> DeleteAsync(int moduleId, int id, CancellationToken cancellationToken = default)
    {
        var content = await unitOfWork.Contents.FindByIdAsync(id, cancellationToken);
        if (content is null || content.ModuleId != moduleId)
        {
            return Result.Failure(Error.NotFound("Content.NotFound", $"Id={id} olan içerik bulunamadı."));
        }

        unitOfWork.Contents.Remove(content);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var remaining = await unitOfWork.Contents.FindAllByModuleIdAsync(moduleId, cancellationToken);
        return await ReorderHelper.CompactAsync(
            unitOfWork,
            logger,
            remaining,
            getId: c => c.Id,
            setDisplayOrder: (c, order) => c.DisplayOrder = order,
            markDirty: c => unitOfWork.Contents.UpdateProperty(c, x => x.DisplayOrder),
            conflictError: Error.Conflict("Content.ReorderConflict", "Aynı anda başka bir sıralama işlemi yapıldı, lütfen tekrar deneyin."),
            cancellationToken);
    }

    public async Task<Result> ReorderAsync(int moduleId, ReorderDto dto, CancellationToken cancellationToken = default)
    {
        var validationResult = await reorderValidator.ValidateAsync(dto, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result.Failure(validationResult.ToValidationError("Content.ReorderValidationFailed"));
        }

        var module = await unitOfWork.Modules.FindByIdAsync(moduleId, cancellationToken);
        if (module is null)
        {
            return Result.Failure(Error.NotFound("Module.NotFound", $"Id={moduleId} olan modül bulunamadı."));
        }

        var siblings = await unitOfWork.Contents.FindAllByModuleIdAsync(moduleId, cancellationToken);

        return await ReorderHelper.ApplyAsync(
            unitOfWork,
            logger,
            siblings,
            dto.OrderedIds,
            getId: c => c.Id,
            setDisplayOrder: (c, order) => c.DisplayOrder = order,
            markDirty: c => unitOfWork.Contents.UpdateProperty(c, x => x.DisplayOrder),
            mismatchError: Error.Validation("Content.ReorderMismatch", "OrderedIds, modülün mevcut içerik kümesiyle birebir eşleşmeli."),
            conflictError: Error.Conflict("Content.ReorderConflict", "Aynı anda başka bir sıralama işlemi yapıldı, lütfen tekrar deneyin."),
            cancellationToken);
    }
}
