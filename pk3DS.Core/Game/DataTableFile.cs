using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using pk3DS.Core.Structures;

namespace pk3DS.Core;

public enum DataTable
{
    Personal,
    Learnset,
    Move,
}

/// <summary>
/// Reads and writes a game data table as a tab-separated spreadsheet file: a header row of column names, then one row per entry.
/// </summary>
/// <remarks>
/// Columns that only describe an entry (names) are written by <see cref="Dump"/> and ignored by <see cref="Import"/>.
/// Import applies each row onto the entry's current data, so fields without a column and entries without a row are kept.
/// </remarks>
public static class DataTableFile
{
    private static readonly UTF8Encoding Utf8 = new(false);
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <summary>One spreadsheet column; <see cref="Set"/> is null for a column that is only written.</summary>
    private sealed record Column(string Name, Func<int, string> Get, Action<int, string> Set = null);

    /// <returns>Number of entries written</returns>
    public static int Dump(GameConfig config, DataTable table, string path)
    {
        var (columns, count) = GetColumns(config, table);
        var sb = new StringBuilder();
        sb.Append(string.Join('\t', columns.Select(c => c.Name))).Append('\n');
        for (int i = 0; i < count; i++)
            sb.Append(string.Join('\t', columns.Select(c => c.Get(i)))).Append('\n');
        File.WriteAllText(path, sb.ToString(), Utf8);
        return count;
    }

    /// <summary>
    /// Applies the rows of a file written by <see cref="Dump"/> and saves the table. Nothing is saved if any row is invalid.
    /// </summary>
    /// <returns>Number of entries written; 0 if nothing changed</returns>
    public static int Import(GameConfig config, DataTable table, string path)
    {
        var (columns, count) = GetColumns(config, table);
        string file = Path.GetFileName(path);
        string[] lines = File.ReadAllText(path, Utf8).Split('\n');
        int end = lines.Length;
        while (end > 0 && lines[end - 1].TrimEnd('\r').Length == 0)
            end--;
        if (end == 0)
            throw new InvalidDataException($"{file}: the file is empty.");
        CheckHeader(file, lines[0].TrimEnd('\r').Split('\t'), columns);

        try
        {
            var seen = new HashSet<int>();
            for (int l = 1; l < end; l++)
            {
                string[] cells = lines[l].TrimEnd('\r').Split('\t');
                if (cells.Length != columns.Length)
                    throw new InvalidDataException($"{file}:{l + 1}: expected {columns.Length} cells, found {cells.Length}.");
                if (!int.TryParse(cells[0], NumberStyles.None, Invariant, out int index) || index >= count)
                    throw new InvalidDataException($"{file}:{l + 1}: index: '{cells[0]}' is not a number between 0 and {count - 1}.");
                if (!seen.Add(index))
                    throw new InvalidDataException($"{file}:{l + 1}: index: {index} appears more than once.");
                for (int c = 1; c < columns.Length; c++)
                {
                    try
                    {
                        columns[c].Set?.Invoke(index, cells[c]);
                    }
                    catch (FormatException e)
                    {
                        throw new InvalidDataException($"{file}:{l + 1}: {columns[c].Name}: {e.Message}", e);
                    }
                }
            }
        }
        catch
        {
            // Drop the rows applied so far, so the loaded table matches the files again.
            Reload(config, table);
            throw;
        }
        return Save(config, table);
    }

    private static void CheckHeader(string file, string[] header, Column[] columns)
    {
        for (int c = 0; c < Math.Max(header.Length, columns.Length); c++)
        {
            string expected = c < columns.Length ? columns[c].Name : null;
            string found = c < header.Length ? header[c] : null;
            if (expected == found)
                continue;
            throw new InvalidDataException(expected == null
                ? $"{file}:1: unknown column '{found}' after the last column '{columns[^1].Name}'."
                : $"{file}:1: column {c + 1} must be '{expected}', found '{found ?? "nothing"}'.");
        }
    }

    private static (Column[] Columns, int Count) GetColumns(GameConfig config, DataTable table)
    {
        if (!config.ORAS)
            throw new NotSupportedException("Data tables are only supported for Omega Ruby / Alpha Sapphire so far.");
        return table switch
        {
            DataTable.Personal => (PersonalColumns(config), config.Personal.Table.Length),
            DataTable.Learnset => (LearnsetColumns(config), config.Learnsets.Length),
            DataTable.Move => (MoveColumns(config), config.Moves.Length),
            _ => throw new ArgumentOutOfRangeException(nameof(table)),
        };
    }

    private static void Reload(GameConfig config, DataTable table)
    {
        switch (table)
        {
            case DataTable.Personal: config.InitializePersonal(); break;
            case DataTable.Learnset: config.InitializeLearnset(); break;
            case DataTable.Move: config.InitializeMoves(); break;
        }
    }

    private static int Save(GameConfig config, DataTable table) => table switch
    {
        DataTable.Personal => config.SavePersonal(),
        DataTable.Learnset => config.SaveLearnsets(),
        DataTable.Move => config.SaveMoves(),
        _ => throw new ArgumentOutOfRangeException(nameof(table)),
    };

    private static Column[] PersonalColumns(GameConfig config)
    {
        var t = config.Personal.Table;
        var (names, forms) = EntryNames(config);
        int maxType = config.GetText(TextName.Types).Length - 1;
        int maxItem = config.Info.MaxItemID;
        int maxAbility = config.Info.MaxAbilityID;

        return
        [
            Index(),
            Text("name", i => names[i]),
            Text("form", i => forms[i].ToString(Invariant)),
            Number("hp", i => t[i].HP, (i, v) => t[i].HP = v, 0, 255),
            Number("atk", i => t[i].ATK, (i, v) => t[i].ATK = v, 0, 255),
            Number("def", i => t[i].DEF, (i, v) => t[i].DEF = v, 0, 255),
            Number("spe", i => t[i].SPE, (i, v) => t[i].SPE = v, 0, 255),
            Number("spa", i => t[i].SPA, (i, v) => t[i].SPA = v, 0, 255),
            Number("spd", i => t[i].SPD, (i, v) => t[i].SPD = v, 0, 255),
            Number("ev_hp", i => t[i].EV_HP, (i, v) => t[i].EV_HP = v, 0, 3),
            Number("ev_atk", i => t[i].EV_ATK, (i, v) => t[i].EV_ATK = v, 0, 3),
            Number("ev_def", i => t[i].EV_DEF, (i, v) => t[i].EV_DEF = v, 0, 3),
            Number("ev_spe", i => t[i].EV_SPE, (i, v) => t[i].EV_SPE = v, 0, 3),
            Number("ev_spa", i => t[i].EV_SPA, (i, v) => t[i].EV_SPA = v, 0, 3),
            Number("ev_spd", i => t[i].EV_SPD, (i, v) => t[i].EV_SPD = v, 0, 3),
            Element("type1", i => t[i].Types, (i, a) => t[i].Types = a, 0, maxType),
            Element("type2", i => t[i].Types, (i, a) => t[i].Types = a, 1, maxType),
            Number("catch_rate", i => t[i].CatchRate, (i, v) => t[i].CatchRate = v, 0, 255),
            Number("evo_stage", i => t[i].EvoStage, (i, v) => t[i].EvoStage = v, 0, 255),
            Element("item1", i => t[i].Items, (i, a) => t[i].Items = a, 0, maxItem),
            Element("item2", i => t[i].Items, (i, a) => t[i].Items = a, 1, maxItem),
            Element("item3", i => t[i].Items, (i, a) => t[i].Items = a, 2, maxItem),
            Number("gender", i => t[i].Gender, (i, v) => t[i].Gender = v, 0, 255),
            Number("hatch_cycles", i => t[i].HatchCycles, (i, v) => t[i].HatchCycles = v, 0, 255),
            Number("base_friendship", i => t[i].BaseFriendship, (i, v) => t[i].BaseFriendship = v, 0, 255),
            Number("exp_growth", i => t[i].EXPGrowth, (i, v) => t[i].EXPGrowth = v, 0, 255),
            Element("egg_group1", i => t[i].EggGroups, (i, a) => t[i].EggGroups = a, 0, 255),
            Element("egg_group2", i => t[i].EggGroups, (i, a) => t[i].EggGroups = a, 1, 255),
            Element("ability1", i => t[i].Abilities, (i, a) => t[i].Abilities = a, 0, maxAbility),
            Element("ability2", i => t[i].Abilities, (i, a) => t[i].Abilities = a, 1, maxAbility),
            Element("ability3", i => t[i].Abilities, (i, a) => t[i].Abilities = a, 2, maxAbility),
            Number("escape_rate", i => t[i].EscapeRate, (i, v) => t[i].EscapeRate = v, 0, 255),
            Text("form_stats_index", i => t[i].FormStatsIndex.ToString(Invariant)),
            Number("forme_sprite", i => t[i].FormeSprite, (i, v) => t[i].FormeSprite = v, 0, ushort.MaxValue),
            Number("forme_count", i => t[i].FormeCount, (i, v) => t[i].FormeCount = v, 0, 255),
            Number("color", i => t[i].Color, (i, v) => t[i].Color = v, 0, 0x3F),
            Number("base_exp", i => t[i].BaseEXP, (i, v) => t[i].BaseEXP = v, 0, ushort.MaxValue),
            // Shown in metres and kilograms as PersonalEditor6 shows them; stored as hundredths and tenths.
            Scaled("height", i => t[i].Height, (i, v) => t[i].Height = v, 100, "00.00"),
            Scaled("weight", i => t[i].Weight, (i, v) => t[i].Weight = v, 10, "000.0"),
            Bits("tmhm", i => t[i].TMHM),
            Bits("type_tutors", i => t[i].TypeTutors),
            Bits("special_tutors1", i => t[i].SpecialTutors[0]),
            Bits("special_tutors2", i => t[i].SpecialTutors[1]),
            Bits("special_tutors3", i => t[i].SpecialTutors[2]),
            Bits("special_tutors4", i => t[i].SpecialTutors[3]),
        ];
    }

    private static Column[] LearnsetColumns(GameConfig config)
    {
        var t = config.Learnsets;
        var (names, forms) = EntryNames(config);
        string[] moveNames = config.GetText(TextName.MoveNames);
        int maxMove = config.Info.MaxMoveID;

        return
        [
            Index(),
            Text("name", i => names[i]),
            Text("form", i => forms[i].ToString(Invariant)),
            // level:move pairs. Levels and moves are limited as in LevelUpEditor6, which clamped levels to 1-100 and dropped move 0.
            new("moves", i => string.Join(' ', t[i].Moves.Select((m, n) => $"{t[i].Levels[n]}:{m}")), (i, s) =>
            {
                int[][] pairs = [.. s.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(p => ParsePair(p, 1, 100, 1, maxMove))];
                for (int n = 1; n < pairs.Length; n++)
                {
                    if (pairs[n][0] < pairs[n - 1][0])
                        throw new FormatException($"levels must not go down ('{pairs[n - 1][0]}:{pairs[n - 1][1]}' comes before '{pairs[n][0]}:{pairs[n][1]}').");
                }
                t[i].Levels = [.. pairs.Select(z => z[0])];
                t[i].Moves = [.. pairs.Select(z => z[1])];
            }),
            Text("move_names", i => string.Join(", ", t[i].Moves.Select((m, n) => $"{t[i].Levels[n]}:{moveNames[m]}"))),
        ];
    }

    private static Column[] MoveColumns(GameConfig config)
    {
        var t = config.Moves.Cast<Move6>().ToArray();
        string[] names = config.GetText(TextName.MoveNames);
        int maxType = config.GetText(TextName.Types).Length - 1;

        // Signed where MoveEditor6 reads the byte as signed: priority, recoil, stat stages and the inflicted condition (-1 is "Special").
        return
        [
            Index(),
            Text("name", i => names[i]),
            Number("type", i => t[i].Type, (i, v) => t[i].Type = v, 0, maxType),
            Number("quality", i => t[i].Quality, (i, v) => t[i].Quality = v, 0, 255),
            Number("category", i => t[i].Category, (i, v) => t[i].Category = v, 0, 255),
            Number("power", i => t[i].Power, (i, v) => t[i].Power = v, 0, 255),
            Number("accuracy", i => t[i].Accuracy, (i, v) => t[i].Accuracy = v, 0, 255),
            Number("pp", i => t[i].PP, (i, v) => t[i].PP = v, 0, 255),
            Number("priority", i => (sbyte)t[i].Priority, (i, v) => t[i].Priority = v, sbyte.MinValue, sbyte.MaxValue),
            Number("hit_min", i => t[i].HitMin, (i, v) => t[i].HitMin = v, 0, 15),
            Number("hit_max", i => t[i].HitMax, (i, v) => t[i].HitMax = v, 0, 15),
            Number("inflict", i => (short)t[i].Inflict, (i, v) => t[i].Inflict = v, short.MinValue, short.MaxValue),
            Number("inflict_percent", i => t[i].InflictPercent, (i, v) => t[i].InflictPercent = v, 0, 255),
            Number("inflict_count", i => (int)t[i].InflictCount, (i, v) => t[i].InflictCount = (MoveInflictDuration)v, 0, 255),
            Number("turn_min", i => t[i].TurnMin, (i, v) => t[i].TurnMin = v, 0, 255),
            Number("turn_max", i => t[i].TurnMax, (i, v) => t[i].TurnMax = v, 0, 255),
            Number("crit_stage", i => t[i].CritStage, (i, v) => t[i].CritStage = v, 0, 255),
            Number("flinch", i => t[i].Flinch, (i, v) => t[i].Flinch = v, 0, 255),
            Number("effect_sequence", i => t[i].EffectSequence, (i, v) => t[i].EffectSequence = v, 0, ushort.MaxValue),
            Number("recoil", i => (sbyte)t[i].Recoil, (i, v) => t[i].Recoil = v, sbyte.MinValue, sbyte.MaxValue),
            Number("healing", i => (int)t[i].Healing, (i, v) => t[i].Healing = (Heal)v, 0, 255),
            Number("target", i => (int)t[i].Target, (i, v) => t[i].Target = (MoveTarget)v, 0, 255),
            Number("stat1", i => t[i].Stat1, (i, v) => t[i].Stat1 = v, 0, 255),
            Number("stat2", i => t[i].Stat2, (i, v) => t[i].Stat2 = v, 0, 255),
            Number("stat3", i => t[i].Stat3, (i, v) => t[i].Stat3 = v, 0, 255),
            Number("stat1_stage", i => (sbyte)t[i].Stat1Stage, (i, v) => t[i].Stat1Stage = v, sbyte.MinValue, sbyte.MaxValue),
            Number("stat2_stage", i => (sbyte)t[i].Stat2Stage, (i, v) => t[i].Stat2Stage = v, sbyte.MinValue, sbyte.MaxValue),
            Number("stat3_stage", i => (sbyte)t[i].Stat3Stage, (i, v) => t[i].Stat3Stage = v, sbyte.MinValue, sbyte.MaxValue),
            Number("stat1_percent", i => t[i].Stat1Percent, (i, v) => t[i].Stat1Percent = v, 0, 255),
            Number("stat2_percent", i => t[i].Stat2Percent, (i, v) => t[i].Stat2Percent = v, 0, 255),
            Number("stat3_percent", i => t[i].Stat3Percent, (i, v) => t[i].Stat3Percent = v, 0, 255),
            new("flags", i => string.Join(' ', Enumerable.Range(0, 32).Where(n => (((uint)t[i].Flags >> n) & 1) != 0)), (i, s) =>
            {
                int[] set = ParseList(s, 0, 31);
                if (set.Distinct().Count() != set.Length)
                    throw new FormatException($"'{s}' lists a flag more than once.");
                t[i].Flags = (MoveFlag6)set.Aggregate(0u, (f, n) => f | 1u << n);
            }),
        ];
    }

    /// <summary>
    /// Names of the personal table entries as the upstream editors list them (forms as "Species N"), and each entry's form number.
    /// </summary>
    private static (string[] Names, int[] Forms) EntryNames(GameConfig config)
    {
        string[] species = config.GetText(TextName.SpeciesNames);
        string[][] altForms = config.Personal.GetFormList(species, config.MaxSpeciesID);
        string[] names = config.Personal.GetPersonalEntryList(altForms, species, config.MaxSpeciesID, out _, out int[] forms);
        return (names, forms);
    }

    private static Column Index() => new("index", i => i.ToString(Invariant));

    private static Column Text(string name, Func<int, string> get) => new(name, i => get(i) ?? string.Empty);

    private static Column Number(string name, Func<int, int> get, Action<int, int> set, int min, int max) =>
        new(name, i => get(i).ToString(Invariant), (i, s) => set(i, ParseNumber(s, min, max)));

    /// <summary>One element of an array property, such as the second type.</summary>
    private static Column Element(string name, Func<int, int[]> get, Action<int, int[]> set, int element, int max) =>
        new(name, i => get(i)[element].ToString(Invariant), (i, s) =>
        {
            int[] values = get(i);
            values[element] = ParseNumber(s, 0, max);
            set(i, values);
        });

    /// <summary>A whole number shown divided by <paramref name="scale"/> (a power of ten).</summary>
    private static Column Scaled(string name, Func<int, int> get, Action<int, int> set, int scale, string format) =>
        new(name, i => ((decimal)get(i) / scale).ToString(format, Invariant), (i, s) =>
        {
            if (!decimal.TryParse(s, NumberStyles.AllowDecimalPoint, Invariant, out decimal d))
                throw new FormatException($"'{s}' is not a number.");
            decimal v = d * scale;
            if (v != decimal.Truncate(v))
                throw new FormatException($"'{s}' has more than {format.Length - format.IndexOf('.') - 1} decimal place(s).");
            if (v > ushort.MaxValue)
                throw new FormatException($"'{s}' is above the maximum {(decimal)ushort.MaxValue / scale}.");
            set(i, (int)v);
        });

    /// <summary>A set of flags, listed as the indexes of the set ones.</summary>
    private static Column Bits(string name, Func<int, bool[]> get) =>
        new(name, i => string.Join(' ', get(i).Select((b, n) => b ? n : -1).Where(n => n >= 0)), (i, s) =>
        {
            bool[] bits = get(i);
            int[] set = ParseList(s, 0, bits.Length - 1);
            if (set.Distinct().Count() != set.Length)
                throw new FormatException($"'{s}' lists a flag more than once.");
            Array.Clear(bits);
            foreach (int n in set)
                bits[n] = true;
        });

    private static int ParseNumber(string s, int min, int max)
    {
        if (!int.TryParse(s, NumberStyles.AllowLeadingSign, Invariant, out int v))
            throw new FormatException($"'{s}' is not a whole number.");
        if (v < min || v > max)
            throw new FormatException($"{v} is not between {min} and {max}.");
        return v;
    }

    /// <summary>Parses "a:b" into two numbers.</summary>
    private static int[] ParsePair(string s, int minA, int maxA, int minB, int maxB)
    {
        string[] parts = s.Split(':');
        if (parts.Length != 2)
            throw new FormatException($"'{s}' is not two numbers joined by ':'.");
        return [ParseNumber(parts[0], minA, maxA), ParseNumber(parts[1], minB, maxB)];
    }

    private static int[] ParseList(string s, int min, int max) =>
        [.. s.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(z => ParseNumber(z, min, max))];
}
