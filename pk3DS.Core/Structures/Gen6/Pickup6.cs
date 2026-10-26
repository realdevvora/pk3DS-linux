namespace pk3DS.Core.Structures;

/// <summary>
/// The items the Pickup ability finds, stored in the executable (moved from the WinForms PickupEditor6).
/// </summary>
public sealed class Pickup6
{
    // Bytes that follow the item lists; the lists start 0x3A bytes before them.
    private static readonly byte[] Signature = [0x1E, 0x28, 0x32, 0x3C, 0x46, 0x50, 0x5A, 0x5E, 0x62, 0x05, 0x0A, 0x0F, 0x14, 0x19, 0x1E, 0x23, 0x28, 0x2D, 0x32];
    private const int CommonCount = 0x12;
    private const int RareCount = 0xB;

    private readonly int offset;

    public ushort[] Common { get; }
    public ushort[] Rare { get; }

    public Pickup6(CodeBin code)
    {
        offset = code.Find(Signature, "Pickup item lists") - 0x3A;
        Common = code.ReadUInt16s(offset, CommonCount);
        Rare = code.ReadUInt16s(offset + (2 * CommonCount), RareCount);
    }

    public void Write(CodeBin code)
    {
        code.WriteUInt16s(offset, Common);
        code.WriteUInt16s(offset + (2 * CommonCount), Rare);
    }
}
