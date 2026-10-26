using System;
using System.Linq;

namespace pk3DS.Core.Structures;

/// <summary>
/// O-Power data, stored in the executable (moved from the WinForms OPower editor). Entry 0 is unused; the editor lists 1-64.
/// </summary>
public sealed class OPower6
{
    // The table follows this string and some 0xFF padding.
    private static readonly byte[] Signature = [.. "49461845"u8, 0x00];
    private const int Count = 65;

    private readonly int offset;

    public Entry[] Entries { get; }

    public OPower6(CodeBin code)
    {
        offset = code.Find(Signature, "O-Power table") + Signature.Length;
        while (code.Data[offset] == 0xFF)
            offset++;
        Entries = [.. Enumerable.Range(0, Count).Select(i => new Entry(code.Data.AsSpan(offset + (i * Entry.Size), Entry.Size).ToArray()))];
    }

    public void Write(CodeBin code)
    {
        for (int i = 0; i < Entries.Length; i++)
            Entries[i].Data.CopyTo(code.Data, offset + (i * Entry.Size));
    }

    /// <summary>One O-Power. The setters are the fields the OPower editor edits; the rest is read-only there too.</summary>
    public sealed class Entry(byte[] data)
    {
        public const int Size = 22;
        public readonly byte[] Data = data;

        public int Quality => Data[0x00];
        /// <summary>The editor only accepts 0, 2 and 254.</summary>
        public int Usability { get => Data[0x01]; set => Data[0x01] = (byte)value; }
        public int PlayerCost { get => Data[0x03]; set => Data[0x03] = (byte)value; }
        public int OtherCost { get => Data[0x04]; set => Data[0x04] = (byte)value; }
        public int Type => BitConverter.ToUInt16(Data, 0x06);
        public int Mini => BitConverter.ToUInt16(Data, 0x08);
        public int Description => BitConverter.ToUInt16(Data, 0x0A);
        /// <summary>Line of the O-Power text file holding the name.</summary>
        public int Name => BitConverter.ToUInt16(Data, 0x0C);
        public int Stage { get => Data[0x0E]; set => Data[0x0E] = (byte)value; }
        public int LevelUp { get => Data[0x0F]; set => Data[0x0F] = (byte)value; }
        public int SortOrder => BitConverter.ToUInt16(Data, 0x10);
        public int Efficacy { get => BitConverter.ToUInt16(Data, 0x12); set => BitConverter.GetBytes((ushort)value).CopyTo(Data, 0x12); }
        public int Duration { get => Data[0x14]; set => Data[0x14] = (byte)value; }
    }
}
