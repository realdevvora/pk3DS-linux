using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;

namespace pk3DS.Core;

/// <summary>
/// Minimal PNG reader/writer for <see cref="PixelImage"/>. Writes 8-bit RGBA; reads non-interlaced 8-bit grayscale, RGB, palette, gray+alpha and RGBA.
/// </summary>
public static class PngCodec
{
    private static readonly byte[] Signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public static void Save(PixelImage image, string path) => File.WriteAllBytes(path, Encode(image));

    public static PixelImage Load(string path) => Decode(File.ReadAllBytes(path));

    public static byte[] Encode(PixelImage image)
    {
        using var ms = new MemoryStream();
        ms.Write(Signature);

        Span<byte> header = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, image.Width);
        BinaryPrimitives.WriteInt32BigEndian(header[4..], image.Height);
        header[8] = 8; // bit depth
        header[9] = 6; // RGBA
        WriteChunk(ms, "IHDR", header);

        using var raw = new MemoryStream();
        using (var z = new ZLibStream(raw, CompressionLevel.Optimal, leaveOpen: true))
        {
            byte[] row = new byte[1 + (image.Width * 4)]; // filter type 0
            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    int src = ((y * image.Width) + x) * 4;
                    int dst = 1 + (x * 4);
                    row[dst] = image.Bgra[src + 2];
                    row[dst + 1] = image.Bgra[src + 1];
                    row[dst + 2] = image.Bgra[src];
                    row[dst + 3] = image.Bgra[src + 3];
                }
                z.Write(row);
            }
        }
        WriteChunk(ms, "IDAT", raw.ToArray());
        WriteChunk(ms, "IEND", []);
        return ms.ToArray();
    }

    public static PixelImage Decode(byte[] data)
    {
        if (data.Length < Signature.Length || !data.AsSpan(0, Signature.Length).SequenceEqual(Signature))
            throw new InvalidDataException("Not a PNG file.");

        int width = 0, height = 0, colorType = 0;
        byte[] palette = null, paletteAlpha = null;
        using var idat = new MemoryStream();

        int pos = Signature.Length;
        while (pos + 8 <= data.Length)
        {
            int length = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(pos));
            string type = System.Text.Encoding.ASCII.GetString(data, pos + 4, 4);
            var body = data.AsSpan(pos + 8, length);
            pos += 12 + length;

            switch (type)
            {
                case "IHDR":
                    width = BinaryPrimitives.ReadInt32BigEndian(body);
                    height = BinaryPrimitives.ReadInt32BigEndian(body[4..]);
                    colorType = body[9];
                    if (body[8] != 8 || body[12] != 0)
                        throw new NotSupportedException("Only non-interlaced 8-bit PNG files are supported.");
                    break;
                case "PLTE":
                    palette = body.ToArray();
                    break;
                case "tRNS":
                    paletteAlpha = body.ToArray();
                    break;
                case "IDAT":
                    idat.Write(body);
                    break;
            }
            if (type == "IEND")
                break;
        }

        int channels = colorType switch
        {
            0 => 1,
            2 => 3,
            3 => 1,
            4 => 2,
            6 => 4,
            _ => throw new NotSupportedException($"Unsupported PNG color type {colorType}."),
        };
        if (colorType == 3 && palette == null)
            throw new InvalidDataException("Palette PNG has no palette.");

        idat.Position = 0;
        using var z = new ZLibStream(idat, CompressionMode.Decompress);
        int stride = width * channels;
        byte[] prev = new byte[stride];
        byte[] cur = new byte[stride];
        var result = new PixelImage(width, height);
        for (int y = 0; y < height; y++)
        {
            int filter = z.ReadByte();
            z.ReadExactly(cur);
            Unfilter(filter, cur, prev, channels);
            for (int x = 0; x < width; x++)
                result.SetPixel(x, y, ToRgba(cur, x * channels, colorType, palette, paletteAlpha));
            (prev, cur) = (cur, prev);
        }
        return result;
    }

    private static Rgba ToRgba(byte[] row, int i, int colorType, byte[] palette, byte[] paletteAlpha)
    {
        switch (colorType)
        {
            case 0: return Rgba.FromArgb(255, row[i], row[i], row[i]);
            case 2: return Rgba.FromArgb(255, row[i], row[i + 1], row[i + 2]);
            case 3:
                int index = row[i];
                int alpha = paletteAlpha != null && index < paletteAlpha.Length ? paletteAlpha[index] : 255;
                return Rgba.FromArgb(alpha, palette[index * 3], palette[(index * 3) + 1], palette[(index * 3) + 2]);
            case 4: return Rgba.FromArgb(row[i + 1], row[i], row[i], row[i]);
            default: return Rgba.FromArgb(row[i + 3], row[i], row[i + 1], row[i + 2]);
        }
    }

    private static void Unfilter(int filter, byte[] cur, byte[] prev, int bpp)
    {
        for (int i = 0; i < cur.Length; i++)
        {
            int a = i >= bpp ? cur[i - bpp] : 0;
            int b = prev[i];
            int c = i >= bpp ? prev[i - bpp] : 0;
            int add = filter switch
            {
                0 => 0,
                1 => a,
                2 => b,
                3 => (a + b) / 2,
                4 => Paeth(a, b, c),
                _ => throw new InvalidDataException($"Invalid PNG filter type {filter}."),
            };
            cur[i] = (byte)(cur[i] + add);
        }
    }

    private static int Paeth(int a, int b, int c)
    {
        int p = a + b - c;
        int pa = Math.Abs(p - a);
        int pb = Math.Abs(p - b);
        int pc = Math.Abs(p - c);
        if (pa <= pb && pa <= pc)
            return a;
        return pb <= pc ? b : c;
    }

    private static void WriteChunk(Stream s, string type, ReadOnlySpan<byte> body)
    {
        Span<byte> buf = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(buf, body.Length);
        s.Write(buf);

        byte[] typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        s.Write(typeBytes);
        s.Write(body);

        uint crc = Crc32(Crc32(0, typeBytes), body);
        BinaryPrimitives.WriteUInt32BigEndian(buf, crc);
        s.Write(buf);
    }

    private static readonly uint[] CrcTable = BuildCrcTable();

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            uint c = n;
            for (int k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            table[n] = c;
        }
        return table;
    }

    private static uint Crc32(uint crc, ReadOnlySpan<byte> data)
    {
        uint c = ~crc;
        foreach (byte b in data)
            c = CrcTable[(c ^ b) & 0xFF] ^ (c >> 8);
        return ~c;
    }
}
