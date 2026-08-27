using FluentValidation;
using Isbak_SAR_Guide.Business.Common;
using Isbak_SAR_Guide.Business.DTOs.Common;
using Isbak_SAR_Guide.Business.DTOs.ContentBlocks;
using Isbak_SAR_Guide.Business.Services.Abstract;
using Isbak_SAR_Guide.DataAccess.Repositories.Abstract;
using Isbak_SAR_Guide.Entities.Content;
using Mapster;

namespace Isbak_SAR_Guide.Business.Services.Concrete;

public class ContentBlockService(
    IUnitOfWork unitOfWork,
    IValidator<CreateContentBlockDto> createValidator,
    IValidator<UpdateContentBlockDto> updateValidator,
    IValidator<ReorderDto> reorderValidator) : IContentBlockService
{
    public async Task<Result<PagedResult<ContentBlockDto>>> GetPagedAsync(
        int contentId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var content = await unitOfWork.Contents.FindByIdAsync(contentId, cancellationToken);
        if (content is null)
        {
            return Result.Failure<PagedResult<ContentBlockDto>>(Error.NotFound("Content.NotFound", $"Id={contentId} olan içerik bulunamadı."));
        }

        var (items, totalCount) = await unitOfWork.ContentBlocks.GetPagedAsync(contentId, page, pageSize, cancellationToken);
        var pagedResult = new PagedResult<ContentBlockDto>(items.Adapt<List<ContentBlockDto>>(), totalCount, page, pageSize);
        return Result.Success(pagedResult);
    }

    public async Task<Result<ContentBlockDto>> GetByIdAsync(int contentId, int id, CancellationToken cancellationToken = default)
    {
        var block = await unitOfWork.ContentBlocks.FindByIdAsync(id, cancellationToken);
        if (block is null || block.ContentId != contentId)
        {
            return Result.Failure<ContentBlockDto>(Error.NotFound("ContentBlock.NotFound", $"Id={id} olan blok bulunamadı."));
        }

        return Result.Success(block.Adapt<ContentBlockDto>());
    }

    public async Task<Result<ContentBlockDto>> CreateAsync(int contentId, CreateContentBlockDto dto, CancellationToken cancellationToken = default)
    {
        var validationResult = await createValidator.ValidateAsync(dto, cancellationToken);
        if (!validationResult.IsValid)
        {
            var message = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));
            return Result.Failure<ContentBlockDto>(Error.Validation("ContentBlock.ValidationFailed", message));
        }

        var content = await unitOfWork.Contents.FindByIdAsync(contentId, cancellationToken);
        if (content is null)
        {
            return Result.Failure<ContentBlockDto>(Error.NotFound("Content.NotFound", $"Id={contentId} olan içerik bulunamadı."));
        }

        if (dto.MediaId is not null)
        {
            var media = await unitOfWork.Media.FindByIdAsync(dto.MediaId.Value, cancellationToken);
            if (media is null)
            {
                return Result.Failure<ContentBlockDto>(Error.Validation("ContentBlock.MediaNotFound", $"Id={dto.MediaId} olan medya bulunamadı."));
            }
        }

        var siblings = await unitOfWork.ContentBlocks.FindAllByContentIdAsync(contentId, cancellationToken);
        var nextDisplayOrder = siblings.Count == 0 ? 0 : siblings.Max(b => b.DisplayOrder) + 1;

        var block = dto.Adapt<ContentBlock>();
        block.ContentId = contentId;
        block.DisplayOrder = nextDisplayOrder;

        await unitOfWork.ContentBlocks.AddAsync(block, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(block.Adapt<ContentBlockDto>());
    }

    public async Task<Result<ContentBlockDto>> UpdateAsync(int contentId, int id, UpdateContentBlockDto dto, CancellationToken cancellationToken = default)
    {
        var validationResult = await updateValidator.ValidateAsync(dto, cancellationToken);
        if (!validationResult.IsValid)
        {
            var message = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));
            return Result.Failure<ContentBlockDto>(Error.Validation("ContentBlock.ValidationFailed", message));
        }

        var block = await unitOfWork.ContentBlocks.FindByIdAsync(id, cancellationToken);
        if (block is null || block.ContentId != contentId)
        {
            return Result.Failure<ContentBlockDto>(Error.NotFound("ContentBlock.NotFound", $"Id={id} olan blok güncellenemedi."));
        }

        if (dto.MediaId is not null)
        {
            var media = await unitOfWork.Media.FindByIdAsync(dto.MediaId.Value, cancellationToken);
            if (media is null)
            {
                return Result.Failure<ContentBlockDto>(Error.Validation("ContentBlock.MediaNotFound", $"Id={dto.MediaId} olan medya bulunamadı."));
            }
        }

        dto.Adapt(block);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(block.Adapt<ContentBlockDto>());
    }

    public async Task<Result> DeleteAsync(int contentId, int id, CancellationToken cancellationToken = default)
    {
        var block = await unitOfWork.ContentBlocks.FindByIdAsync(id, cancellationToken);
        if (block is null || block.ContentId != contentId)
        {
            return Result.Failure(Error.NotFound("ContentBlock.NotFound", $"Id={id} olan blok bulunamadı."));
        }

        unitOfWork.ContentBlocks.Remove(block);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result> ReorderAsync(int contentId, ReorderDto dto, CancellationToken cancellationToken = default)
    {
        var validationResult = await reorderValidator.ValidateAsync(dto, cancellationToken);
        if (!validationResult.IsValid)
        {
            var message = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));
            return Result.Failure(Error.Validation("ContentBlock.ReorderValidationFailed", message));
        }

        var content = await unitOfWork.Contents.FindByIdAsync(contentId, cancellationToken);
        if (content is null)
        {
            return Result.Failure(Error.NotFound("Content.NotFound", $"Id={contentId} olan içerik bulunamadı."));
        }

        var siblings = await unitOfWork.ContentBlocks.FindAllByContentIdAsync(contentId, cancellationToken);

        return await ReorderHelper.ApplyAsync(
            unitOfWork,
            siblings,
            dto.OrderedIds,
            getId: b => b.Id,
            setDisplayOrder: (b, order) => b.DisplayOrder = order,
            markDirty: unitOfWork.ContentBlocks.Update,
            mismatchError: Error.Validation("ContentBlock.ReorderMismatch", "OrderedIds, içeriğin mevcut blok kümesiyle birebir eşleşmeli."),
            conflictError: Error.Conflict("ContentBlock.ReorderConflict", "Aynı anda başka bir sıralama işlemi yapıldı, lütfen tekrar deneyin."),
            cancellationToken);
    }
}
