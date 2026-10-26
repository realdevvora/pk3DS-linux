using System;
using System.IO;
using System.Linq;
using System.Text;
using pk3DS.Core;
using pk3DS.Core.CTR;
using pk3DS.Core.Randomizers;
using pk3DS.Core.Structures;

namespace pk3DS.Cli;

/// <summary>
/// Maps each command-line operation onto the corresponding <c>pk3DS.Core</c> call.
/// </summary>
public static class Commands
{
    private static readonly string[] NoOptions = [];

    private static readonly UTF8Encoding Utf8 = new(false);

    public static readonly Command[] All =
    [
        new("ncsd", "extract", "<rom.3ds> <outDir>", "Extract exheader, exefs, romfs and more from a .3ds cartridge dump",
            NoOptions, a =>
            {
                var (rom, outDir) = Two(a);
                new NCSD().ExtractFilesFromNCSD(rom, outDir, new ConsoleProgressSink());
                return true;
            }),

        new("ncch", "extract", "<game.cxi> <outDir>", "Extract exheader, exefs and romfs from a CXI",
            NoOptions, a =>
            {
                var (cxi, outDir) = Two(a);
                new NCCH().ExtractNCCHFromFile(cxi, outDir, new ConsoleProgressSink());
                return true;
            }),

        new("romfs", "extract", "<romfs.bin> <outDir>", "Extract a RomFS image to a directory",
            NoOptions, a =>
            {
                var (romfs, outDir) = Two(a);
                new RomFS(romfs).ExtractRomFS(outDir, new ConsoleProgressSink());
                return true;
            }),

        new("romfs", "build", "<inDir> <out.bin>", "Build a RomFS image from a directory",
            NoOptions, a =>
            {
                var (inDir, outFile) = Two(a);
                RomFS.BuildRomFS(inDir, outFile, new ConsoleProgressSink());
                return true;
            }),

        new("exefs", "unpack", "<exefs.bin> <outDir>", "Unpack an ExeFS image to a directory of .bin files",
            NoOptions, a =>
            {
                var (exefs, outDir) = Two(a);
                return ExeFS.UnpackExeFS(exefs, outDir);
            }),

        new("exefs", "pack", "<inDir> <out.bin>", "Pack up to 10 files of a folder (in name order) into an ExeFS image",
            NoOptions, a =>
            {
                var (inDir, outFile) = Two(a);
                if (!Directory.Exists(inDir))
                    throw new DirectoryNotFoundException($"Folder '{inDir}' does not exist.");
                string[] files = [.. Directory.GetFiles(inDir)
                    .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
                    .ThenBy(f => Path.GetFileName(f), StringComparer.Ordinal)];
                return ExeFS.PackExeFS(files, outFile);
            }),

        new("garc", "unpack", "<file.garc> <outDir> [--no-decompress]", "Unpack a GARC archive to numbered files (LZSS-compressed entries are written as dec_*.bin)",
            NoOptions, a =>
            {
                var (garc, outDir) = Two(a);
                a.RejectUnknown("no-decompress");
                int count = GARC.GarcUnpack(garc, outDir, a.Has("no-decompress"), new ConsoleProgressSink());
                Console.WriteLine($"{count} files");
                return true;
            }),

        new("garc", "pack", "<inDir> <out.garc> --version <4|6> [--padding <bytes>]", "Pack numbered files into a GARC (version 4 for X/Y and ORAS, 6 for Sun/Moon and later)",
            ["version", "padding"], a =>
            {
                var (inDir, garc) = Two(a);
                a.RejectUnknown("version", "padding");
                int version = a.Require("version") switch
                {
                    "4" => GARC.VER_4,
                    "6" => GARC.VER_6,
                    var v => throw new UsageException($"Unknown GARC version '{v}'; use 4 or 6."),
                };
                int padding = 4;
                if (a.Get("padding") is { } p && (!int.TryParse(p, out padding) || padding < 1))
                    throw new UsageException($"Invalid padding '{p}'.");
                int count = GARC.PackGARC(inDir, garc, version, padding, new ConsoleProgressSink());
                Console.WriteLine($"{count} files");
                return true;
            }),

        new("mini", "unpack", "<file> <outDir>", "Unpack a mini container to numbered files (0000.bin, ...) and print its two-letter identifier",
            NoOptions, a =>
            {
                var (file, outDir) = Two(a);
                byte[] data = File.ReadAllBytes(file);
                string ident = MiniIdentifier(data);
                byte[][] files = Mini.UnpackMini(data, ident) ?? throw new InvalidDataException("Not a valid mini container.");
                Directory.CreateDirectory(outDir);
                for (int i = 0; i < files.Length; i++)
                    File.WriteAllBytes(Path.Combine(outDir, $"{i:D4}.bin"), files[i]);
                Console.WriteLine($"{ident} {files.Length} files header={BitConverter.ToInt32(data, 4)}");
                return true;
            }),

        new("mini", "pack", "<inDir> <out> --ident <XX> [--header-size <bytes>]", "Pack the files of a folder (in name order) into a mini container; pass the header size printed by 'mini unpack' to rebuild containers with a padded header",
            ["ident", "header-size"], a =>
            {
                var (inDir, outFile) = Two(a);
                a.RejectUnknown("ident", "header-size");
                string ident = a.Require("ident");
                if (ident.Length != 2 || ident.Any(c => c > 0x7F))
                    throw new UsageException("--ident must be two ASCII characters.");
                byte[][] files = [.. Directory.GetFiles(inDir).OrderBy(f => Path.GetFileName(f), StringComparer.Ordinal).Select(File.ReadAllBytes)];
                byte[] packed = Mini.PackMini(files, ident);
                if (a.Get("header-size") is { } h)
                {
                    if (!int.TryParse(h, out int headerSize) || headerSize < 8 + (4 * files.Length) || headerSize % 4 != 0)
                        throw new UsageException($"Invalid header size '{h}'; it must be a multiple of 4 and at least {8 + (4 * files.Length)}.");
                    if (headerSize != 8 + (4 * files.Length))
                        packed = Mini.AdjustMiniHeader(packed, headerSize);
                }
                File.WriteAllBytes(outFile, packed);
                Console.WriteLine($"{files.Length} files");
                return true;
            }),

        new("game", "info", GameOptions.Synopsis, "Detect the game in an extracted RomFS/ExeFS and print its basic data",
            GameOptions.ValueOptions, a =>
            {
                a.Expect(0);
                a.RejectUnknown(GameOptions.ValueOptions);
                var config = GameOptions.Load(a);
                Console.WriteLine($"version {config.Version}");
                Console.WriteLine($"generation {config.Generation}");
                Console.WriteLine($"language {config.Language}");
                Console.WriteLine($"species {config.Info.MaxSpeciesID}");
                Console.WriteLine($"moves {config.Info.MaxMoveID}");
                Console.WriteLine($"items {config.Info.MaxItemID}");
                Console.WriteLine($"abilities {config.Info.MaxAbilityID}");
                Console.WriteLine($"text files {config.GameTextStrings.Length}");
                return true;
            }),

        new("text", "dump", $"{GameOptions.Synopsis} [--story] <outDir>", "Write each game text file (or story text file) to <outDir>/NNNN.txt, one line per string",
            GameOptions.ValueOptions, a =>
            {
                string[] args = a.Expect(1);
                a.RejectUnknown([.. GameOptions.ValueOptions, "story"]);
                string[][] text = GameText(GameOptions.Load(a), a.Has("story")).Text;
                Directory.CreateDirectory(args[0]);
                for (int i = 0; i < text.Length; i++)
                    File.WriteAllText(Path.Combine(args[0], $"{i:D4}.txt"), string.Concat(text[i].Select(line => line + "\n")), Utf8);
                Console.WriteLine($"{text.Length} files");
                return true;
            }),

        new("text", "import", $"{GameOptions.Synopsis} [--story] <inDir>", "Write NNNN.txt files from <inDir> back into the game text (or story text) archive",
            GameOptions.ValueOptions, a =>
            {
                string[] args = a.Expect(1);
                a.RejectUnknown([.. GameOptions.ValueOptions, "story"]);
                var (text, save) = GameText(GameOptions.Load(a), a.Has("story"));
                foreach (string path in Directory.GetFiles(args[0], "*.txt"))
                {
                    if (!int.TryParse(Path.GetFileNameWithoutExtension(path), out int index) || index < 0 || index >= text.Length)
                        throw new InvalidDataException($"'{Path.GetFileName(path)}' is not a text file number between 0 and {text.Length - 1}.");
                    string content = File.ReadAllText(path, Utf8);
                    text[index] = content.Length == 0 ? [] : content.EndsWith('\n') ? content[..^1].Split('\n') : throw new InvalidDataException($"'{Path.GetFileName(path)}' must end with a newline.");
                }
                Console.WriteLine($"{save()} files changed");
                return true;
            }),

        .. DataCommands("personal", DataTable.Personal, "species stats, types, abilities, items and TM/tutor flags"),
        .. DataCommands("learnset", DataTable.Learnset, "level-up moves"),
        .. DataCommands("move", DataTable.Move, "move data (type, power, accuracy, PP, effects, flags)"),
        .. DataCommands("evolution", DataTable.Evolution, "evolution methods"),
        .. DataCommands("eggmove", DataTable.EggMove, "egg moves"),
        .. DataCommands("item", DataTable.Item, "item data (prices, effects, pockets)"),
        .. DataCommands("megaevo", DataTable.MegaEvo, "Mega Evolutions (form and Mega Stone)"),
        .. DataCommands("trainer", DataTable.Trainer, "trainer data (class, items, AI, prize; one row per trainer)"),
        .. DataCommands("trainerpoke", DataTable.TrainerPokemon, "trainer teams (one row per team slot)"),
        .. DataCommands("maisontrainer", DataTable.MaisonTrainer, "Battle Maison trainers and the Pokémon each picks from", DataTable.SuperMaisonTrainer),
        .. DataCommands("maisonpoke", DataTable.MaisonPokemon, "the Battle Maison Pokémon pool", DataTable.SuperMaisonPokemon),
        .. DataCommands("tmhm", DataTable.TMHM, "the moves taught by TMs and HMs (needs a decompressed code.bin)"),
        .. DataCommands("mart", DataTable.Mart, "Poké Mart inventories (needs a decompressed code.bin)"),
        .. DataCommands("pickup", DataTable.Pickup, "the items the Pickup ability finds (needs a decompressed code.bin)"),
        .. DataCommands("opower", DataTable.OPower, "O-Power costs and effects (needs a decompressed code.bin)"),

        .. DataCommands("static", DataTable.StaticEncounter, "static encounters: legendaries and other fixed Pokémon (DllField.cro)"),
        .. DataCommands("gift", DataTable.Gift, "gift Pokémon, including the starters (DllField.cro)"),
        .. DataCommands("starter", DataTable.Starter, "the starter choices (DllPoke3Select.cro and their gifts in DllField.cro)"),
        .. DataCommands("typechart", DataTable.TypeChart, "the type effectiveness chart (DllBattle.cro)"),
        .. DataCommands("wild", DataTable.Wild, "Omega Ruby / Alpha Sapphire wild encounters (one row per slot)"),

        Randomize<PersonalRandomizerOptions>("personal", "Randomize species data (stats, types, abilities, items, TM/tutor compatibility)", TableRandomizers.Personal),
        Randomize<LearnsetRandomizerOptions>("learnset", "Randomize level-up moves", TableRandomizers.Learnset),
        Randomize<EggMoveRandomizerOptions>("eggmove", "Randomize egg moves", TableRandomizers.EggMoves),
        Randomize<EvolutionRandomizerOptions>("evolution", "Randomize the species each Pokémon evolves into (methods stay the same)", TableRandomizers.Evolutions),
        Randomize<EvolutionRandomizerOptions>("evolve-every-level", "Make every Pokémon evolve into a random species at every level up", TableRandomizers.EvolveEveryLevel),
        Randomize<NoRandomizerOptions>("trade-evolutions", "Replace trade evolutions with level-up evolutions (not random)", (c, _) => TableRandomizers.RemoveTradeEvolutions(c)),
        Randomize<MoveRandomizerOptions>("move", "Randomize move types and/or damage categories", TableRandomizers.Moves),
        Randomize<TrainerRandomizerOptions>("trainer", "Randomize trainers: teams, moves, items, classes, AI and prizes", TableRandomizers.Trainers),
        Randomize<WildRandomizerOptions>("wild", "Randomize Omega Ruby / Alpha Sapphire wild encounters", TableRandomizers.Wild),

        new("shiny", "info", GameOptions.Synopsis, "Show the shiny rate patch in code.bin (needs a decompressed code.bin)",
            GameOptions.ValueOptions, a =>
            {
                a.Expect(0);
                a.RejectUnknown(GameOptions.ValueOptions);
                var shiny = new ShinyRate6(CodeBin.Load(GameOptions.Load(a).ExeFS));
                Console.WriteLine(shiny.IsOriginal ? "rerolls original (the game's own count)"
                    : shiny.Rerolls is { } r ? $"rerolls {r} (~{ShinyRate6.Chance(r):P} per Pokémon)"
                    : "rerolls patched with an unknown instruction");
                Console.WriteLine($"always shiny {shiny.AlwaysShiny switch { true => "yes", false => "no", null => "not available" }}");
                return true;
            }),

        new("shiny", "set", $"{GameOptions.Synopsis} [--rerolls <count> | --original] [--always yes|no]", "Patch the shiny rate in code.bin: a fixed PID reroll count, or the game's own; optionally make every Pokémon shiny",
            [.. GameOptions.ValueOptions, "rerolls", "always"], a =>
            {
                a.Expect(0);
                a.RejectUnknown([.. GameOptions.ValueOptions, "rerolls", "original", "always"]);
                if (a.Has("rerolls") && a.Has("original"))
                    throw new UsageException("Use either --rerolls or --original.");
                if (!a.Has("rerolls") && !a.Has("original") && !a.Has("always"))
                    throw new UsageException("Nothing to change: give --rerolls, --original or --always.");
                var code = CodeBin.Load(GameOptions.Load(a).ExeFS);
                var shiny = new ShinyRate6(code);
                if (a.Get("rerolls") is { } value)
                {
                    if (!int.TryParse(value, out int rerolls) || rerolls < 1)
                        throw new UsageException($"Invalid reroll count '{value}'.");
                    int written = shiny.SetRerolls(rerolls);
                    Console.WriteLine(written == rerolls ? $"rerolls {written}" : $"rerolls {written} (raised from {rerolls}, the next count the patch can encode)");
                }
                if (a.Has("original"))
                {
                    shiny.Restore();
                    Console.WriteLine("rerolls original");
                }
                if (a.Get("always") is { } always)
                {
                    shiny.AlwaysShiny = always switch
                    {
                        "yes" => true,
                        "no" => false,
                        _ => throw new UsageException($"--always takes yes or no, not '{always}'."),
                    };
                    Console.WriteLine($"always shiny {always}");
                }
                Console.WriteLine(code.Save() ? "code.bin written" : "code.bin unchanged");
                return true;
            }),

        new("blz", "decode", "<file>...", "Decompress bottom-LZ files in place",
            NoOptions, a => Blz(a, "-d")),

        new("blz", "encode", "<file>... [--best] [--arm9]", "Compress files with bottom-LZ in place",
            NoOptions, a => Blz(a, "-e" + (a.Has("best") ? "o" : "n") + (a.Has("arm9") ? "9" : ""))),

        new("lzss", "decompress", "<in> <out>", "Decompress an LZSS (LZ10/LZ11) file",
            NoOptions, a =>
            {
                var (inFile, outFile) = Two(a);
                long size = LZSS.Decompress(inFile, outFile);
                Console.WriteLine($"{size} bytes");
                return true;
            }),

        new("lzss", "compress", "<in> <out>", "Compress a file with LZSS (LZ11)",
            NoOptions, a =>
            {
                var (inFile, outFile) = Two(a);
                int size = LZSS.Compress(inFile, outFile);
                Console.WriteLine($"{size} bytes");
                return true;
            }),

        new("cro", "hash", "<crr> <croDir> [--no-save-cro] [--no-save-crr]", "Recompute CRO hashes and write them into the CRR",
            NoOptions, a =>
            {
                var (crr, croDir) = Two(a);
                a.RejectUnknown("no-save-cro", "no-save-crr");
                return CRO.E_HashCRR(crr, croDir, !a.Has("no-save-cro"), !a.Has("no-save-crr"), new ConsoleProgressSink());
            }),

        new("rom", "build", "--exefs <path> --romfs <path> --exheader <file> --serial <id> --out <file> [--logo <name>] [--card2] [--trimmed]",
            "Assemble a .3ds from ExeFS, RomFS and exheader (logo: Nintendo, Licensed, Distributed, iQue, iQueForSystem)",
            ["exefs", "romfs", "exheader", "serial", "out", "logo"], a =>
            {
                a.Expect(0);
                a.RejectUnknown("exefs", "romfs", "exheader", "serial", "out", "logo", "card2", "trimmed");
                string exefs = a.Require("exefs");
                string romfs = a.Require("romfs");
                string exheader = a.Require("exheader");
                string serial = a.Require("serial");
                string output = a.Require("out");
                string logo = a.Get("logo") ?? "Nintendo";
                if (!IsLogo(logo))
                    throw new UsageException($"Unknown logo '{logo}'.");
                return CTRUtil.BuildROM(a.Has("card2"), logo, exefs, romfs, exheader, serial, output, a.Has("trimmed"), new ConsoleProgressSink());
            }),
    ];

    /// <param name="super">With --super, the table to use instead (the Super Battle Maison); null if the command has no --super</param>
    private static Command[] DataCommands(string group, DataTable table, string what, DataTable? super = null) =>
    [
        new(group, "dump", $"{GameOptions.Synopsis}{(super == null ? "" : " [--super]")} <out.tsv>", $"Write {what} to a tab-separated spreadsheet file",
            GameOptions.ValueOptions, a =>
            {
                string[] args = a.Expect(1);
                a.RejectUnknown(super == null ? GameOptions.ValueOptions : [.. GameOptions.ValueOptions, "super"]);
                int count = DataTableFile.Dump(GameOptions.Load(a), a.Has("super") ? super.Value : table, args[0]);
                Console.WriteLine($"{count} entries");
                return true;
            }),

        new(group, "import", $"{GameOptions.Synopsis}{(super == null ? "" : " [--super]")} <in.tsv>", $"Write edited rows of a '{group} dump' file back into the game",
            GameOptions.ValueOptions, a =>
            {
                string[] args = a.Expect(1);
                a.RejectUnknown(super == null ? GameOptions.ValueOptions : [.. GameOptions.ValueOptions, "super"]);
                int changed = DataTableFile.Import(GameOptions.Load(a), a.Has("super") ? super.Value : table, args[0]);
                Console.WriteLine($"{changed} entries changed");
                return true;
            }),
    ];

    /// <summary>The loaded game text or story text, and the method that saves it.</summary>
    private static (string[][] Text, Func<int> Save) GameText(GameConfig config, bool story)
    {
        if (!story)
            return (config.GameTextStrings, config.SaveGameText);
        config.InitializeStoryText();
        return (config.StoryTextStrings, config.SaveStoryText);
    }

    /// <summary>A randomizer without options.</summary>
    private sealed record NoRandomizerOptions;

    /// <summary>
    /// A 'randomize' command. Every property of <typeparamref name="T"/> becomes an option (--kebab-case, yes/no or a number) whose default is
    /// the property's, which is the upstream editor's.
    /// </summary>
    private static Command Randomize<T>(string name, string summary, Func<GameConfig, T, int> run) where T : new()
    {
        var properties = typeof(T).GetProperties().Where(p => p.CanWrite && p.Name != "EqualityContract").ToArray();
        string[] names = [.. properties.Select(p => KebabCase(p.Name))];
        var defaults = new T();
        string usage = string.Join(' ', [GameOptions.Synopsis, "[--seed <n>]",
            .. properties.Select((p, i) => $"[--{names[i]} {OptionValues(p.PropertyType)} ({FormatOption(p.GetValue(defaults))})]")]);
        string[] valueOptions = [.. GameOptions.ValueOptions, "seed", .. names];
        return new("randomize", name, usage, summary, valueOptions, a =>
        {
            a.Expect(0);
            a.RejectUnknown(valueOptions);
            var options = new T();
            for (int i = 0; i < properties.Length; i++)
            {
                if (a.Get(names[i]) is { } value)
                    properties[i].SetValue(options, ParseOption(names[i], properties[i].PropertyType, value));
            }
            var config = GameOptions.Load(a);
            if (a.Get("seed") is { } seed)
                Util.ReseedRand(int.TryParse(seed, out int s) ? s : throw new UsageException($"Invalid seed '{seed}'."));
            Console.WriteLine($"{run(config, options)} entries changed");
            return true;
        });
    }

    private static object ParseOption(string name, Type type, string value)
    {
        if (type == typeof(bool))
        {
            return value switch
            {
                "yes" => true,
                "no" => false,
                _ => throw new UsageException($"--{name} takes yes or no, not '{value}'."),
            };
        }
        if (type.IsEnum && Enum.GetNames(type).FirstOrDefault(n => KebabCase(n) == value) is { } member)
            return Enum.Parse(type, member);
        if (type == typeof(int) && int.TryParse(value, out int i) && i >= 0)
            return i;
        if (type == typeof(decimal) && decimal.TryParse(value, System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture, out decimal d))
            return d;
        throw new UsageException($"Invalid value '{value}' for --{name}.");
    }

    private static string OptionValues(Type type) =>
        type == typeof(bool) ? "yes|no" : type.IsEnum ? string.Join('|', Enum.GetNames(type).Select(KebabCase)) : "<n>";

    private static string FormatOption(object value) => value switch
    {
        bool b => b ? "yes" : "no",
        Enum e => KebabCase(e.ToString()),
        _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture),
    };

    /// <summary>StatHp -> stat-hp, STABPercent -> stab-percent, FourMovesAtLevel1 -> four-moves-at-level-1.</summary>
    private static string KebabCase(string name)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            bool boundary = i > 0 && ((char.IsUpper(c) && (char.IsLower(name[i - 1]) || char.IsDigit(name[i - 1])))
                || (char.IsUpper(c) && char.IsUpper(name[i - 1]) && i + 1 < name.Length && char.IsLower(name[i + 1]))
                || (char.IsDigit(c) && char.IsLetter(name[i - 1])));
            if (boundary)
                sb.Append('-');
            sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }

    private static (string First, string Second) Two(CommandArgs a)
    {
        string[] args = a.Expect(2);
        return (args[0], args[1]);
    }

    private static bool Blz(CommandArgs a, string mode)
    {
        string[] files = a.ExpectAtLeast(1);
        foreach (string file in files)
        {
            if (!File.Exists(file))
                throw new FileNotFoundException("File not found.", file);
        }
        _ = new BLZCoder([mode, .. files]);
        return true;
    }

    private static string MiniIdentifier(byte[] data)
    {
        if (data.Length < 4 || data[0] is < 0x20 or > 0x7E || data[1] is < 0x20 or > 0x7E)
            throw new InvalidDataException("Not a valid mini container.");
        return new string([(char)data[0], (char)data[1]]);
    }

    private static bool IsLogo(string name) =>
        name is "Nintendo" or "Licensed" or "Distributed" or "iQue" or "iQueForSystem";
}
