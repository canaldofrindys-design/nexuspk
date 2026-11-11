using System.Buffers.Binary;
using System.Text;

namespace Priston.Application.Support;

/// <summary>
/// Confere que o arquivo é mesmo uma imagem PNG, JPEG ou BMP (pela estrutura, não pela extensão)
/// e devolve uma cópia sem metadados: EXIF, XMP, IPTC, comentários e textos do PNG saem,
/// e qualquer coisa grudada depois do fim da imagem é descartada.
/// </summary>
public static class ImageSanitizer
{
    public const int MaxBytes = 3 * 1024 * 1024;
    public const int MaxDimension = 10_000;

    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    // Chunks auxiliares que não carregam texto nem dados pessoais.
    private static readonly HashSet<string> PngKeep = new(StringComparer.Ordinal)
    {
        "IHDR", "PLTE", "IDAT", "IEND", "tRNS", "gAMA", "cHRM", "sRGB", "iCCP", "sBIT", "pHYs", "bKGD",
    };

    /// <summary>Null se o arquivo não for uma imagem aceita ou estiver corrompido.</summary>
    public static CleanAttachment? Clean(string? fileName, byte[] data)
    {
        if (data.Length is < 8 or > MaxBytes)
        {
            return null;
        }

        var (type, clean) = data.AsSpan(0, 8).SequenceEqual(PngSignature) ? ("image/png", Png(data))
            : data[0] == 0xFF && data[1] == 0xD8 ? ("image/jpeg", Jpeg(data))
            : data[0] == (byte)'B' && data[1] == (byte)'M' ? ("image/bmp", Bmp(data))
            : (null, null);
        return type is null || clean is null ? null : new CleanAttachment(SafeName(fileName, type), type, clean);
    }

    public static string SafeName(string? fileName, string contentType)
    {
        var name = Path.GetFileNameWithoutExtension((fileName ?? string.Empty).Replace('\\', '/').Split('/')[^1]);
        var sb = new StringBuilder();
        foreach (var ch in name.Normalize(NormalizationForm.FormC))
        {
            if (char.IsLetterOrDigit(ch) || ch is '-' or '_' or ' ' or '.' or '(' or ')')
            {
                sb.Append(ch);
            }
        }

        var stem = sb.ToString().Trim(' ', '.');
        if (stem.Length == 0)
        {
            stem = "imagem";
        }

        if (stem.Length > 80)
        {
            stem = stem[..80];
        }

        var ext = contentType switch
        {
            "image/png" => ".png",
            "image/jpeg" => ".jpg",
            _ => ".bmp",
        };
        return stem + ext;
    }

    private static byte[]? Png(byte[] data)
    {
        using var output = new MemoryStream(data.Length);
        output.Write(PngSignature);
        var pos = 8;
        var first = true;
        while (pos + 12 <= data.Length)
        {
            var length = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(pos, 4));
            if (length > int.MaxValue || pos + 12 + (long)length > data.Length)
            {
                return null;
            }

            var type = Encoding.ASCII.GetString(data, pos + 4, 4);
            if (first)
            {
                if (type != "IHDR" || length != 13)
                {
                    return null;
                }

                var w = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(pos + 8, 4));
                var h = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(pos + 12, 4));
                if (w is 0 or > MaxDimension || h is 0 or > MaxDimension)
                {
                    return null;
                }

                first = false;
            }

            var total = 12 + (int)length;
            if (PngKeep.Contains(type))
            {
                output.Write(data, pos, total);
            }

            pos += total;
            if (type == "IEND")
            {
                return output.ToArray();
            }
        }

        return null;
    }

    private static byte[]? Jpeg(byte[] data)
    {
        using var output = new MemoryStream(data.Length);
        output.Write(data, 0, 2);
        var pos = 2;
        var sawFrame = false;
        while (pos + 4 <= data.Length)
        {
            if (data[pos] != 0xFF)
            {
                return null;
            }

            var marker = data[pos + 1];
            if (marker == 0xFF)
            {
                pos++; // preenchimento
                continue;
            }

            var length = (data[pos + 2] << 8) | data[pos + 3];
            if (length < 2 || pos + 2 + length > data.Length)
            {
                return null;
            }

            if (marker is >= 0xC0 and <= 0xCF and not 0xC4 and not 0xC8 and not 0xCC)
            {
                if (length < 8)
                {
                    return null;
                }

                var h = (data[pos + 5] << 8) | data[pos + 6];
                var w = (data[pos + 7] << 8) | data[pos + 8];
                if (w is 0 or > MaxDimension || h is 0 or > MaxDimension)
                {
                    return null;
                }

                sawFrame = true;
            }

            if (marker == 0xDA)
            {
                if (!sawFrame)
                {
                    return null;
                }

                // Dados comprimidos até o último marcador de fim (FFD9); o que vier depois é descartado.
                var end = LastIndexOfEoi(data, pos + 2 + length);
                if (end < 0)
                {
                    return null;
                }

                output.Write(data, pos, end + 2 - pos);
                return output.ToArray();
            }

            // APP0 (JFIF), APP2 (perfil de cor) e APP14 (Adobe) ficam; os outros APPn e comentários saem.
            var drop = (marker is >= 0xE0 and <= 0xEF && marker is not 0xE0 and not 0xE2 and not 0xEE) || marker == 0xFE;
            if (!drop)
            {
                output.Write(data, pos, 2 + length);
            }

            pos += 2 + length;
        }

        return null;
    }

    private static int LastIndexOfEoi(byte[] data, int from)
    {
        for (var i = data.Length - 2; i >= from; i--)
        {
            if (data[i] == 0xFF && data[i + 1] == 0xD9)
            {
                return i;
            }
        }

        return -1;
    }

    private static byte[]? Bmp(byte[] data)
    {
        if (data.Length < 26)
        {
            return null;
        }

        var declared = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(2, 4));
        var offset = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(10, 4));
        var header = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(14, 4));
        if (declared < 26 || declared > data.Length || header < 12 || offset < 14 + header || offset >= declared)
        {
            return null;
        }

        long w, h;
        if (header == 12)
        {
            w = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(18, 2));
            h = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(20, 2));
        }
        else
        {
            w = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(18, 4));
            h = Math.Abs((long)BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(22, 4)));
        }

        return w is <= 0 or > MaxDimension || h is <= 0 or > MaxDimension ? null : data.AsSpan(0, (int)declared).ToArray();
    }
}
