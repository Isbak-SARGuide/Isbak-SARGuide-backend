namespace Isbak_SAR_Guide.Business.Common;

/// <summary>
/// Dosyanin GERCEK tipini ilk baytlarindan (magic byte / file signature)
/// tespit eder - uzantiya veya istemcinin bildirdigi Content-Type'a asla
/// guvenilmez ("uzantı bazlı doğrulama güvenlik değildir", roadmap §10).
/// MVP kapsami sadece imaj (Video Faz 1'de yok, roadmap §1 Varsayımlar).
/// Statik ve saf: durumu yok, disk/agi bilmez - test etmesi kolay olsun diye.
/// </summary>
public static class ImageSignatureDetector
{
    public readonly record struct Signature(string ContentType, string Extension);

    /// <summary>
    /// Baytlari bilinen imza tablosuyla eslestirir. Eslesme yoksa null -
    /// caginan taraf bunu "desteklenmeyen/bozuk dosya" olarak reddetmeli.
    /// </summary>
    public static Signature? Detect(ReadOnlySpan<byte> header)
    {
        if (header.Length >= 8 && header[..8].SequenceEqual(Png))
        {
            return new Signature("image/png", ".png");
        }

        if (header.Length >= 3 && header[..3].SequenceEqual(Jpeg))
        {
            return new Signature("image/jpeg", ".jpg");
        }

        if (header.Length >= 6 && (header[..6].SequenceEqual(Gif87A) || header[..6].SequenceEqual(Gif89A)))
        {
            return new Signature("image/gif", ".gif");
        }

        if (header.Length >= 12
            && header[..4].SequenceEqual(RiffPrefix)
            && header[8..12].SequenceEqual(WebpPrefix))
        {
            return new Signature("image/webp", ".webp");
        }

        return null;
    }

    /// <summary>
    /// PNG genislik/yukseklik IHDR chunk'inda, byte offset 16'da (8 bayt imza +
    /// 4 bayt chunk uzunlugu + 4 bayt "IHDR" etiketi) buyuk-endian 4'er bayt
    /// olarak durur - kutuphanesiz okunabilir (CLAUDE.md'de ayni teknik,
    /// icerik import surecinde de kullanildi).
    /// </summary>
    public static (int Width, int Height)? TryReadPngDimensions(ReadOnlySpan<byte> content)
    {
        if (content.Length < 24)
        {
            return null;
        }

        var width = ReadUInt32BigEndian(content[16..20]);
        var height = ReadUInt32BigEndian(content[20..24]);
        return ((int)width, (int)height);
    }

    /// <summary>GIF genislik/yukseklik, 6 baytlik imzadan hemen sonra kucuk-endian 2'ser bayt.</summary>
    public static (int Width, int Height)? TryReadGifDimensions(ReadOnlySpan<byte> content)
    {
        if (content.Length < 10)
        {
            return null;
        }

        var width = ReadUInt16LittleEndian(content[6..8]);
        var height = ReadUInt16LittleEndian(content[8..10]);
        return (width, height);
    }

    /// <summary>
    /// JPEG segment segment taranir; SOF (Start Of Frame) marker'i bulunca
    /// (0xFFC0-0xFFCF, DHT/JPG uzantilari 0xC4/0xC8/0xCC haric) yukseklik/genislik
    /// o segmentin icinde sabit ofsette durur. Diger segmentler kendi uzunluk
    /// alanlarindan atlanir.
    /// </summary>
    public static (int Width, int Height)? TryReadJpegDimensions(ReadOnlySpan<byte> content)
    {
        var offset = 2; // ilk iki bayt SOI (0xFFD8), zaten Detect'te dogrulandi

        while (offset + 4 <= content.Length)
        {
            if (content[offset] != 0xFF)
            {
                offset++;
                continue;
            }

            var marker = content[offset + 1];

            // SOI/EOI/RSTn/TEM gibi payload'suz marker'lar - uzunluk alani yok.
            if (marker is 0xD8 or 0xD9 or (>= 0xD0 and <= 0xD7) or 0x01)
            {
                offset += 2;
                continue;
            }

            var segmentLength = ReadUInt16BigEndian(content[(offset + 2)..(offset + 4)]);

            var isSofMarker = marker is (>= 0xC0 and <= 0xCF)
                and not 0xC4 and not 0xC8 and not 0xCC; // DHT/JPG/DAC haric - gercek SOF degiller

            if (isSofMarker)
            {
                if (offset + 9 > content.Length)
                {
                    return null;
                }

                var height = ReadUInt16BigEndian(content[(offset + 5)..(offset + 7)]);
                var width = ReadUInt16BigEndian(content[(offset + 7)..(offset + 9)]);
                return (width, height);
            }

            offset += 2 + segmentLength;
        }

        return null;
    }

    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] Gif87A = "GIF87a"u8.ToArray();
    private static readonly byte[] Gif89A = "GIF89a"u8.ToArray();
    private static readonly byte[] RiffPrefix = "RIFF"u8.ToArray();
    private static readonly byte[] WebpPrefix = "WEBP"u8.ToArray();

    private static uint ReadUInt32BigEndian(ReadOnlySpan<byte> bytes) =>
        (uint)((bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3]);

    private static int ReadUInt16BigEndian(ReadOnlySpan<byte> bytes) =>
        (bytes[0] << 8) | bytes[1];

    private static int ReadUInt16LittleEndian(ReadOnlySpan<byte> bytes) =>
        bytes[0] | (bytes[1] << 8);
}
