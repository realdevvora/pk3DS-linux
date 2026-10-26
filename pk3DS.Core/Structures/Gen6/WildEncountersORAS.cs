using System;
using System.IO;
using System.Linq;
using pk3DS.Core.CTR;

namespace pk3DS.Core.Structures;

/// <summary>
/// The Omega Ruby / Alpha Sapphire wild encounter tables in the <c>encdata</c> GARC (moved from the WinForms RSWE).
/// </summary>
/// <remarks>
/// The GARC holds one LZSS-compressed file per map, then the zone data and a second copy of every map's table used by the DexNav.
/// A map file points at its table from offset 0x10; maps without encounters have no table.
/// </remarks>
public sealed class WildEncountersORAS
{
    public const int SlotCount = 61;
    private const int TableSize = SlotCount * Slot.Size;
    private const int TableMinimum = 0xF6; // RSWE treats a shorter span as "no encounters in this map"
    private const int ZoneSize = 56;

    /// <summary>The encounter types in table order, with their number of slots (as RSWE lays them out).</summary>
    public static readonly (string Type, int Count)[] SlotTypes =
    [
        ("grass", 12), ("tall_grass", 12), ("swarm", 3), ("surf", 5), ("rock_smash", 5),
        ("old_rod", 3), ("good_rod", 3), ("super_rod", 3), ("horde_a", 5), ("horde_b", 5), ("horde_c", 5),
    ];

    private readonly byte[][] files;
    private readonly byte[][] maps;
    private readonly int[] tableOffsets;

    /// <summary>The 61 slots of each map, or null for a map without encounters.</summary>
    public Slot[][] Tables { get; }

    /// <summary>Each map's location, as a line of the location name text.</summary>
    public int[] Locations { get; }

    public WildEncountersORAS(byte[][] garcFiles)
    {
        files = garcFiles;
        int mapCount = files.Length - 2;
        byte[] zones = files[mapCount];
        maps = new byte[mapCount][];
        tableOffsets = new int[mapCount];
        Tables = new Slot[mapCount][];
        Locations = new int[mapCount];
        for (int i = 0; i < mapCount; i++)
        {
            maps[i] = Decompress(files[i]);
            int offset = BitConverter.ToInt32(maps[i], 0x10) + 0xE;
            int end = BitConverter.ToInt32(maps[i], 0x14);
            tableOffsets[i] = offset;
            if (end - offset >= TableMinimum)
                Tables[i] = ReadTable(maps[i], offset);
            int z = (i * ZoneSize) + 0x1C;
            Locations[i] = zones[z] + (0x100 * (zones[z + 1] & 1));
        }
    }

    /// <summary>
    /// Writes the slots of every changed map to its file and to the DexNav copy, recompressing only those maps. Only the 61 slots are
    /// written; RSWE also cleared the 14 bytes after them, which 10 retail tables use.
    /// </summary>
    /// <returns>The GARC files, and how many maps changed</returns>
    public (byte[][] Files, int Changed) Write()
    {
        byte[][] result = (byte[][])files.Clone();
        byte[] dexNav = (byte[])files[^1].Clone();
        int changed = 0;
        for (int i = 0; i < Tables.Length; i++)
        {
            if (Tables[i] == null)
                continue;
            byte[] table = WriteTable(Tables[i]);
            if (table.AsSpan().SequenceEqual(maps[i].AsSpan(tableOffsets[i], TableSize)))
                continue;
            if (NeedsInsertion(dexNav, i))
                throw new InvalidDataException($"Map {i} has no DexNav entry; pk3DS does not save encounters for such a map.");
            byte[] map = (byte[])maps[i].Clone();
            table.CopyTo(map, tableOffsets[i]);
            result[i] = Compress(map);
            table.CopyTo(dexNav, BitConverter.ToInt32(dexNav, (i + 1) * 4) + 0xE);
            changed++;
        }
        result[^1] = dexNav;
        return (result, changed);
    }

    /// <summary>RSWE.NeedsInsertion: the map has no table in the DexNav copy.</summary>
    private static bool NeedsInsertion(byte[] dexNav, int map)
    {
        if (map != 535) // Hardcoded in RSWE
            return BitConverter.ToUInt32(dexNav, (map + 2) * 4) == BitConverter.ToUInt32(dexNav, (map + 1) * 4);
        return BitConverter.ToUInt32(dexNav, (map + 1) * 4) == dexNav.Length;
    }

    private static Slot[] ReadTable(byte[] data, int offset) =>
        [.. Enumerable.Range(0, SlotCount).Select(i => new Slot(data.AsSpan(offset + (i * Slot.Size), Slot.Size)))];

    private static byte[] WriteTable(Slot[] slots)
    {
        byte[] data = new byte[TableSize];
        for (int i = 0; i < slots.Length; i++)
            slots[i].Write(data.AsSpan(i * Slot.Size));
        return data;
    }

    private static byte[] Decompress(byte[] data)
    {
        using var output = new MemoryStream();
        LZSS.Decompress(new MemoryStream(data), data.Length, output);
        return output.ToArray();
    }

    private static byte[] Compress(byte[] data)
    {
        using var output = new MemoryStream();
        LZSS.Compress(new MemoryStream(data), data.Length, output, original: true);
        return output.ToArray();
    }

    /// <summary>One encounter slot: species and form packed into 16 bits (form in the top 5), then the level range.</summary>
    public sealed class Slot
    {
        public const int Size = 4;

        public int Species { get; set; }
        public int Form { get; set; }
        public int MinLevel { get; set; }
        public int MaxLevel { get; set; }

        public Slot(ReadOnlySpan<byte> data)
        {
            int value = BitConverter.ToUInt16(data);
            Species = value & 0x7FF;
            Form = value >> 11;
            MinLevel = data[2];
            MaxLevel = data[3];
        }

        public void Write(Span<byte> data)
        {
            BitConverter.TryWriteBytes(data, (ushort)((Form << 11) | Species));
            data[2] = (byte)MinLevel;
            data[3] = (byte)MaxLevel;
        }
    }
}
