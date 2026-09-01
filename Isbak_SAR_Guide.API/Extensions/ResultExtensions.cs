using System.Text.Json;
using Isbak_SAR_Guide.Business.Common;
using Microsoft.AspNetCore.Mvc;

namespace Isbak_SAR_Guide.API.Extensions;

/// <summary>
/// Business katmaninin dondurdugu Result/Result&lt;T&gt; degerlerini HTTP
/// yanitina cevirir. Error.Type -> HTTP durum kodu eslemesi burada tek
/// yerde toplanir, her controller'da tekrar etmez.
/// </summary>
public static class ResultExtensions
{
    public static IActionResult ToActionResult<T>(this Result<T> result, ControllerBase controller) =>
        result.IsSuccess
            ? controller.Ok(result.Value)
            : CreateProblemResult(result.Error!, controller);

    public static IActionResult ToActionResult(this Result result, ControllerBase controller) =>
        result.IsSuccess
            ? controller.NoContent()
            : CreateProblemResult(result.Error!, controller);

    /// <summary>
    /// Result&lt;string&gt; icindeki HAM JSON'u aynen (verbatim) doner - sync
    /// uclarinin "sakladigin = servis ettigin" sozlesmesi icin. Ok(value)
    /// KULLANILMAZ: ASP.NET string'i JSON string literal'ine sarar, istemci
    /// obje yerine tirnakli bir metin ("{\"bookId\":1,...}") alirdi.
    /// Hatalar ToActionResult ile ayni eslemeden gecer.
    /// </summary>
    public static IActionResult ToJsonContentResult(this Result<string> result, ControllerBase controller) =>
        result.IsSuccess
            ? new ContentResult
            {
                Content = result.Value,
                ContentType = "application/json",
                StatusCode = StatusCodes.Status200OK,
            }
            : CreateProblemResult(result.Error!, controller);

    /// <summary>
    /// ToJsonContentResult'in ETag/If-None-Match destekli hali (12.4). Sync
    /// yayinlari immutable oldugu icin ETag icerigin kendisinden degil,
    /// <paramref name="computeETag"/>'in JSON govdesinden okudugu versiyon
    /// alan(lar)indan turetilir - govde zaten cekildigi icin ekstra DB
    /// sorgusu gerekmez. Istemci ayni ETag'i If-None-Match ile geri
    /// gonderirse govde HIC yazilmadan 304 doner (mobil bant genisligi
    /// tasarrufu) - govde JSON'unun sekli/checksum invariant'i degismez,
    /// bu tamamen ek bir HTTP basligi.
    /// </summary>
    public static IActionResult ToJsonContentResultWithETag(
        this Result<string> result,
        ControllerBase controller,
        Func<string, string> computeETag)
    {
        if (!result.IsSuccess)
        {
            return CreateProblemResult(result.Error!, controller);
        }

        var eTag = $"W/\"{computeETag(result.Value!)}\"";
        controller.Response.Headers.ETag = eTag;

        var ifNoneMatch = controller.Request.Headers.IfNoneMatch;

        if (ifNoneMatch.Any(candidate => candidate == eTag))
        {
            return controller.StatusCode(StatusCodes.Status304NotModified);
        }

        return new ContentResult
        {
            Content = result.Value,
            ContentType = "application/json",
            StatusCode = StatusCodes.Status200OK,
        };
    }

    /// <summary>
    /// Bir JSON govdesinden tek bir sayisal ust-seviye alani okur - ETag
    /// hesaplamasi icin (12.4). SyncService'in ic parse pattern'iyle ayni
    /// teknik (JsonDocument.Parse), ama burada bilerek API katmaninda: ETag
    /// saf bir HTTP kavrami, Business'in sync sozlesmesiyle ilgisi yok.
    /// </summary>
    public static int ExtractJsonIntProperty(string json, string propertyName)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty(propertyName).GetInt32();
    }

    private static IActionResult CreateProblemResult(Error error, ControllerBase controller)
    {
        var statusCode = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError,
        };

        return controller.Problem(
            detail: error.Message,
            statusCode: statusCode,
            title: error.Code);
    }
}
