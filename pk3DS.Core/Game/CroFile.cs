using System;
using System.IO;

namespace pk3DS.Core;

/// <summary>
/// A CRO module from the RomFS root (such as <c>DllField.cro</c>), whose tables the static encounter, gift and starter editors edit in place.
/// </summary>
/// <remarks>The game checks CRO hashes unless the RO module is patched; run <c>pk3ds cro hash</c> after editing if needed.</remarks>
public sealed class CroFile
{
    private byte[] saved;

    public string Path { get; }
    public byte[] Data { get; }

    public CroFile(string romfs, string name)
    {
        Path = System.IO.Path.Combine(romfs, name);
        if (!File.Exists(Path))
            throw new FileNotFoundException($"{name} was not found in the RomFS folder.", Path);
        Data = File.ReadAllBytes(Path);
        saved = (byte[])Data.Clone();
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
