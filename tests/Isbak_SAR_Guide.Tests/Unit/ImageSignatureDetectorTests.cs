using Isbak_SAR_Guide.Business.Common;
using Shouldly;

namespace Isbak_SAR_Guide.Tests.Unit;

/// <summary>
/// Magic-byte tespiti Faz 6'nin guvenlik cekirdegi - "uzantı bazlı doğrulama
/// güvenlik değildir" (roadmap §10). Boyut okuma fonksiyonlari da burada:
/// gercek bir imaj kutuphanesi olmadan, sadece baytlardan.
/// </summary>
public class ImageSignatureDetectorTests
{
    [Fact]
    public void Detect_ValidPngHeader_ReturnsPngSignature()
    {
        var bytes = BuildMinimalPng(1, 1);

        var signature = ImageSignatureDetector.Detect(bytes);

        signature.ShouldNotBeNull();
        signature.Value.ContentType.ShouldBe("image/png");
        signature.Value.Extension.ShouldBe(".png");
    }

    [Fact]
    public void Detect_ValidJpegHeader_ReturnsJpegSignature()
    {
        byte[] bytes = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10];

        var signature = ImageSignatureDetector.Detect(bytes);

        signature.ShouldNotBeNull();
        signature.Value.ContentType.ShouldBe("image/jpeg");
    }

    [Fact]
    public void Detect_ValidGifHeader_ReturnsGifSignature()
    {
        var bytes = "GIF89a"u8.ToArray();

        var signature = ImageSignatureDetector.Detect(bytes);

        signature.ShouldNotBeNull();
        signature.Value.ContentType.ShouldBe("image/gif");
    }

    [Fact]
    public void Detect_ValidWebpHeader_ReturnsWebpSignature()
    {
        byte[] bytes = [.. "RIFF"u8.ToArray(), 0x00, 0x00, 0x00, 0x00, .. "WEBP"u8.ToArray()];

        var signature = ImageSignatureDetector.Detect(bytes);

        signature.ShouldNotBeNull();
        signature.Value.ContentType.ShouldBe("image/webp");
    }

    [Fact]
    public void Detect_RandomBytes_ReturnsNull()
    {
        byte[] bytes = [0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08];

        var signature = ImageSignatureDetector.Detect(bytes);

        signature.ShouldBeNull();
    }

    [Fact]
    public void Detect_TextFileMasqueradingAsImage_ReturnsNull()
    {
        // Uzantisi "photo.png" olsa bile icerik duz metinse imza eslesmez -
        // "uzantı bazlı doğrulama güvenlik değildir" tam olarak bu senaryo.
        var bytes = "<script>alert(1)</script>"u8.ToArray();

        var signature = ImageSignatureDetector.Detect(bytes);

        signature.ShouldBeNull();
    }

    [Fact]
    public void TryReadPngDimensions_ValidIhdr_ReturnsWidthAndHeight()
    {
        var bytes = BuildMinimalPng(800, 600);

        var dimensions = ImageSignatureDetector.TryReadPngDimensions(bytes);

        dimensions.ShouldNotBeNull();
        dimensions!.Value.Width.ShouldBe(800);
        dimensions.Value.Height.ShouldBe(600);
    }

    [Fact]
    public void TryReadPngDimensions_TruncatedContent_ReturnsNull()
    {
        byte[] tooShort = [0x89, 0x50, 0x4E, 0x47];

        var dimensions = ImageSignatureDetector.TryReadPngDimensions(tooShort);

        dimensions.ShouldBeNull();
    }

    [Fact]
    public void TryReadGifDimensions_ValidHeader_ReturnsWidthAndHeight()
    {
        var bytes = BuildMinimalGif(320, 240);

        var dimensions = ImageSignatureDetector.TryReadGifDimensions(bytes);

        dimensions.ShouldNotBeNull();
        dimensions!.Value.Width.ShouldBe(320);
        dimensions.Value.Height.ShouldBe(240);
    }

    [Fact]
    public void TryReadJpegDimensions_ValidSof0Segment_ReturnsWidthAndHeight()
    {
        var bytes = BuildMinimalJpeg(1024, 768);

        var dimensions = ImageSignatureDetector.TryReadJpegDimensions(bytes);

        dimensions.ShouldNotBeNull();
        dimensions!.Value.Width.ShouldBe(1024);
        dimensions.Value.Height.ShouldBe(768);
    }

    // ---- Yardımcılar: gercek bir imaj kutuphanesi olmadan, sadece
    // dedektorun okudugu alanlari dolduran minimal/gecersiz-govdeli dosyalar ----

    internal static byte[] BuildMinimalPng(int width, int height)
    {
        var bytes = new byte[33];
        byte[] signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        signature.CopyTo(bytes, 0);

        // IHDR chunk uzunlugu (13, buyuk-endian) + etiket
        bytes[8] = 0; bytes[9] = 0; bytes[10] = 0; bytes[11] = 13;
        "IHDR"u8.ToArray().CopyTo(bytes, 12);

        WriteUInt32BigEndian(bytes, 16, (uint)width);
        WriteUInt32BigEndian(bytes, 20, (uint)height);

        return bytes;
    }

    private static byte[] BuildMinimalGif(int width, int height)
    {
        var bytes = new byte[10];
        "GIF89a"u8.ToArray().CopyTo(bytes, 0);
        bytes[6] = (byte)(width & 0xFF);
        bytes[7] = (byte)((width >> 8) & 0xFF);
        bytes[8] = (byte)(height & 0xFF);
        bytes[9] = (byte)((height >> 8) & 0xFF);
        return bytes;
    }

    private static byte[] BuildMinimalJpeg(int width, int height)
    {
        // SOI + SOF0 segmenti (marker 0xC0): uzunluk(2) + hassasiyet(1) +
        // yukseklik(2) + genislik(2) + bilesen sayisi(1) - yukseklik genislikten
        // once gelir (JPEG spesifikasyonu).
        List<byte> bytes = [0xFF, 0xD8, 0xFF, 0xC0];
        bytes.AddRange([0x00, 0x08]); // segment uzunlugu (uzunluk alani dahil, 8)
        bytes.Add(0x08); // hassasiyet
        bytes.AddRange([(byte)(height >> 8), (byte)(height & 0xFF)]);
        bytes.AddRange([(byte)(width >> 8), (byte)(width & 0xFF)]);
        bytes.Add(0x03); // bilesen sayisi
        return [.. bytes];
    }

    private static void WriteUInt32BigEndian(byte[] buffer, int offset, uint value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }
}
