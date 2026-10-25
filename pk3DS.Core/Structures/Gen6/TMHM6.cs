using System.Collections.Generic;
using System.Linq;

namespace pk3DS.Core.Structures;

/// <summary>
/// The moves taught by each TM and HM, stored in the executable (moved from the WinForms TMHMEditor6).
/// </summary>
public sealed class TMHM6
{
    private static readonly byte[] Signature = [0xD4, 0x00, 0xAE, 0x02, 0xAF, 0x02, 0xB0, 0x02];

    private readonly int offset;
    private readonly bool oras;

    public ushort[] TMs { get; }
    public ushort[] HMs { get; }

    public TMHM6(CodeBin code, bool oras)
    {
        this.oras = oras;
        offset = code.Find(Signature, "TM/HM list") + 8;
        var tms = new List<ushort>();
        var hms = new List<ushort>();
        ushort[] raw = code.ReadUInt16s(offset, Slots(oras).Count());
        foreach (var (slot, hm, _) in Slots(oras))
            (hm ? hms : tms).Add(raw[slot]);
        TMs = [.. tms];
        HMs = [.. hms];
    }

    public void Write(CodeBin code)
    {
        ushort[] raw = new ushort[Slots(oras).Count()];
        foreach (var (slot, hm, index) in Slots(oras))
            raw[slot] = hm ? HMs[index] : TMs[index];
        code.WriteUInt16s(offset, raw);
    }

    /// <summary>
    /// Stored order: TM01-TM92, HM01-HM05, then in ORAS HM06, TM93-TM100, HM07, and in X/Y TM93-TM100.
    /// </summary>
    private static IEnumerable<(int Slot, bool HM, int Index)> Slots(bool oras)
    {
        int slot = 0;
        for (int i = 0; i < 92; i++)
            yield return (slot++, false, i);
        for (int i = 0; i < 5; i++)
            yield return (slot++, true, i);
        if (oras)
            yield return (slot++, true, 5);
        for (int i = 92; i < 100; i++)
            yield return (slot++, false, i);
        if (oras)
            yield return (slot, true, 6);
    }

    /// <summary>
    /// Copies the move description of each TM/HM whose move differs from <paramref name="original"/> into that TM/HM item's
    /// description. TMHMEditor6 copied all of them, which also rewrote the 44 retail item descriptions worded differently from their move's.
    /// </summary>
    /// <returns>True if any description changed</returns>
    public bool SetItemDescriptions(GameConfig config, TMHM6 original)
    {
        string[] itemDescriptions = config.GetText(TextName.ItemFlavor);
        string[] moveDescriptions = config.GetText(TextName.MoveFlavor);
        string[] before = (string[])itemDescriptions.Clone();
        foreach (var (item, hm, index) in ItemSlots(oras))
        {
            ushort move = hm ? HMs[index] : TMs[index];
            if (move != (hm ? original.HMs[index] : original.TMs[index]))
                itemDescriptions[item] = moveDescriptions[move];
        }
        if (itemDescriptions.SequenceEqual(before))
            return false;
        config.SetText(TextName.ItemFlavor, itemDescriptions);
        return true;
    }

    /// <summary>Item numbers of the TMs and HMs, as TMHMEditor6 lists them.</summary>
    private static IEnumerable<(int Item, bool HM, int Index)> ItemSlots(bool oras)
    {
        for (int i = 1 - 1; i <= 92 - 1; i++) // TM01 - TM92
            yield return (328 + i, false, i);
        for (int i = 93 - 1; i <= 95 - 1; i++) // TM93 - TM95
            yield return (618 + i - 92, false, i);
        for (int i = 96 - 1; i <= 100 - 1; i++) // TM96 - TM100
            yield return (690 + i - 95, false, i);
        for (int i = 1 - 1; i <= 5 - 1; i++) // HM01 - HM05
            yield return (420 + i, true, i);
        if (oras)
        {
            yield return (425, true, 5); // HM06
            yield return (737, true, 6); // HM07
        }
    }
}
