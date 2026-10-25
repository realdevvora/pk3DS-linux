using System.Linq;

namespace pk3DS.Core.Structures;

/// <summary>
/// Poké Mart inventories, stored in the executable (moved from the WinForms MartEditor6). Each mart has a fixed number of item slots.
/// </summary>
public sealed class Mart6
{
    private static readonly byte[] Vanilla = [0x00, .. "rom:/DllStartMenu.cro"u8, 0x00];
    private static readonly byte[] Patched = [0x00, .. "rom2:/DllStartMenu.cro"u8, 0x00, 0xFF];

    private static readonly byte[] EntriesORAS =
    [
        3, 10, 14, 17, 18, 19, 19, 19, 19, // General
        1,
        9, 6, 4, 3, 8,
        8, 3, 3, 4,
        3, 6, 8,
        7, 4,
    ];

    private static readonly byte[] EntriesXY =
    [
        2, 11, 14, 17, 18, 19, 19, 19, 19, // General
        1, // Unused
        4, 10, 3, 9, 1, 1, // Misc
        3, 3, // Balls
        5, 5, // TMs
        6, // Vitamins
        7, // Balls
        5, // TMs
        5, // TMs
        8, // Battle
        3, // Balls
    ];

    public static readonly string[] LocationsORAS =
    [
        "No Gym Badges [After Pokédex]", "1 Gym Badge", "2 Gym Badges", "3 Gym Badges", "4 Gym Badges", "5 Gym Badges", "6 Gym Badges", "7 Gym Badges", "8 Gym Badges",
        "No Gym Badges [Before Pokédex]",
        "Slateport Market [Incenses]", "Slateport Market [Vitamins]", "Slateport Market [TMs]", "Rustboro City [Poké Balls]", "Slateport City [X Items]",
        "Mauville City [TMs]", "Verdanturf Town [Poké Balls]", "Fallarbor Town [Poké Balls]", "Lavaridge Town [Herbs]",
        "Lilycove Dept Store, 2F Left [Run Away Items]", "Lilycove Dept Store, 3F Left [Vitamins]", "Lilycove Dept Store, 3F Right [X Items]",
        "Lilycove Dept Store, 4F Left [Offensive TMs]", "Lilycove Dept Store, 4F Right [Defensive TMs]",
    ];

    public static readonly string[] LocationsXY =
    [
        "No Gym Badges", "1 Gym Badge", "2 Gym Badges", "3 Gym Badges", "4 Gym Badges", "5 Gym Badges", "6 Gym Badges", "7 Gym Badges", "8 Gym Badges",
        "Unused",
        "Lumiose City [Herboriste]", "Lumiose City [Poké Ball Boutique]", "Lumiose City [Stone Emporium]", "Coumarine City [Incenses]", "Aquacorde Town [Poké Ball]", "Aquacorde Town [Potion]",
        "Lumiose City North Boulevard [Poké Balls]", "Cyllage City [Poké Balls]",
        "Shalour City [TMs]", "Lumiose City South Boulevard [TMs]",
        "Laverre City [Vitamins]",
        "Snowbelle City [Poké Balls]",
        "Kiloude City [TMs]",
        "Anistar City [TMs]",
        "Santalune City [X Items]",
        "Coumarine City [Poké Balls]",
    ];

    private readonly int offset;

    /// <summary>Item IDs of each mart, in the order of <see cref="LocationsORAS"/> / <see cref="LocationsXY"/>.</summary>
    public ushort[][] Items { get; }

    public Mart6(CodeBin code, bool oras)
    {
        // The table follows a file name string, which a patched executable spells differently.
        int vanilla = Util.IndexOfBytes(code.Data, Vanilla, 0x400000, 0);
        offset = vanilla >= 0 ? vanilla + Vanilla.Length : code.Find(Patched, "mart inventories") + Patched.Length;
        byte[] entries = oras ? EntriesORAS : EntriesXY;
        Items = new ushort[entries.Length][];
        int pos = offset;
        for (int i = 0; i < entries.Length; i++)
        {
            Items[i] = code.ReadUInt16s(pos, entries[i]);
            pos += 2 * entries[i];
        }
    }

    public void Write(CodeBin code) => code.WriteUInt16s(offset, [.. Items.SelectMany(z => z)]);
}
