namespace Isbak_SAR_Guide.Business.Common;

/// <summary>
/// Faz 5 CMS liste uclarinin ortak sayfalama zarfi (Module/Content/ContentBlock).
/// TotalCount, istemcinin toplam sayfa sayisini hesaplayabilmesi icin var.
/// </summary>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize);
