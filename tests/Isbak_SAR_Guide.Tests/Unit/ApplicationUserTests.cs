namespace Isbak_SAR_Guide.Tests.Unit;

using Isbak_SAR_Guide.Entities.Identity;
using Shouldly;

public class ApplicationUserTests
{
    [Fact]
    public void InheritsEmailFromIdentityUser()
    {
        // Arrange — Email'i ApplicationUser icinde HIC tanimlamadik,
        // IdentityUser'dan miras geliyor.
        var user = new ApplicationUser
        {
            UserName = "eren",
            Email = "eren@ornek.com",
            FullName = "Eren Atasoy"
        };

        // Act
        var email = user.Email;

        // Assert
        email.ShouldBe("eren@ornek.com");
    }

    [Fact]
    public void InheritsLockoutFieldsFromIdentityUser()
    {
        // Arrange — hesap kilitleme alanlari da hazir geliyor.
        // Bu yuzden ayrica IsActive alani EKLEMEDIK.
        var user = new ApplicationUser { UserName = "eren", FullName = "Eren Atasoy" };

        // Act
        user.LockoutEnd = new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);

        // Assert
        user.LockoutEnd.ShouldNotBeNull();
        user.AccessFailedCount.ShouldBe(0);
    }
}
