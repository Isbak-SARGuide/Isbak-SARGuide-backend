using SkiaSharp;

namespace Isbak_SAR_Guide.Tests.Unit;

/// <summary>
/// Faz 12.7 (WebP + thumbnail) MediaService.UploadAsync'i artik SKBitmap.Decode
/// ile GERCEKTEN decode ediyor - ImageSignatureDetectorTests.BuildMinimalPng/
/// Jpeg/Gif (sahte IHDR/header, gercek piksel verisi yok) bu yuzden Media
/// testlerinde artik yeterli degil (o yardimcilar ImageSignatureDetector'in
/// KENDI hand-rolled byte-parser'ini test etmeye devam ediyor, degismedi).
/// Bu sinif SkiaSharp'in kendisiyle GERCEKTEN decode edilebilir, kucuk,
/// tek-renkli test gorselleri uretir.
/// </summary>
public static class TestImageFactory
{
    public static byte[] BuildRealPng(int width, int height) => Encode(width, height, SKEncodedImageFormat.Png);

    public static byte[] BuildRealJpeg(int width, int height) => Encode(width, height, SKEncodedImageFormat.Jpeg);

    /// <summary>
    /// SkiaSharp GIF ENCODE desteklemiyor (sadece decode, Encode(...Gif...)
    /// null doner) - digerleri gibi programatik uretilemiyor. Yaygin bilinen,
    /// gercekten gecerli (LZW image data dahil, sadece sahte header degil)
    /// 1x1 seffaf GIF89a baytlari sabit kullanilir - boyut testi bu yuzden
    /// 1x1'e sabit.
    /// </summary>
    public static byte[] BuildRealGif1X1() =>
    [
        0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 0x01, 0x00, 0x01, 0x00,
        0x80, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0x21,
        0xF9, 0x04, 0x01, 0x00, 0x00, 0x00, 0x00, 0x2C, 0x00, 0x00,
        0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0x02, 0x02, 0x44,
        0x01, 0x00, 0x3B,
    ];

    private static byte[] Encode(int width, int height, SKEncodedImageFormat format)
    {
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.CornflowerBlue);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, quality: 90);
        return data.ToArray();
    }
}
