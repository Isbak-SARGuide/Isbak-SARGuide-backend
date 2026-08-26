using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Isbak_SAR_Guide.DataAccess.Common;

/// <summary>
/// Provider'a ozgu hata tespiti. Business katmani PostgresException tipini
/// tanimamali (katman sizintisi) - "bu bir unique ihlali mi?" sorusunun
/// cevabi burada, provider bilgisinin zaten yasadigi katmanda verilir.
/// </summary>
public static class DbErrors
{
    /// <summary>
    /// Exception zincirinin dibinde SqlState 23505 (unique_violation) var mi?
    /// Ornek: iki eszamanli publish ayni (BookId, Version)'i yazmaya
    /// kalktiginda kaybeden taraf bu hatayla duser.
    /// </summary>
    public static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.GetBaseException() is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
        };
}
