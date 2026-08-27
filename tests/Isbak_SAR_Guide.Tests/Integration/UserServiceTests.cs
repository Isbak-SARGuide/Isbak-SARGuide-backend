using Isbak_SAR_Guide.Business.Common;
using Isbak_SAR_Guide.Business.DTOs.Users;
using Isbak_SAR_Guide.Business.Services.Abstract;
using Isbak_SAR_Guide.Entities.Identity;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Isbak_SAR_Guide.Tests.Integration;

/// <summary>Faz 9.2: Admin'in Editor hesabi acmasi (kayit/self sign-up yok, bilerek).</summary>
[Collection("Api")]
public class UserServiceTests(ApiFactory factory)
{
    [Fact]
    public async Task CreateAsync_WithValidEditorDto_CreatesUserWithEditorRole()
    {
        var dto = new CreateUserDto($"editor-{Guid.NewGuid():N}", "Editor!2026Pass", "Yeni Editör", RoleNames.Editor);

        var result = await CreateAsync(dto);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Roles.ShouldContain(RoleNames.Editor);
        result.Value.UserName.ShouldBe(dto.UserName);
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateUserName_ReturnsConflict()
    {
        var dto = new CreateUserDto($"dup-{Guid.NewGuid():N}", "Editor!2026Pass", "Kullanıcı", RoleNames.Editor);
        (await CreateAsync(dto)).IsSuccess.ShouldBeTrue();

        var result = await CreateAsync(dto);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.Conflict);
    }

    [Fact]
    public async Task CreateAsync_WithInvalidRole_ReturnsValidationError()
    {
        var dto = new CreateUserDto($"badrole-{Guid.NewGuid():N}", "Editor!2026Pass", "Kullanıcı", "SuperAdmin");

        var result = await CreateAsync(dto);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.Validation);
    }

    [Fact]
    public async Task CreateAsync_WithWeakPassword_ReturnsValidationError()
    {
        // Identity'nin varsayilan sifre politikasi (rakam/buyuk harf/uzunluk
        // vb.) karsisinda gecersiz - UserManager.CreateAsync'in IdentityResult
        // hatalari Error.Validation'a cevriliyor mu diye kontrol eder.
        var dto = new CreateUserDto($"weakpass-{Guid.NewGuid():N}", "abc", "Kullanıcı", RoleNames.Editor);

        var result = await CreateAsync(dto);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.Validation);
    }

    private async Task<Result<UserDto>> CreateAsync(CreateUserDto dto)
    {
        using var scope = factory.Services.CreateScope();
        var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
        return await userService.CreateAsync(dto);
    }
}
