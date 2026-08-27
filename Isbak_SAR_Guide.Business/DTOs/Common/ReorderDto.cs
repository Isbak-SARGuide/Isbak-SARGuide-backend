namespace Isbak_SAR_Guide.Business.DTOs.Common;

/// <summary>
/// Module/Content/ContentBlock reorder uclarinin ortak istek govdesi. Liste
/// sirasi = istenen DisplayOrder sirasi (0-based); tum kardes id'leri icermeli,
/// eksik/fazla/tekrar servis tarafinda Validation hatasi olarak reddedilir.
/// </summary>
public sealed record ReorderDto(IReadOnlyList<int> OrderedIds);
