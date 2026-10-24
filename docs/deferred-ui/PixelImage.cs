using System;

namespace pk3DS.Core;

/// <summary>
/// 32-bit color with 8 bits per channel.
/// </summary>
public readonly record struct Rgba(byte A, byte R, byte G, byte B)
{
    public static Rgba FromArgb(int a, int r, int g, int b) => new((byte)a, (byte)r, (byte)g, (byte)b);
}

/// <summary>
/// Platform-neutral raster image, stored as 8-bit B, G, R, A per pixel (the byte order of GDI+ 32bppArgb).
/// </summary>
public sealed class PixelImage
{
    public int Width { get; }
    public int Height { get; }

    /// <summary>
    /// Pixel bytes in B, G, R, A order, row by row from the top-left.
    /// </summary>
    public byte[] Bgra { get; }

    public PixelImage(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegative(height);
        Width = width;
        Height = height;
        Bgra = new byte[width * height * 4];
    }

    public PixelImage(int width, int height, byte[] bgra)
    {
        if (bgra.Length != width * height * 4)
            throw new ArgumentException("Pixel data length does not match the image dimensions.", nameof(bgra));
        Width = width;
        Height = height;
        Bgra = bgra;
    }

    public Rgba GetPixel(int x, int y)
    {
        int i = GetOffset(x, y);
        return new Rgba(Bgra[i + 3], Bgra[i + 2], Bgra[i + 1], Bgra[i]);
    }

    public void SetPixel(int x, int y, Rgba c)
    {
        int i = GetOffset(x, y);
        Bgra[i] = c.B;
        Bgra[i + 1] = c.G;
        Bgra[i + 2] = c.R;
        Bgra[i + 3] = c.A;
    }

    private int GetOffset(int x, int y)
    {
        if ((uint)x >= (uint)Width || (uint)y >= (uint)Height)
            throw new ArgumentOutOfRangeException(x < 0 || x >= Width ? nameof(x) : nameof(y));
        return ((y * Width) + x) * 4;
    }

    /// <summary>
    /// Copies the top-left <paramref name="width"/> x <paramref name="height"/> area; any part outside this image is left transparent.
    /// </summary>
    public PixelImage Crop(int width, int height)
    {
        var result = new PixelImage(width, height);
        int copyWidth = Math.Min(width, Width);
        int copyHeight = Math.Min(height, Height);
        for (int y = 0; y < copyHeight; y++)
            Buffer.BlockCopy(Bgra, y * Width * 4, result.Bgra, y * width * 4, copyWidth * 4);
        return result;
    }

    /// <summary>
    /// Swaps rows and columns (a 90 degree rotation followed by a mirror), the equivalent of GDI+ <c>Rotate90FlipX</c>.
    /// </summary>
    public PixelImage Transpose()
    {
        var result = new PixelImage(Height, Width);
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
                Buffer.BlockCopy(Bgra, ((y * Width) + x) * 4, result.Bgra, ((x * Height) + y) * 4, 4);
        }
        return result;
    }
}
