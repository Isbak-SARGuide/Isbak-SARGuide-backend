using Isbak_SAR_Guide.Business.Common;
using Isbak_SAR_Guide.Business.DTOs.Users;
using Isbak_SAR_Guide.Business.Services.Abstract;
using Isbak_SAR_Guide.Entities.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Isbak_SAR_Guide.Tests.Integration;

/// <summary>
/// Faz 9.2: Admin'in Editor hesabi acmasi (kayit/self sign-up yok, bilerek).
/// Faz 13.6: liste, rol degistirme, pasiflestirme (self-lockout guard dahil)
/// ve kendi sifresini degistirme.
/// </summary>
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

    [Fact]
    public async Task GetAllAsync_ReturnsCreatedUserWithRoleAndLockoutStatus()
    {
        var dto = new CreateUserDto($"list-{Guid.NewGuid():N}", "Editor!2026Pass", "Listelenecek Kullanıcı", RoleNames.Editor);
        var created = await CreateAsync(dto);

        using var scope = factory.Services.CreateScope();
        var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
        var result = await userService.GetAllAsync(page: 1, pageSize: 500);

        result.IsSuccess.ShouldBeTrue();
        var listed = result.Value.Items.Single(u => u.Id == created.Value.Id);
        listed.Roles.ShouldContain(RoleNames.Editor);
        listed.IsLockedOut.ShouldBeFalse();
    }

    [Fact]
    public async Task ChangeRoleAsync_WithValidRole_ReplacesExistingRole()
    {
        var dto = new CreateUserDto($"role-{Guid.NewGuid():N}", "Editor!2026Pass", "Rol Değişecek", RoleNames.Editor);
        var created = await CreateAsync(dto);

        using var scope = factory.Services.CreateScope();
        var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
        var result = await userService.ChangeRoleAsync(created.Value.Id, new ChangeRoleDto(RoleNames.Admin));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Roles.ShouldBe([RoleNames.Admin]);
    }

    [Fact]
    public async Task ChangeRoleAsync_WithNonExistentUser_ReturnsNotFound()
    {
        using var scope = factory.Services.CreateScope();
        var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
        var result = await userService.ChangeRoleAsync(Guid.NewGuid().ToString(), new ChangeRoleDto(RoleNames.Admin));

        result.IsFailure.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public async Task DeactivateAsync_ByAnotherAdmin_LocksOutTargetUser()
    {
        var dto = new CreateUserDto($"deact-{Guid.NewGuid():N}", "Editor!2026Pass", "Pasifleşecek", RoleNames.Editor);
        var created = await CreateAsync(dto);

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var admin = await userManager.FindByNameAsync("admin");

        var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
        var result = await userService.DeactivateAsync(created.Value.Id, actingUserId: admin!.Id);

        result.IsSuccess.ShouldBeTrue();
        var target = await userManager.FindByIdAsync(created.Value.Id);
        (await userManager.IsLockedOutAsync(target!)).ShouldBeTrue();
    }

    [Fact]
    public async Task DeactivateAsync_OwnAccount_ReturnsValidationError()
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var admin = await userManager.FindByNameAsync("admin");

        var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
        var result = await userService.DeactivateAsync(admin!.Id, actingUserId: admin.Id);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.Validation);
        (await userManager.IsLockedOutAsync(admin)).ShouldBeFalse();
    }

    [Fact]
    public async Task ChangeOwnPasswordAsync_WithCorrectCurrentPassword_Succeeds()
    {
        const string oldPassword = "Editor!2026Pass";
        const string newPassword = "Editor!2027PassNew";
        var dto = new CreateUserDto($"pwd-{Guid.NewGuid():N}", oldPassword, "Şifre Değişecek", RoleNames.Editor);
        var created = await CreateAsync(dto);

        using var scope = factory.Services.CreateScope();
        var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
        var result = await userService.ChangeOwnPasswordAsync(created.Value.Id, new ChangePasswordDto(oldPassword, newPassword));

        result.IsSuccess.ShouldBeTrue();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByIdAsync(created.Value.Id);
        (await userManager.CheckPasswordAsync(user!, newPassword)).ShouldBeTrue();
    }

    [Fact]
    public async Task ChangeOwnPasswordAsync_WithWrongCurrentPassword_ReturnsValidationError()
    {
        var dto = new CreateUserDto($"pwdwrong-{Guid.NewGuid():N}", "Editor!2026Pass", "Şifre", RoleNames.Editor);
        var created = await CreateAsync(dto);

        using var scope = factory.Services.CreateScope();
        var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
        var result = await userService.ChangeOwnPasswordAsync(created.Value.Id, new ChangePasswordDto("YanlisSifre!1", "Editor!2027PassNew"));

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
