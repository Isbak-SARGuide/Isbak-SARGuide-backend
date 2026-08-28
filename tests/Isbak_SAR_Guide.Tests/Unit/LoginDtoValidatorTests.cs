using Isbak_SAR_Guide.Business.DTOs.Auth;
using Isbak_SAR_Guide.Business.Validation.Auth;
using Shouldly;

namespace Isbak_SAR_Guide.Tests.Unit;

/// <summary>
/// Faz 12.5: hicbir yerde dogrudan test edilmiyordu (AuthTests.cs sadece
/// gecerli/kotu-kimlik-bilgili girişleri test ediyor, boş alanla giriş hic
/// denenmemişti). Kasitli olarak sadece "boş mu" kontrolu var - sifre
/// karmaşikligi kurali YOK (validator'daki yorumun kendi gerekcesi).
/// </summary>
public class LoginDtoValidatorTests
{
    private readonly LoginDtoValidator _validator = new();

    [Fact]
    public void Validate_WithValidCredentials_IsValid()
    {
        // Arrange
        var dto = new LoginDto("admin", "Admin!Dev123");

        // Act
        var result = _validator.Validate(dto);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WithEmptyUserName_IsInvalid()
    {
        // Arrange
        var dto = new LoginDto("", "Admin!Dev123");

        // Act
        var result = _validator.Validate(dto);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(LoginDto.UserName));
    }

    [Fact]
    public void Validate_WithEmptyPassword_IsInvalid()
    {
        // Arrange
        var dto = new LoginDto("admin", "");

        // Act
        var result = _validator.Validate(dto);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(LoginDto.Password));
    }

    [Fact]
    public void Validate_WithSimplePassword_IsValid()
    {
        // Arrange - bilerek: sifre karmasikligi kurali yok, "1" gibi zayif bir
        // sifre bile DTO seviyesinde reddedilmemeli (validator'in kendi
        // gerekcesi - politika sizintisini/geriye donuk kilitlenmeyi onlemek).
        var dto = new LoginDto("admin", "1");

        // Act
        var result = _validator.Validate(dto);

        // Assert
        result.IsValid.ShouldBeTrue();
    }
}
