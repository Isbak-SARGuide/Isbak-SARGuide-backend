namespace Isbak_SAR_Guide.API.Common;

/// <summary>
/// Tum PagedResult&lt;T&gt; liste uclarinin (Modules/Contents/ContentBlocks/Users)
/// ortak page/pageSize normalizasyonu. Onceden her controller kendi
/// "page &lt;= 0 ? 1 : page" satirini tekrarliyordu ve pageSize icin ust
/// sinir HIC yoktu - kotu niyetli/buggy bir istemci pageSize=100000 gibi
/// bir deger gonderirse sunucuyu gereksiz buyuk bir sorguya zorlayabilirdi
/// (Backend-Yapilacaklar.md #2). Reddetmek yerine KIRPMA tercih edildi -
/// page&lt;=0 icin zaten var olan "sessizce normalize et" davranisiyla tutarli.
/// </summary>
public static class PagingDefaults
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 200;

    public static int NormalizePage(int page) => page <= 0 ? 1 : page;

    public static int NormalizePageSize(int pageSize) =>
        pageSize <= 0 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);
}
