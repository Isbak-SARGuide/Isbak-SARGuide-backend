using Isbak_SAR_Guide.DataAccess.Common;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shouldly;

namespace Isbak_SAR_Guide.Tests.Unit;

/// <summary>
/// Conflict cevirisinin zincirindeki tek "bizim kod" halkasi: unique index
/// DB garantisi, when filtresi uc satir wiring - deterministik unit teste
/// deger olan, SqlState ayrimini yapan bu metod. Gercek eszamanlilik provasi
/// bilerek yok (flaky olurdu); istenirse yeri Faz 9 hardening.
/// </summary>
public class DbErrorsTests
{
    [Fact]
    public void IsUniqueViolation_PostgresUniqueViolation_ReturnsTrue()
    {
        // Arrange
        var exception = new DbUpdateException(
            "duplicate key",
            new PostgresException("duplicate key value violates unique constraint", "ERROR", "ERROR", "23505"));

        // Act
        var isUniqueViolation = DbErrors.IsUniqueViolation(exception);

        // Assert
        isUniqueViolation.ShouldBeTrue();
    }

    [Fact]
    public void IsUniqueViolation_ForeignKeyViolation_ReturnsFalse()
    {
        // Arrange - 23503: FK ihlali, unique degil
        var exception = new DbUpdateException(
            "fk violation",
            new PostgresException("violates foreign key constraint", "ERROR", "ERROR", "23503"));

        // Act
        var isUniqueViolation = DbErrors.IsUniqueViolation(exception);

        // Assert
        isUniqueViolation.ShouldBeFalse();
    }

    [Fact]
    public void IsUniqueViolation_NoInnerException_ReturnsFalse()
    {
        // Arrange
        var exception = new DbUpdateException("bare failure");

        // Act
        var isUniqueViolation = DbErrors.IsUniqueViolation(exception);

        // Assert
        isUniqueViolation.ShouldBeFalse();
    }
}
