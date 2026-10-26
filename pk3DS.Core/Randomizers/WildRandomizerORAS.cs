using System;
using System.Linq;
using pk3DS.Core.Structures;
using pk3DS.Core.Structures.PersonalInfo;

namespace pk3DS.Core.Randomizers;

/// <summary>Options of <see cref="WildRandomizerORAS"/>; the defaults are RSWE's starting values.</summary>
public sealed record WildRandomizerOptions
{
    public bool Gen1 { get; init; } = true;
    public bool Gen2 { get; init; } = true;
    public bool Gen3 { get; init; } = true;
    public bool Gen4 { get; init; } = true;
    public bool Gen5 { get; init; } = true;
    public bool Gen6 { get; init; } = true;
    public bool GameLegends { get; init; } = true;
    public bool EventLegends { get; init; } = true;
    /// <summary>"Randomize by BST": replacements have a similar base stat total.</summary>
    public bool SimilarBST { get; init; }
    public bool MultiplyLevels { get; init; }
    public decimal LevelMultiplier { get; init; } = 1;
    public bool RandomMegaForms { get; init; }
    public bool SingleSpeciesHordes { get; init; }
}

/// <summary>
/// The ORAS wild encounter randomizer (moved from the WinForms RSWE). Keeps at most 18 distinct species per map so the DexNav does not crash.
/// </summary>
public sealed class WildRandomizerORAS(GameConfig config, WildEncountersORAS wild, WildRandomizerOptions o) : IRandomizer
{
    private readonly PersonalInfo[] stats = config.Personal.Table;

    private static uint Random32() => Util.Random32();

    /// <summary>RSWE.B_Randomize_Click</summary>
    public void Execute()
    {
        decimal leveldiff = o.LevelMultiplier;
        var rand = new SpeciesRandomizer(config)
        {
            G1 = o.Gen1,
            G2 = o.Gen2,
            G3 = o.Gen3,
            G4 = o.Gen4,
            G5 = o.Gen5,
            G6 = o.Gen6,
            L = o.GameLegends,
            E = o.EventLegends,
            Shedinja = false,
            rBST = o.SimilarBST,
        };
        rand.Initialize();

        int slotCount = WildEncountersORAS.SlotCount;
        int[] slotArray = Enumerable.Range(0, slotCount).ToArray();

        foreach (var slots in wild.Tables)
        {
            if (slots == null || !HasData(slots))
                continue; // Don't randomize if doesn't have data.

            // Assign Levels
            if (o.MultiplyLevels)
            {
                foreach (var s in slots)
                    s.MinLevel = s.MaxLevel = s.MaxLevel <= 1 ? s.MaxLevel : Math.Max(1, Math.Min(100, (int)(leveldiff * s.MaxLevel)));
            }

            // Get a new list of Pokemon so that DexNav does not crash.
            int[] list = new int[slotCount];
            int used = 19;

            // Count up how many slots are active.
            for (int s = 0; s < slotCount; s++)
            {
                if (slots[s].Species > 0)
                    list[s] = slots[s].Species;
            }

            // At most 18, but don't chew if there's only a few slots.
            int cons = list.Count(a => a != 0);
            int[] RandomList = new int[cons > 18 ? 18 - (cons / 8) : cons];

            // Fill Location List
            for (int s = 0; s < RandomList.Length; s++)
                RandomList[s] = rand.GetRandomSpecies(slots[s].Species);

            // Assign Slots
            while (used < RandomList.Distinct().Count() || used > 18) // Can just arbitrarily assign slots.
            {
                Util.Shuffle(slotArray);
                for (int s = 0; s < slotCount; s++)
                {
                    int slot = slotArray[s];
                    if (slots[slot].Species != 0) // If the slot is in use
                        list[slot] = RandomList[Util.Rand.Next(0, RandomList.Length)];
                }
                used = CountUnique(list);
                if (used != RandomList.Length)
                    ShuffleSlots(ref list);
                used = CountUnique(list);
            }
            // If Distinct Hordes are selected, homogenize
            int hordeslot = 0;
            if (o.SingleSpeciesHordes)
            {
                for (int slot = slotCount - 15; slot < slotCount; slot++)
                {
                    list[slot] = list[slot - (hordeslot % 5)];
                    hordeslot++;
                }
            }

            // Fill Slots
            for (int slot = 0; slot < slotCount; slot++)
            {
                if (slots[slot].Species != 0)
                {
                    slots[slot].Species = list[slot];
                    slots[slot].Form = GetRandomForm(slots[slot].Species);
                }
            }
        }
    }

    private static bool HasData(WildEncountersORAS.Slot[] slots) =>
        slots.Any(s => s.Species > 0 || s.Form > 0 || s.MinLevel > 0 || s.MaxLevel > 0);

    private static int CountUnique(int[] list)
    {
        int used = 0;
        used += list.Skip(0).Take(12).Distinct().Count(a => a != 0);
        used += list.Skip(12).Take(12).Distinct().Count(a => a != 0);
        used += list.Skip(24).Take(3).Distinct().Count(a => a != 0);
        used += list.Skip(27).Take(5).Distinct().Count(a => a != 0);
        used += list.Skip(32).Take(5).Distinct().Count(a => a != 0);
        used += list.Skip(37).Take(3).Distinct().Count(a => a != 0);
        used += list.Skip(40).Take(3).Distinct().Count(a => a != 0);
        used += list.Skip(43).Take(3).Distinct().Count(a => a != 0);
        used += list.Skip(46).Take(15).Distinct().Count(a => a != 0);
        return used;
    }

    /// <summary>RSWE.SetRandomForm</summary>
    private int GetRandomForm(int species)
    {
        if (o.RandomMegaForms && stats[species].FormeCount > 1 && Legal.Mega_ORAS.Contains((ushort)species))
            return (int)(Random32() % stats[species].FormeCount); // Slot-Random
        if (species is 666 or 665 or 664) // Vivillon
            return (int)(Random32() % 20);
        if (species == 386) // Deoxys
            return (int)(Random32() % 4);
        if (species == 201) // Unown
            return 31;
        if (species == 550) // Basculin
            return (int)(Random32() % 2);
        if (species is 412 or 413) // Wormadam
            return (int)(Random32() % 3);
        if (species is 422 or 423) // Gastrodon
            return (int)(Random32() % 2);
        if (species is 585 or 586) // Sawsbuck
            return (int)(Random32() % 4);
        if (species is 669 or 671) // Flabebe/Florges
            return (int)(Random32() % 5);
        if (species == 670) // Floette
            return (int)(Random32() % 6);
        if (species is 710 or 711) // Pumpkaboo
            return (int)(Random32() % 4);
        return 0;
    }

    private static void ShuffleSlots(ref int[] list)
    {
        int[] input = (int[])list.Clone();
        // Initialize
        int[] slotset = [0, 12, 24, 27, 32, 37, 40, 43, 46];
        int[] slotlen = [12, 12, 3, 5, 5, 3, 3, 3, 5 + 5 + 5];
        int[][] slotdata = new int[slotset.Length][];
        for (int i = 0; i < slotset.Length; i++)
            slotdata[i] = list.Skip(slotset[i]).Take(slotlen[i]).ToArray();

        int used = CountUnique(list);
        // Iterate
        while (used > 18)
        {
            for (int n = 0; n <= 20; n++)
            {
                if (n == 20)
                { list = (int[])input.Clone(); continue; } // Reset and try again (shuffle failed)
                for (int i = 0; i < slotdata.Length; i++)
                {
                    for (int j = 0; j < slotdata[i].Length; j++)
                    {
                        int data = slotdata[i][j];
                        if (data == 0) continue;
                        for (int s = 0; s < slotdata.Length; s++)
                        {
                            if (s == i) continue;
                            if (!slotdata[s].Contains(data)) continue;

                            int z = Array.LastIndexOf(slotdata[s], data);
                            int next = (z + 1 + Util.Rand.Next(slotdata[s].Length - 1)) % slotdata[s].Length;
                            int donor = slotdata[s][next];
                            if (donor == 0) continue;
                            if (donor != data)
                                (slotdata[s][next], slotdata[i][j]) = (slotdata[i][j], slotdata[s][next]);
                        }
                    }
                }
                int[] temp = slotdata[0];
                for (int i = 1; i < slotdata.Length; i++)
                    temp = [.. temp, .. slotdata[i]];
                list = [.. temp];
                used = CountUnique(list);
                if (used < 18) break;
            }
        }
    }
}
