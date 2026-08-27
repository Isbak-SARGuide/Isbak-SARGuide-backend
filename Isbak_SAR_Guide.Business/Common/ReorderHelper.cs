using Isbak_SAR_Guide.DataAccess.Common;
using Isbak_SAR_Guide.DataAccess.Repositories.Abstract;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Isbak_SAR_Guide.Business.Common;

/// <summary>
/// Module/Content/ContentBlock reorder'inin ortak iki fazli mantigi.
/// Module ve Content'te (ParentId, DisplayOrder) unique+partial index var
/// (bkz. ModuleConfiguration/ContentConfiguration) - tek adimda "B'yi C'nin
/// yerine koy" gecici bir cakismaya (23505) takilir. Bu yuzden once tum
/// kardesler gecici negatif DisplayOrder'a tasinir, sonra final degerlere
/// yazilir; ikisi de tek transaction icinde. ContentBlock'ta unique index yok
/// ama ayni kod yolu zararsiz - uc serviste kopyala-yapistir yerine burada tek yer.
/// </summary>
internal static class ReorderHelper
{
    public static async Task<Result> ApplyAsync<T>(
        IUnitOfWork unitOfWork,
        ILogger logger,
        IReadOnlyList<T> siblings,
        IReadOnlyList<int> orderedIds,
        Func<T, int> getId,
        Action<T, int> setDisplayOrder,
        Action<T> markDirty,
        Error mismatchError,
        Error conflictError,
        CancellationToken cancellationToken)
    {
        var siblingIds = siblings.Select(getId).ToHashSet();
        var requestedIds = orderedIds.ToHashSet();

        // Set esitligi + sayim: eksik id (bir kayit pozisyonsuz kalir), fazla id
        // (baska aileden/var olmayan bir id) veya tekrar (Count farki) hepsi
        // burada tek kontrolle yakalanir.
        if (requestedIds.Count != orderedIds.Count || !siblingIds.SetEquals(requestedIds))
        {
            return Result.Failure(mismatchError);
        }

        var byId = siblings.ToDictionary(getId);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            for (var i = 0; i < orderedIds.Count; i++)
            {
                var entity = byId[orderedIds[i]];
                setDisplayOrder(entity, -(i + 1));
                markDirty(entity);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);

            for (var i = 0; i < orderedIds.Count; i++)
            {
                var entity = byId[orderedIds[i]];
                setDisplayOrder(entity, i);
                markDirty(entity);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result.Success();
        }
        catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex))
        {
            logger.LogInformation(ex, "Eszamanli reorder yarisi - {ConflictCode}.", conflictError.Code);
            return Result.Failure(conflictError);
        }
    }
}
