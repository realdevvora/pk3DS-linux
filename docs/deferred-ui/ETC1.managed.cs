using System;
using System.Buffers.Binary;

namespace pk3DS.Core.CTR;

/// <summary>
/// Managed ETC1 / ETC1A4 texture decoder for the 3DS block layout.
/// </summary>
internal static class ETC1
{
    private static readonly int[][] ModifierTable =
    [
        [2, 8, -2, -8],
        [5, 17, -5, -17],
        [9, 29, -9, -29],
        [13, 42, -13, -42],
        [18, 60, -18, -60],
        [24, 80, -24, -80],
        [33, 106, -33, -106],
        [47, 183, -47, -183],
    ];

    // Order of the 4x4 blocks inside an 8x8 tile, in block units.
    private static readonly (int X, int Y)[] BlockOrder = [(0, 0), (1, 0), (0, 1), (1, 1)];

    /// <summary>
    /// Decodes swizzled 3DS ETC1 data into row-major pixels, packed as little-endian R, G, B, A bytes.
    /// </summary>
    /// <param name="data">Compressed texture data</param>
    /// <param name="width">Width in pixels, a multiple of 8</param>
    /// <param name="height">Height in pixels, a multiple of 8</param>
    /// <param name="alpha">True for ETC1A4 (an alpha block precedes each color block)</param>
    public static uint[] Decode(ReadOnlySpan<byte> data, int width, int height, bool alpha)
    {
        if (width % 8 != 0 || height % 8 != 0)
            throw new ArgumentException("ETC1 dimensions must be multiples of 8.");
        int blockSize = alpha ? 16 : 8;
        if (data.Length < width / 4 * (height / 4) * blockSize)
            throw new ArgumentException("Not enough ETC1 data for the given dimensions.", nameof(data));

        var output = new uint[width * height];
        int pos = 0;
        for (int tileY = 0; tileY < height; tileY += 8)
        {
            for (int tileX = 0; tileX < width; tileX += 8)
            {
                foreach (var (bx, by) in BlockOrder)
                {
                    ulong alphaBlock = ulong.MaxValue;
                    if (alpha)
                    {
                        alphaBlock = BinaryPrimitives.ReadUInt64LittleEndian(data[pos..]);
                        pos += 8;
                    }
                    ulong colorBlock = BinaryPrimitives.ReadUInt64LittleEndian(data[pos..]);
                    pos += 8;
                    DecodeBlock(colorBlock, alphaBlock, output, width, tileX + (bx * 4), tileY + (by * 4));
                }
            }
        }
        return output;
    }

    private static void DecodeBlock(ulong block, ulong alphaBlock, uint[] output, int stride, int originX, int originY)
    {
        uint top = (uint)(block >> 32);
        uint bottom = (uint)block;

        bool flip = (top & 1) != 0;
        bool diff = (top & 2) != 0;
        int table1 = (int)((top >> 5) & 7);
        int table2 = (int)((top >> 2) & 7);

        int r1, g1, b1, r2, g2, b2;
        if (diff)
        {
            int r = (int)((top >> 27) & 0x1F);
            int g = (int)((top >> 19) & 0x1F);
            int b = (int)((top >> 11) & 0x1F);
            r1 = Extend5(r);
            g1 = Extend5(g);
            b1 = Extend5(b);
            r2 = Extend5(r + SignExtend3((int)((top >> 24) & 7)));
            g2 = Extend5(g + SignExtend3((int)((top >> 16) & 7)));
            b2 = Extend5(b + SignExtend3((int)((top >> 8) & 7)));
        }
        else
        {
            r1 = Extend4((int)((top >> 28) & 0xF));
            g1 = Extend4((int)((top >> 20) & 0xF));
            b1 = Extend4((int)((top >> 12) & 0xF));
            r2 = Extend4((int)((top >> 24) & 0xF));
            g2 = Extend4((int)((top >> 16) & 0xF));
            b2 = Extend4((int)((top >> 8) & 0xF));
        }

        for (int x = 0; x < 4; x++)
        {
            for (int y = 0; y < 4; y++)
            {
                int p = (x * 4) + y; // pixels within a block run down each column
                bool second = flip ? y >= 2 : x >= 2;
                int[] table = ModifierTable[second ? table2 : table1];
                int index = (int)((((bottom >> (16 + p)) & 1) << 1) | ((bottom >> p) & 1));
                int modifier = table[index];

                int r = Clamp((second ? r2 : r1) + modifier);
                int g = Clamp((second ? g2 : g1) + modifier);
                int b = Clamp((second ? b2 : b1) + modifier);
                int a = (int)((alphaBlock >> (p * 4)) & 0xF) * 0x11;

                output[((originY + y) * stride) + originX + x] = (uint)(r | (g << 8) | (b << 16) | (a << 24));
            }
        }
    }

    private static int Extend4(int v) => (v << 4) | v;

    private static int Extend5(int v) => (v << 3) | (v >> 2);

    private static int SignExtend3(int v) => v >= 4 ? v - 8 : v;

    private static int Clamp(int v) => Math.Clamp(v, 0, 255);
}
