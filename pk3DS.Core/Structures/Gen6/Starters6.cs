using System;
using System.Linq;

namespace pk3DS.Core.Structures;

/// <summary>
/// The starter choices (moved from the WinForms StarterEditor6): shown by <c>DllPoke3Select.cro</c> and given by the matching gifts in
/// <c>DllField.cro</c>. ORAS has four sets of three (Gen 3, 2, 4, 5), X/Y two (Gen 6, 1).
/// </summary>
public sealed class Starters6
{
    public const string SelectFileName = "DllPoke3Select.cro";
    private const int Stride = 0x54;

    public static readonly string[] SetNamesORAS = ["Gen 3 Starters", "Gen 2 Starters", "Gen 4 Starters", "Gen 5 Starters"];
    public static readonly string[] SetNamesXY = ["Gen 6 Starters", "Gen 1 Starters"];

    // Gift entries that give each starter, in set order.
    private static readonly int[] GiftsORAS = [0, 1, 2, 28, 29, 30, 31, 32, 33, 34, 35, 36];
    private static readonly int[] GiftsXY = [0, 1, 2, 3, 4, 5];

    private readonly bool oras;
    private readonly int offset;

    /// <summary>Species of each set's three starters.</summary>
    public ushort[][] Species { get; }

    public Starters6(CroFile select, bool oras)
    {
        this.oras = oras;
        offset = BitConverter.ToInt32(select.Data, 0xB8);
        if (!oras) // X/Y have 0x10 bytes of zeroes
            offset += 0x10;
        Species = [.. Enumerable.Range(0, oras ? 4 : 2).Select(i => Enumerable.Range(0, 3).Select(j => BitConverter.ToUInt16(select.Data, offset + (((i * 3) + j) * Stride))).ToArray())];
    }

    /// <summary>Writes the starters to the selection screen and to the gifts that give them.</summary>
    public void Write(CroFile select, CroFile field)
    {
        var gifts = FieldEncounters6.ReadGifts(field, oras);
        int[] entries = oras ? GiftsORAS : GiftsXY;
        for (int i = 0; i < Species.Length; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                BitConverter.GetBytes(Species[i][j]).CopyTo(select.Data, offset + (((i * 3) + j) * Stride));
                gifts[entries[(i * 3) + j]].Species = Species[i][j];
            }
        }
        FieldEncounters6.WriteGifts(field, oras, gifts);
    }
}
