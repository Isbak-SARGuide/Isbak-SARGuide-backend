using Isbak_SAR_Guide.API.Common;
using Shouldly;

namespace Isbak_SAR_Guide.Tests.Unit;

/// <summary>
/// Backend-Yapilacaklar.md #2: pageSize'a sunucu tarafinda ust sinir yoktu -
/// kotu niyetli/buggy bir istemci pageSize=100000 gonderirse sunucuyu
/// gereksiz buyuk bir sorguya zorlayabilirdi. Bu testler sadece klips
/// mantigini (saf fonksiyon) dogrular; HTTP seviyesinde yansimasi
/// UsersControllerTests.GetAll_OversizedPageSize_IsClampedToMax'ta.
/// </summary>
public class PagingDefaultsTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(3, 3)]
    public void NormalizePage_ReturnsExpected(int input, int expected) =>
        PagingDefaults.NormalizePage(input).ShouldBe(expected);

    [Theory]
    [InlineData(0, 50)]
    [InlineData(-1, 50)]
    [InlineData(10, 10)]
    [InlineData(200, 200)]
    [InlineData(201, 200)]
    [InlineData(100_000, 200)]
    public void NormalizePageSize_ReturnsExpected(int input, int expected) =>
        PagingDefaults.NormalizePageSize(input).ShouldBe(expected);
}
