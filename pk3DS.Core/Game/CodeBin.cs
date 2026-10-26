using System;
using System.IO;
using System.Linq;

namespace pk3DS.Core;

/// <summary>
/// The decompressed game executable (ExeFS <c>code.bin</c>), whose tables the TM/HM, mart and pickup editors edit in place.
/// </summary>
public sealed class CodeBin
{
    // The upstream editors only search the executable from here on.
    private const int SearchStart = 0x400000;

    private byte[] saved;

    public string Path { get; }
    public byte[] Data { get; }

    private CodeBin(string path, byte[] data)
    {
        Path = path;
        Data = data;
        saved = (byte[])data.Clone();
    }

    /// <summary>
    /// Loads <c>.code.bin</c> (or <c>code.bin</c>) from an extracted ExeFS folder. Like the upstream editors, a compressed file is refused.
    /// </summary>
    public static CodeBin Load(string exefs)
    {
        string path = new[] { ".code.bin", "code.bin" }.Select(f => System.IO.Path.Combine(exefs, f)).FirstOrDefault(File.Exists)
            ?? throw new FileNotFoundException($"No code.bin in '{exefs}'.");
        byte[] data = File.ReadAllBytes(path);
        // A decompressed executable is a multiple of 0x200 bytes; the BLZ-compressed one is not.
        if (data.Length % 0x200 != 0)
            throw new InvalidDataException($"'{System.IO.Path.GetFileName(path)}' is compressed; decompress it first (pk3ds blz decode) and compress it again before building the ROM (pk3ds blz encode).");
        return new CodeBin(path, data);
    }

    /// <summary>Finds a byte pattern the way the upstream editors do.</summary>
    /// <param name="pattern">Bytes to find</param>
    /// <param name="what">Name of the table, for the error message</param>
    /// <param name="start">Where to start searching; the table editors start at 0x400000</param>
    public int Find(byte[] pattern, string what, int start = SearchStart)
    {
        int offset = Util.IndexOfBytes(Data, pattern, start, 0);
        if (offset < 0)
            throw new InvalidDataException($"Could not find the {what} in '{System.IO.Path.GetFileName(Path)}'.");
        return offset;
    }

    public ushort[] ReadUInt16s(int offset, int count) =>
        [.. Enumerable.Range(0, count).Select(i => BitConverter.ToUInt16(Data, offset + (2 * i)))];

    public void WriteUInt16s(int offset, ushort[] values)
    {
        for (int i = 0; i < values.Length; i++)
            BitConverter.GetBytes(values[i]).CopyTo(Data, offset + (2 * i));
    }

    /// <summary>Writes the file if its bytes changed since it was loaded or last saved.</summary>
    /// <returns>True if the file was written</returns>
    public bool Save()
    {
        if (Data.AsSpan().SequenceEqual(saved))
            return false;
        File.WriteAllBytes(Path, Data);
        saved = (byte[])Data.Clone();
        return true;
    }
}
