using System;
using System.IO;
using System.Linq;
using pk3DS.Core.CTR;
using pk3DS.Core.Structures.PersonalInfo;
using pk3DS.Core.Structures;

namespace pk3DS.Core;

public class GameConfig
{
    private const int FILECOUNT_XY = 271;
    private const int FILECOUNT_ORASDEMO = 301;
    private const int FILECOUNT_ORAS = 299;
    private const int FILECOUNT_SMDEMO = 239;
    private const int FILECOUNT_SM = 311;
    private const int FILECOUNT_USUM = 333;
    public readonly GameVersion Version = GameVersion.Invalid;

    public GARCReference[] Files { get; private set; }
    public TextVariableCode[] Variables { get; private set; }
    public TextReference[] GameText { get; private set; }
    public GameInfo Info { get; private set; }

    /// <summary>
    /// Whether or not to remap characters in text files to proper unicode. Defaults to false.
    /// </summary>
    /// <remarks>
    /// This will enable the display of ♂ and ♀, but may interfere with text editing. Set this to true if you're not going to change any text, otherwise set it to false.
    /// </remarks>
    public bool RemapCharacters { get; set; } = false;

    public GameConfig(int fileCount)
    {
        GameVersion game = fileCount switch
        {
            FILECOUNT_XY => GameVersion.XY,
            FILECOUNT_ORASDEMO => GameVersion.ORASDEMO,
            FILECOUNT_ORAS => GameVersion.ORAS,
            FILECOUNT_SMDEMO => GameVersion.SMDEMO,
            FILECOUNT_SM => GameVersion.SM,
            FILECOUNT_USUM => GameVersion.USUM,
            _ => GameVersion.Invalid,
        };
        if (game == GameVersion.Invalid)
            return;

        Version = game;
    }

    public GameConfig(GameVersion game)
    {
        Version = game;
    }

    private void GetGameData(GameVersion game)
    {
        switch (game)
        {
            case GameVersion.XY:
                Files = GARCReference.GARCReference_XY;
                Variables = TextVariableCode.VariableCodes_XY;
                GameText = TextReference.GameText_XY;
                break;

            case GameVersion.ORASDEMO:
            case GameVersion.ORAS:
                Files = GARCReference.GARCReference_AO;
                Variables = TextVariableCode.VariableCodes_AO;
                GameText = TextReference.GameText_AO;
                break;

            case GameVersion.SMDEMO:
                Files = GARCReference.GARCReference_SMDEMO;
                Variables = TextVariableCode.VariableCodes_SM;
                GameText = TextReference.GameText_SMDEMO;
                break;
            case GameVersion.SN:
            case GameVersion.MN:
            case GameVersion.SM:
                Files = GARCReference.GARCReference_SN;
                if (new FileInfo(Path.Combine(RomFS, GetGARCFileName("encdata"))).Length == 0)
                    Files = GARCReference.GARCReference_MN;
                Variables = TextVariableCode.VariableCodes_SM;
                GameText = TextReference.GameText_SM;
                break;
            case GameVersion.US:
            case GameVersion.UM:
            case GameVersion.USUM:
                Files = GARCReference.GARCReference_US;
                if (new FileInfo(Path.Combine(RomFS, GetGARCFileName("encdata"))).Length == 0)
                    Files = GARCReference.GARCReference_UM;
                Variables = TextVariableCode.VariableCodes_SM;
                GameText = TextReference.GameText_USUM;
                break;
        }
    }

    /// <summary>
    /// Opens an extracted game: detects which game the RomFS holds from its number of data files, as pk3DS does, and loads it.
    /// </summary>
    /// <param name="romfs">Extracted RomFS folder (with the 'a' directory)</param>
    /// <param name="exefs">Extracted ExeFS folder</param>
    /// <param name="lang">Text language, 0-9 (2 is English)</param>
    /// <exception cref="ArgumentException">The folders are not an extracted game, the game is not recognized, or the language is not available.</exception>
    public static GameConfig Open(string romfs, string exefs, int lang)
    {
        if (!Directory.Exists(Path.Combine(romfs, "a")))
            throw new ArgumentException($"'{romfs}' is not a RomFS folder (no 'a' directory).");
        if (!Directory.Exists(exefs))
            throw new ArgumentException($"ExeFS folder '{exefs}' does not exist.");
        if (lang is < 0 or > 9)
            throw new ArgumentException($"Invalid language '{lang}'; use 0-9 (2 is English).");

        // Games are told apart by how many numbered files the 'a' tree holds.
        int fileCount = Directory.GetFiles(Path.Combine(romfs, "a"), "*", SearchOption.AllDirectories)
            .Count(f => Path.GetFileName(f).Length == 1);
        var config = new GameConfig(fileCount);
        if (config.Version == GameVersion.Invalid)
            throw new ArgumentException($"Unrecognized game: found {fileCount} data files in '{Path.Combine(romfs, "a")}'.");
        if ((config.XY || config.ORAS) && lang > 7)
            throw new ArgumentException("Languages above 7 are not available in this game.");

        config.Initialize(romfs, exefs, lang);
        return config;
    }

    public void Initialize(string romFSpath, string exeFSpath, int lang)
    {
        RomFS = romFSpath;
        ExeFS = exeFSpath;
        Language = lang;
        GetGameData(Version);
        InitializeAll();
    }

    public void InitializeAll()
    {
        InitializePersonal();
        InitializeLearnset();
        InitializeGameText();
        InitializeMoves();
        InitializeEvos();
        InitializeEggMoves();
        InitializeItems();
        InitializeMegaEvolutions();
        InitializeTrainers();
        InitializeMaison();
        InitializeGameInfo();
    }

    public void InitializePersonal()
    {
        GARCPersonal = GetGARCData("personal");
        Personal = new PersonalTable(GARCPersonal.GetFile(GARCPersonal.FileCount - 1), Version);
    }

    public void InitializeLearnset()
    {
        GARCLearnsets = GetGARCData("levelup");
        switch (Generation)
        {
            case 6:
                Learnsets = GARCLearnsets.Files.Select(file => new Learnset6(file)).ToArray();
                break;
            case 7:
                Learnsets = GARCLearnsets.Files.Select(file => new Learnset6(file)).ToArray();
                break;
        }
    }

    public void InitializeGameText()
    {
        GARCGameText = GetGARCData("gametext");
        GameTextStrings = GARCGameText.Files.Select(file => new TextFile(this, file, RemapCharacters).Lines).ToArray();
    }

    public void InitializeItems()
    {
        Items = GetGARCData("item").Files.Select(file => new Item(file)).ToArray();
    }

    public void InitializeMegaEvolutions()
    {
        MegaEvolutions = GetGARCData("megaevo").Files.Select(file => new MegaEvolutions(file)).ToArray();
    }

    /// <summary>Loads the Gen 6 trainers. Entry 0 is a placeholder and stays null, as RSTE never opened it.</summary>
    public void InitializeTrainers()
    {
        if (Generation != 6)
            return;
        byte[][] d = GetGARCData("trdata").Files;
        byte[][] p = GetGARCData("trpoke").Files;
        Trainers = new TrainerData6[d.Length];
        for (int i = 1; i < d.Length; i++)
            Trainers[i] = new TrainerData6(d[i], p[i], ORAS);
    }

    /// <summary>Loads the Gen 6 Battle Maison trainers and Pokémon, normal and Super.</summary>
    public void InitializeMaison()
    {
        if (Generation != 6)
            return;
        MaisonTrainers = GetGARCData("maisontrN").Files.Select(z => new Maison6.Trainer(z)).ToArray();
        MaisonPokemon = GetGARCData("maisonpkN").Files.Select(z => new Maison6.Pokemon(z)).ToArray();
        SuperMaisonTrainers = GetGARCData("maisontrS").Files.Select(z => new Maison6.Trainer(z)).ToArray();
        SuperMaisonPokemon = GetGARCData("maisonpkS").Files.Select(z => new Maison6.Pokemon(z)).ToArray();
    }

    /// <summary>Loads the ORAS wild encounters, which <see cref="InitializeAll"/> does not (they are decompressed on load).</summary>
    public void InitializeWildEncounters()
    {
        if (ORAS)
            WildEncounters = new WildEncountersORAS(GetGARCData("encdata").Files);
    }

    /// <summary>Loads the story text, which <see cref="InitializeAll"/> does not (the upstream story text editor read it on open).</summary>
    public void InitializeStoryText()
    {
        GARCStoryText = GetGARCData("storytext");
        StoryTextStrings = GARCStoryText.Files.Select(file => new TextFile(this, file).Lines).ToArray();
    }

    public void InitializeMoves()
    {
        GARCMoves = GetGARCData("move");
        switch (Generation)
        {
            case 6:
                if (XY)
                    Moves = GARCMoves.Files.Select(file => new Move6(file)).ToArray();
                if (ORAS)
                    Moves = Mini.UnpackMini(GARCMoves.GetFile(0), "WD").Select(file => new Move6(file)).ToArray();
                break;
            case 7:
                Moves = Mini.UnpackMini(GARCMoves.GetFile(0), "WD").Select(file => new Move7(file)).ToArray();
                break;
        }
    }
    public void InitializeEvos()
    {
        var g = GetGARCData("evolution");
        byte[][] d = g.Files;
        switch (Generation)
        {
            case 6:
                Evolutions = d.Select(z => new EvolutionSet6(z)).ToArray();
                break;
            case 7:
                Evolutions = d.Select(z => new EvolutionSet7(z)).ToArray();
                break;
        }
    }

    public void InitializeEggMoves()
    {
        var g = GetGARCData("eggmove");
        switch (Generation)
        {
            case 6:
                EggMoves = EggMoves6.GetArray(g.Files);
                break;
            case 7:
                EggMoves = EggMoves7.GetArray(g.Files);
                break;
        }
    }

    // The Save methods write a table held above back to its GARC, as the pk3DS.WinForms editors did when closed.
    // Only entries whose contents changed are written; the rest keep their original bytes. Each returns how many it wrote.

    public int SavePersonal()
    {
        byte[][] d = GARCPersonal.Files;
        int changed = WriteChanged(d, Personal.Table, z => new PersonalTable(z, Version).Table[0], z => z.Write());
        if (changed == 0)
            return 0;
        // Set Master Table back
        for (int i = 0; i < d.Length - 1; i++)
            d[i].CopyTo(d[^1], i * d[i].Length);
        GARCPersonal.Files = d;
        GARCPersonal.Save();
        InitializePersonal();
        return changed;
    }

    public int SaveLearnsets()
    {
        byte[][] d = GARCLearnsets.Files;
        int changed = WriteChanged(d, Learnsets, z => new Learnset6(z), z => z.Write());
        if (changed == 0)
            return 0;
        GARCLearnsets.Files = d;
        GARCLearnsets.Save();
        InitializeLearnset();
        return changed;
    }

    public int SaveMoves()
    {
        bool isMini = ORAS || Generation == 7;
        byte[][] d = isMini ? Mini.UnpackMini(GARCMoves.GetFile(0), "WD") : GARCMoves.Files;
        Func<byte[], Move> read = Generation == 7 ? z => new Move7(z) : z => new Move6(z);
        int changed = WriteChanged(d, Moves, read, z => z.Write());
        if (changed == 0)
            return 0;
        GARCMoves.Files = isMini ? [Mini.PackMini(d, "WD")] : d;
        GARCMoves.Save();
        InitializeMoves();
        return changed;
    }

    public int SaveEvolutions()
    {
        var g = GetGARCData("evolution");
        byte[][] d = g.Files;
        Func<byte[], EvolutionSet> read = Generation == 7 ? z => new EvolutionSet7(z) : z => new EvolutionSet6(z);
        int changed = WriteChanged(d, Evolutions, read, z => z.Write());
        if (changed == 0)
            return 0;
        g.Files = d;
        g.Save();
        InitializeEvos();
        return changed;
    }

    public int SaveEggMoves()
    {
        var g = GetGARCData("eggmove");
        byte[][] d = g.Files;
        Func<byte[], EggMoves> read = Generation == 7 ? z => new EggMoves7(z) : z => new EggMoves6(z);
        int changed = WriteChanged(d, EggMoves, read, z => z.Write());
        if (changed == 0)
            return 0;
        g.Files = d;
        g.Save();
        InitializeEggMoves();
        return changed;
    }

    public int SaveItems()
    {
        var g = GetGARCData("item");
        byte[][] d = g.Files;
        int changed = WriteChanged(d, Items, z => new Item(z), z => z.Write());
        if (changed == 0)
            return 0;
        g.Files = d;
        g.Save();
        InitializeItems();
        return changed;
    }

    public int SaveMegaEvolutions()
    {
        var g = GetGARCData("megaevo");
        byte[][] d = g.Files;
        int changed = WriteChanged(d, MegaEvolutions, z => new MegaEvolutions(z), z => z.Write());
        if (changed == 0)
            return 0;
        g.Files = d;
        g.Save();
        InitializeMegaEvolutions();
        return changed;
    }

    public int SaveTrainers()
    {
        var gd = GetGARCData("trdata");
        var gp = GetGARCData("trpoke");
        byte[][] d = gd.Files;
        byte[][] p = gp.Files;
        int changed = 0;
        for (int i = 1; i < Trainers.Length; i++)
        {
            byte[] data = Trainers[i].Write();
            byte[] team = Trainers[i].WriteTeam();
            var original = new TrainerData6(d[i], p[i], ORAS);
            if (data.AsSpan().SequenceEqual(original.Write()) && team.AsSpan().SequenceEqual(original.WriteTeam()))
                continue;
            d[i] = data;
            p[i] = team;
            changed++;
        }
        if (changed == 0)
            return 0;
        gd.Files = d;
        gp.Files = p;
        gd.Save();
        gp.Save();
        InitializeTrainers();
        return changed;
    }

    public int SaveMaisonTrainers(bool super)
    {
        var g = GetGARCData(super ? "maisontrS" : "maisontrN");
        byte[][] d = g.Files;
        int changed = WriteChanged(d, super ? SuperMaisonTrainers : MaisonTrainers, z => new Maison6.Trainer(z), z => z.Write());
        if (changed == 0)
            return 0;
        g.Files = d;
        g.Save();
        InitializeMaison();
        return changed;
    }

    public int SaveMaisonPokemon(bool super)
    {
        var g = GetGARCData(super ? "maisonpkS" : "maisonpkN");
        byte[][] d = g.Files;
        int changed = WriteChanged(d, super ? SuperMaisonPokemon : MaisonPokemon, z => new Maison6.Pokemon(z), z => z.Write());
        if (changed == 0)
            return 0;
        g.Files = d;
        g.Save();
        InitializeMaison();
        return changed;
    }

    public int SaveWildEncounters()
    {
        var g = GetGARCData("encdata");
        var (files, changed) = WildEncounters.Write();
        if (changed == 0)
            return 0;
        g.Files = files;
        g.Save();
        InitializeWildEncounters();
        return changed;
    }

    public int SaveGameText() => SaveText(GARCGameText, GameTextStrings, RemapCharacters);

    public int SaveStoryText() => SaveText(GARCStoryText, StoryTextStrings, false);

    private int SaveText(GARCFile g, string[][] text, bool remapChars)
    {
        byte[][] d = g.Files;
        int changed = 0;
        for (int i = 0; i < text.Length; i++)
        {
            // Unchanged files keep their bytes: the text writer does not reproduce the original line flags (see docs/ui-deferred.md).
            if (text[i].SequenceEqual(new TextFile(this, d[i], remapChars).Lines))
                continue;
            try
            {
                d[i] = TextFile.GetBytes(this, text[i], remapChars);
            }
            catch (Exception e)
            {
                throw new InvalidDataException($"text file {i}: {e.Message}", e);
            }
            changed++;
        }
        if (changed == 0)
            return 0;
        g.Files = d;
        g.Save();
        return changed;
    }

    private static int WriteChanged<T>(byte[][] files, T[] entries, Func<byte[], T> read, Func<T, byte[]> write)
    {
        int changed = 0;
        for (int i = 0; i < entries.Length; i++)
        {
            byte[] data = write(entries[i]);
            // Compare with the original passed through the same writer: an entry the writer stores differently
            // than the game does (an empty Gen 6 egg move list) is then only written if its contents changed.
            if (data.AsSpan().SequenceEqual(write(read(files[i]))))
                continue;
            files[i] = data;
            changed++;
        }
        return changed;
    }

    private void InitializeGameInfo()
    {
        Info = new GameInfo(this);
    }

    public LazyGARCFile GetlzGARCData(string file)
    {
        var gr = GetGARCReference(file);
        gr = gr.LanguageVariant ? gr.GetRelativeGARC(Language, gr.Name) : gr;
        return new LazyGARCFile(GetlzGARC(file), gr, GetGARCPath(file));
    }

    public GARCFile GetGARCData(string file, bool skipRelative = false)
    {
        var gr = GetGARCReference(file);
        if (gr.LanguageVariant && !skipRelative)
            gr = gr.GetRelativeGARC(Language, gr.Name);
        return GetGARCByReference(gr);
    }

    public GARCFile GetGARCByReference(GARCReference gr)
    {
        try
        {
            return new(GetMemGARC(gr.Name), gr, GetGARCPath(gr.Name));
        }
        catch (FormatException f)
        {
            var message = $"{gr.Name} - ({gr.Reference}) is apparently corrupt. Please restore the backup for this file." + f.Message;
            throw new FormatException(message, f);
        }
    }

    private string GetGARCPath(string file)
    {
        var gr = GetGARCReference(file);
        gr = gr.LanguageVariant ? gr.GetRelativeGARC(Language, gr.Name) : gr;
        string subloc = gr.Reference;
        return Path.Combine(RomFS, subloc);
    }

    private GARC.MemGARC GetMemGARC(string file)
    {
        return new(File.ReadAllBytes(GetGARCPath(file)));
    }

    private GARC.LazyGARC GetlzGARC(string file)
    {
        return new(File.ReadAllBytes(GetGARCPath(file)));
    }

    public string RomFS, ExeFS;

    public GARCReference GetGARCReference(string name) { return Files?.FirstOrDefault(f => f.Name == name); }
    public TextVariableCode GetVariableCode(string name) { return Variables?.FirstOrDefault(v => v.Name == name); }
    public TextVariableCode GetVariableName(int value) { return Variables?.FirstOrDefault(v => v.Code == value); }

    private TextReference GetGameText(TextName name) { return GameText.FirstOrDefault(f => f.Name == name); }
    public TextData GetTextData(TextName file) => new(GetText(file));

    public string[] GetText(TextName file)
    {
        return (string[])GameTextStrings[GetGameText(file).Index].Clone();
    }

    public bool SetText(TextName file, string[] strings)
    {
        GameTextStrings[GetGameText(file).Index] = strings;
        return true;
    }

    public string GetGARCFileName(string requestedGARC)
    {
        var garc = GetGARCReference(requestedGARC);
        if (garc.LanguageVariant)
            garc = garc.GetRelativeGARC(Language);

        return garc.Reference;
    }

    public int Language { get; set; }

    public GARCFile GARCPersonal, GARCLearnsets, GARCMoves, GARCGameText, GARCStoryText;
    public PersonalTable Personal { get; private set; }
    public Learnset[] Learnsets { get; private set; }
    public string[][] GameTextStrings { get; private set; }
    public string[][] StoryTextStrings { get; private set; }
    public Move[] Moves { get; private set; }
    public EvolutionSet[] Evolutions { get; private set; }
    public EggMoves[] EggMoves { get; private set; }
    public Item[] Items { get; private set; }
    public MegaEvolutions[] MegaEvolutions { get; private set; }
    public TrainerData6[] Trainers { get; private set; }
    public Maison6.Trainer[] MaisonTrainers { get; private set; }
    public Maison6.Pokemon[] MaisonPokemon { get; private set; }
    public Maison6.Trainer[] SuperMaisonTrainers { get; private set; }
    public Maison6.Pokemon[] SuperMaisonPokemon { get; private set; }
    public WildEncountersORAS WildEncounters { get; private set; }

    public bool XY => Version == GameVersion.XY;
    public bool ORAS => Version is GameVersion.ORAS or GameVersion.ORASDEMO;
    public bool SM => Version is GameVersion.SM or GameVersion.SMDEMO;
    public bool USUM => Version == GameVersion.USUM;
    public int MaxSpeciesID => XY || ORAS ? Legal.MaxSpeciesID_6 : SM ? Legal.MaxSpeciesID_7_SM : Legal.MaxSpeciesID_7_USUM;
    public int GARCVersion => XY || ORAS ? GARC.VER_4 : GARC.VER_6;

    public int Generation
    {
        get
        {
            if (XY || ORAS)
                return 6;
            if (SM || USUM)
                return 7;
            return -1;
        }
    }

    public bool IsRebuildable(int fileCount)
    {
        return fileCount switch
        {
            FILECOUNT_XY => Version == GameVersion.XY,
            FILECOUNT_ORAS => Version == GameVersion.ORAS,
            FILECOUNT_ORASDEMO => Version == GameVersion.ORASDEMO,
            FILECOUNT_SMDEMO => Version == GameVersion.SMDEMO,
            FILECOUNT_SM => Version == GameVersion.SM,
            FILECOUNT_USUM => Version == GameVersion.USUM,
            _ => false,
        };
    }
}