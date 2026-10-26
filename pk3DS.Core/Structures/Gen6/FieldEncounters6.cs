using System;
using System.Linq;

namespace pk3DS.Core.Structures;

/// <summary>
/// The static encounter and gift Pokémon tables in <c>DllField.cro</c> (moved from the WinForms StaticEncounterEditor6 and GiftEditor6).
/// </summary>
public static class FieldEncounters6
{
    public const string FileName = "DllField.cro";

    private const int StaticSize = 0xC;
    private static int StaticOffset(bool oras) => oras ? 0xF1B20 : 0xEE46C;
    private static int StaticCount(bool oras) => oras ? 0x3B : 0xD;

    private static int GiftOffset(bool oras) => oras ? 0xF906C : 0xF805C;
    private static int GiftSize(bool oras) => oras ? 0x24 : 0x18;
    private static int GiftCount(bool oras) => oras ? 0x25 : 0x13;

    public static EncounterStatic6[] ReadStatic(CroFile field, bool oras) =>
        [.. Enumerable.Range(0, StaticCount(oras)).Select(i => new EncounterStatic6(field.Data.AsSpan(StaticOffset(oras) + (i * StaticSize), StaticSize).ToArray()))];

    public static void WriteStatic(CroFile field, bool oras, EncounterStatic6[] encounters)
    {
        for (int i = 0; i < encounters.Length; i++)
            encounters[i].Write().CopyTo(field.Data, StaticOffset(oras) + (i * StaticSize));
    }

    public static EncounterGift6[] ReadGifts(CroFile field, bool oras) =>
        [.. Enumerable.Range(0, GiftCount(oras)).Select(i => new EncounterGift6(field.Data.AsSpan(GiftOffset(oras) + (i * GiftSize(oras)), GiftSize(oras)).ToArray(), oras))];

    public static void WriteGifts(CroFile field, bool oras, EncounterGift6[] gifts)
    {
        for (int i = 0; i < gifts.Length; i++)
            gifts[i].Write().CopyTo(field.Data, GiftOffset(oras) + (i * GiftSize(oras)));
    }
}
