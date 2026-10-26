using System;
using System.Linq;

namespace pk3DS.Core.Structures;

/// <summary>
/// The shiny rate code patch (moved from the WinForms ShinyRate editor). The game rerolls a wild Pokémon's PID a number of times
/// to find a shiny one; the patch replaces the instruction that loads that count with a constant.
/// </summary>
public sealed class ShinyRate6
{
    private static readonly byte[] RerollPattern = [0x01, 0x50, 0x85, 0xE2, 0x05, 0x00, 0x50, 0xE1, 0xDE, 0xFF, 0xFF, 0xCA];
    private static readonly byte[] AlwaysPattern = [0x00, 0x20, 0x22, 0xE0, 0x02, 0x30, 0x21, 0xE2, 0x03, 0x20, 0x92, 0xE1, 0x1C, 0x00, 0x00];
    // LDRB R0, [R4, #0x23]: loads the game's own reroll count.
    private static readonly byte[] Original = [0x23, 0x00, 0xD4, 0xE5];
    private const byte AlwaysOff = 0x0A; // branch if not shiny
    private const byte AlwaysOn = 0xEA; // always branch

    // MOV R0, #value: the encoded immediate of each supported value, from the asm_mov resource (value, then the two argument bytes).
    private static readonly (int Value, byte[] Argument)[] Movs = Enumerable.Range(0, Properties.Resources.asm_mov.Length / 4)
        .Select(i => ((int)BitConverter.ToUInt16(Properties.Resources.asm_mov, 4 * i), Properties.Resources.asm_mov[((4 * i) + 2)..((4 * i) + 4)]))
        .ToArray();

    private readonly CodeBin code;
    private readonly int rerollOffset;
    private readonly int alwaysOffset;

    public ShinyRate6(CodeBin code)
    {
        this.code = code;
        rerollOffset = code.Find(RerollPattern, "PID generation routine", 0) - 4;
        int always = Util.IndexOfBytes(code.Data, AlwaysPattern, 0, 0);
        alwaysOffset = always < 0 || code.Data[always + AlwaysPattern.Length] is not (AlwaysOff or AlwaysOn) ? -1 : always + AlwaysPattern.Length;
    }

    /// <summary>True if the reroll count is the game's own (not patched).</summary>
    public bool IsOriginal => code.Data.AsSpan(rerollOffset, 4).SequenceEqual(Original);

    /// <summary>The patched reroll count, or null if not patched or patched with an instruction this table does not know.</summary>
    public int? Rerolls
    {
        get
        {
            if (IsOriginal)
                return null;
            var mov = Movs.FirstOrDefault(z => code.Data.AsSpan(rerollOffset, 2).SequenceEqual(z.Argument));
            return mov.Argument == null ? null : mov.Value;
        }
    }

    /// <summary>Patches the reroll count. Like the editor, a value the table cannot encode is raised to the next one it can.</summary>
    /// <returns>The reroll count written</returns>
    public int SetRerolls(int rerolls)
    {
        var mov = Movs.FirstOrDefault(z => z.Value >= Math.Min(rerolls, ushort.MaxValue));
        if (mov.Argument == null)
            mov = Movs[^1];
        byte[] instruction = [.. mov.Argument, 0xA0, 0xE3];
        instruction.CopyTo(code.Data, rerollOffset);
        return mov.Value;
    }

    /// <summary>Restores the game's own reroll count.</summary>
    public void Restore() => Original.CopyTo(code.Data, rerollOffset);

    /// <summary>Whether every Pokémon is shiny, or null if this executable has no recognizable switch for it.</summary>
    public bool? AlwaysShiny
    {
        get => alwaysOffset < 0 ? null : code.Data[alwaysOffset] == AlwaysOn;
        set
        {
            if (alwaysOffset < 0)
                throw new InvalidOperationException("This code.bin has no recognizable 'always shiny' switch.");
            code.Data[alwaysOffset] = value == true ? AlwaysOn : AlwaysOff;
        }
    }

    /// <summary>Chance that a Pokémon is shiny with the given reroll count: 1 - (4095/4096)^rerolls, as the editor shows it.</summary>
    public static double Chance(int rerolls) => 1 - Math.Pow(4095.0 / 4096, rerolls);
}
