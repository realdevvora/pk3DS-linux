using System;
using System.Collections.Generic;
using System.Linq;
using pk3DS.Core.Structures;
using pk3DS.Core.Structures.PersonalInfo;

namespace pk3DS.Core.Randomizers;

/// <summary>How the trainer randomizer sets moves (the "Moves" list of TrainerRand).</summary>
public enum TrainerMoves
{
    Keep,
    Random,
    LevelUp,
    Metronome,
}

/// <summary>Options of <see cref="TrainerRandomizer6"/>; the defaults are the TrainerRand form's starting values.</summary>
public sealed record TrainerRandomizerOptions
{
    public bool RandomPokemon { get; init; } = true;
    /// <summary>"Randomize by BST": replacements have a similar base stat total.</summary>
    public bool SimilarBST { get; init; }
    public bool Gen1 { get; init; } = true;
    public bool Gen2 { get; init; } = true;
    public bool Gen3 { get; init; } = true;
    public bool Gen4 { get; init; } = true;
    public bool Gen5 { get; init; } = true;
    public bool Gen6 { get; init; } = true;
    public bool GameLegends { get; init; } = true;
    public bool EventLegends { get; init; } = true;
    public bool RandomMegaForms { get; init; }
    public bool StoryMegaEvolutions { get; init; } = true;
    public bool ForceFullyEvolved { get; init; }
    public int ForceFullyEvolvedLevel { get; init; } = 50;
    public bool TypeTheme { get; init; }
    public bool ThemeGymTrainers { get; init; }
    public bool ThemeGymEliteFourOnly { get; init; }
    public int MinPokemon { get; init; } = 1;
    public int MaxPokemon { get; init; } = 6;
    public bool SixPokemonForImportant { get; init; }
    public TrainerMoves Moves { get; init; } = TrainerMoves.Random;
    public bool DamagingMoves { get; init; } = true;
    public int DamagingMoveCount { get; init; } = 2;
    public bool STABMoves { get; init; } = true;
    public int STABMoveCount { get; init; } = 2;
    public bool NoFixedDamage { get; init; } = true;
    public bool ForceHighPower { get; init; } = true;
    public int ForceHighPowerLevel { get; init; } = 50;
    public bool MultiplyLevels { get; init; }
    public decimal LevelMultiplier { get; init; } = 1;
    public bool RandomItems { get; init; } = true;
    public bool RandomAbilities { get; init; } = true;
    public bool MaxIvs { get; init; } = true;
    public bool RandomClasses { get; init; } = true;
    public bool IgnoreSpecialClasses { get; init; } = true;
    public bool OnlySingles { get; init; } = true;
    public bool RandomGifts { get; init; } = true;
    public decimal GiftPercent { get; init; } = 15;
    public bool MaxAI { get; init; } = true;
}

/// <summary>
/// The Gen 6 trainer randomizer (moved from the WinForms RSTE and its TrainerRand settings form). Works on <see cref="GameConfig.Trainers"/>.
/// </summary>
public sealed class TrainerRandomizer6 : IRandomizer
{
    private static readonly int[] MegaEvosORAS = [15, 18, 80, 208, 254, 260, 302, 319, 323, 334, 362, 373, 376, 380, 381, 428, 475, 531, 719, 3, 6, 9, 65, 94, 115, 127, 130, 142, 150, 181, 212, 214, 229, 248, 257, 282, 303, 306, 308, 310, 354, 359, 445, 448, 460];
    private static readonly int[] MegaEvosXY = [3, 6, 9, 65, 94, 115, 127, 130, 142, 150, 181, 212, 214, 229, 248, 257, 282, 303, 306, 308, 310, 354, 359, 445, 448, 460];
    private static readonly int[] StoryMegaEvosORAS = [178, 235, 557, 583, 687, 698, 699, 700, 701, 713, 906, 907, 908, 909, 910, 911, 912, 913, 942, 944, 946];
    private static readonly int[] StoryMegaEvosXY = [188, 263, 276, 277, 519, 520, 521, 526, 599, 600, 601];

    private readonly GameConfig config;
    private readonly TrainerData6[] trainers;
    private readonly PersonalInfo[] stats;
    private readonly string[] trClass;
    private readonly int typeCount;

    // Settings, as RSTE's static r* fields
    private readonly bool rPKM, rSmart, rLevel, rMove, rMetronome, rNoMove, rForceHighPower, rAbility, rDiffAI, rDiffIV, rClass, rGift, rItem,
        rRandomMegas, rGymE4Only, rTypeTheme, rTypeGymTrainers, rOnlySingles, rDMG, rSTAB, r6PKM, rForceFullyEvolved, rNoFixedDamage;
    private readonly int[] rIgnoreClass, rEnsureMEvo, rModelRestricted, rFinalEvo, megaEvos;
    private readonly int rDMGCount, rSTABCount;
    private readonly decimal rGiftPercent, rLevelMultiplier, rMinPKM, rMaxPKM, rForceFullyEvolvedLevel, rForceHighPowerLevel;
    private readonly SpeciesRandomizer rSpeciesRand;
    private readonly Dictionary<int, int[]> MegaDictionary;

    private string[] rTags;
    private string[] rImportant;
    private int[] mEvoTypes;
    private bool ImportantTrainers;
    private readonly List<string> Tags = [];
    private readonly Dictionary<string, int> TagTypes = [];

    public TrainerRandomizer6(GameConfig config, TrainerRandomizerOptions o)
    {
        this.config = config;
        trainers = config.Trainers;
        stats = config.Personal.Table;
        trClass = config.GetText(TextName.TrainerClasses);
        typeCount = config.GetText(TextName.Types).Length;
        MegaDictionary = Legal.GetMegaDictionary(config);
        rModelRestricted = config.ORAS ? Legal.Model_AO : Legal.Model_XY;
        megaEvos = config.ORAS ? MegaEvosORAS : MegaEvosXY;

        // As TrainerRand.B_Save_Click
        rPKM = o.RandomPokemon;
        rSmart = o.SimilarBST;
        rLevel = o.MultiplyLevels;
        rLevelMultiplier = o.LevelMultiplier;
        rNoFixedDamage = o.NoFixedDamage;
        rMove = o.Moves == TrainerMoves.Random;
        rNoMove = o.Moves == TrainerMoves.LevelUp;
        rMetronome = o.Moves == TrainerMoves.Metronome;
        if (rMove)
        {
            rDMG = o.DamagingMoves;
            rDMGCount = o.DamagingMoveCount;
            rSTAB = o.STABMoves;
            rSTABCount = o.STABMoveCount;
        }
        rItem = o.RandomItems;
        rAbility = o.RandomAbilities;
        rDiffIV = o.MaxIvs;
        rClass = o.RandomClasses;
        rIgnoreClass = rClass && o.IgnoreSpecialClasses ? config.ORAS ? Legal.SpecialClasses_ORAS : Legal.SpecialClasses_XY : [];
        rOnlySingles = rClass && o.OnlySingles;
        rGift = o.RandomGifts;
        rGiftPercent = o.GiftPercent;
        rDiffAI = o.MaxAI;
        rTypeTheme = o.TypeTheme;
        rTypeGymTrainers = o.ThemeGymTrainers;
        rGymE4Only = o.ThemeGymEliteFourOnly;
        rMinPKM = o.MinPokemon;
        rMaxPKM = o.MaxPokemon;
        r6PKM = o.SixPokemonForImportant;
        rRandomMegas = o.RandomMegaForms;
        rForceFullyEvolved = o.ForceFullyEvolved;
        rForceFullyEvolvedLevel = o.ForceFullyEvolvedLevel;
        rForceHighPower = o.ForceHighPower;
        rForceHighPowerLevel = o.ForceHighPowerLevel;
        rEnsureMEvo = o.StoryMegaEvolutions ? config.ORAS ? StoryMegaEvosORAS : StoryMegaEvosXY : [];
        rSpeciesRand = new SpeciesRandomizer(config)
        {
            G1 = o.Gen1,
            G2 = o.Gen2,
            G3 = o.Gen3,
            G4 = o.Gen4,
            G5 = o.Gen5,
            G6 = o.Gen6,
            L = o.GameLegends,
            E = o.EventLegends,
            Shedinja = true,
            rBST = o.SimilarBST,
            rEXP = false,
        };
        rSpeciesRand.Initialize();
        rFinalEvo = Legal.FinalEvolutions_6;
        // add Legendary/Mythical to final evolutions if checked
        if (o.GameLegends) rFinalEvo = [.. rFinalEvo, .. Legal.Legendary_6];
        if (o.EventLegends) rFinalEvo = [.. rFinalEvo, .. Legal.Mythical_6];
    }

    private static uint Rand() => Util.Random32();

    /// <summary>RSTE.Randomize</summary>
    public void Execute()
    {
        List<int> banned = [165, 621]; // Struggle, Hyperspace Fury
        if (rNoFixedDamage)
            banned.AddRange(MoveRandomizer.FixedDamageMoves);
        var move = new MoveRandomizer(config)
        {
            rDMG = rDMG,
            rSTAB = rSTAB,
            rSTABCount = rSTABCount,
            rDMGCount = rDMGCount,
            BannedMoves = banned,
        };
        var learn = new LearnsetRandomizer(config, config.Learnsets);

        rImportant = new string[trainers.Length];
        rTags = config.ORAS ? GetTagsORAS() : GetTagsXY();
        mEvoTypes = GetMegaEvolvableTypes();
        List<int> GymE4Types = [];

        if (rEnsureMEvo.Length > 0)
        {
            if (mEvoTypes.Length < 13 && rTypeTheme)
                throw new InvalidOperationException("There are insufficient Types with at least one mega evolution to Guarantee story Mega Evos while keeping Type theming. Re-Randomize Personal or don't choose both options.");
            GymE4Types.AddRange(mEvoTypes);
        }
        else
        {
            GymE4Types.AddRange(Enumerable.Range(0, typeCount));
        }

        foreach (int t1 in rEnsureMEvo.Where(t1 => rTags[t1].Length != 0 && !TagTypes.ContainsKey(rTags[t1])))
        {
            int t;
            if (rTags[t1].Contains("GYM") || rTags[t1].Contains("ELITE") || rTags[t1].Contains("CHAMPION"))
            {
                t = GymE4Types[(int)(Rand() % GymE4Types.Count)];
                GymE4Types.Remove(t);
            }
            else
            {
                t = mEvoTypes[Rand() % mEvoTypes.Length];
            }

            TagTypes[rTags[t1]] = t;
        }
        foreach (string t1 in Tags)
        {
            if (!TagTypes.ContainsKey(t1) && t1.Length != 0)
            {
                int t;
                if (t1.Contains("GYM") || t1.Contains("ELITE") || t1.Contains("CHAMPION"))
                {
                    t = GymE4Types[(int)(Rand() % GymE4Types.Count)];
                    GymE4Types.Remove(t);
                }
                else
                {
                    t = (int)(Rand() % typeCount);
                }

                TagTypes[t1] = t;
            }
        }

        ushort[] itemvals = config.ORAS ? Legal.Pouch_Items_AO : Legal.Pouch_Items_XY;
        itemvals = [.. itemvals, .. Legal.Pouch_Berry_XY];

        for (int i = 1; i < trainers.Length; i++)
        {
            // Trainer Type/Mega Evo
            int type = GetRandomType(i);
            bool mevo = rEnsureMEvo.Contains(i);
            bool isImportantClass = rImportant[i] != null && (rImportant[i].Contains("GYM") || rImportant[i].Contains("ELITE") || rImportant[i].Contains("CHAMPION"));
            bool typerand = (rTypeTheme && !rGymE4Only) || (rTypeTheme && isImportantClass);

            rSpeciesRand.rType = typerand;

            // RSTE read these two flags from the checkboxes of the trainer shown on screen; each trainer's own flags are used here.
            var t = trainers[i];
            t.Moves = rMove || (!rNoMove && t.Moves);
            t.Item = rItem || t.Item;

            SetMinMaxPKM(t);
            SetFullParties(t, rImportant[i] == null);
            RandomizeTrainerAIClass(t);
            RandomizeTrainerPrizeItem(t);
            RandomizeTeam(t, move, learn, itemvals, type, mevo, typerand);
        }
    }

    private void RandomizeTeam(TrainerData6 t, MoveRandomizer move, LearnsetRandomizer learn, ushort[] itemvals, int type, bool mevo, bool typerand)
    {
        int last = t.Team.Length - 1;
        for (int p = 0; p < t.Team.Length; p++)
        {
            var pk = t.Team[p];
            int[] stones = null;
            if (rPKM)
            {
                // randomize pokemon
                int species;
                if (typerand)
                {
                    species = rSpeciesRand.GetRandomSpeciesType(pk.Species, type);
                    if (p == last && mevo)
                    {
                        int tries = 0;
                        do { stones = GetRandomMega(out species); }
                        while (config.Personal[species].Types.All(z => z != type) && tries++ < 100);
                    }
                }
                else if (p == last && mevo)
                {
                    stones = GetRandomMega(out species);
                }
                else
                {
                    species = rSpeciesRand.GetRandomSpecies(pk.Species);
                }

                pk.Species = (ushort)species;
                pk.Gender = 0; // Set Gender to Random
                bool mega = rRandomMegas && !(mevo && p == last); // except if mega evolution is forced for the last slot
                pk.Form = (ushort)Randomizer.GetRandomForme(pk.Species, mega, true, stats);
            }
            if (rLevel)
                pk.Level = (ushort)Randomizer.GetModifiedLevel(pk.Level, rLevelMultiplier);
            if (rAbility)
                pk.Ability = (int)(1 + (Rand() % 3));
            if (rDiffIV)
                pk.IVs = 255;

            if (mevo && p == last && stones != null)
                pk.Item = (ushort)stones[Rand() % stones.Length];
            else if (rItem)
                pk.Item = itemvals[Rand() % itemvals.Length];

            if (rForceFullyEvolved && pk.Level >= rForceFullyEvolvedLevel && !rFinalEvo.Contains(pk.Species))
            {
                pk.Species = (ushort)rFinalEvo[(int)(Util.Random32() % rFinalEvo.Length)];
                pk.Form = (ushort)Randomizer.GetRandomForme(pk.Species, rRandomMegas, true, stats);
            }

            // random
            if (rMove)
            {
                var pkMoves = move.GetRandomMoveset(pk.Species, 4);
                for (int m = 0; m < 4; m++)
                    pk.Moves[m] = (ushort)pkMoves[m];
            }

            // levelup
            if (rNoMove)
            {
                t.Moves = true;
                var pkMoves = learn.GetCurrentMoves(pk.Species, pk.Form, pk.Level, 4);
                for (int m = 0; m < 4; m++)
                    pk.Moves[m] = (ushort)pkMoves[m];
            }

            if (rMetronome)
            {
                t.Moves = true;
                var pkMoves = new[] { 118, 0, 0, 0 };
                for (int m = 0; m < 4; m++)
                    pk.Moves[m] = (ushort)pkMoves[m];
            }

            // high-power attacks
            if (rForceHighPower && pk.Level >= rForceHighPowerLevel)
            {
                var pkMoves = learn.GetHighPoweredMoves(pk.Species, pk.Form, 4);
                for (int m = 0; m < 4; m++)
                    pk.Moves[m] = (ushort)pkMoves[m];
            }
        }
    }

    private void SetMinMaxPKM(TrainerData6 t)
    {
        int lastPKM = Math.Max(t.NumPokemon - 1, 0); // 0,1-6 => 0-5 (never is 0)
        var avgBST = (int)t.Team.Average(pk => stats[pk.Species].BST);
        int avgLevel = (int)t.Team.Average(pk => pk.Level);
        var pinfo = stats.OrderBy(pk => Math.Abs(avgBST - pk.BST)).First();
        int avgSpec = Array.IndexOf(stats, pinfo);

        // set minimum pkm, don't modify hordes
        if (t.NumPokemon < rMinPKM && t.BattleType != 4)
        {
            t.NumPokemon = (byte)rMinPKM;
            for (int f = lastPKM + 1; f < t.NumPokemon; f++)
            {
                Array.Resize(ref t.Team, (int)rMinPKM);
                t.Team[f] = // clone last pkm, keeping an average level for all new pkm
                    new TrainerData6.Pokemon(t.Team[lastPKM].Write(t.Item, t.Moves), t.Item, t.Moves)
                    {
                        Species = (ushort)rSpeciesRand.GetRandomSpecies(avgSpec),
                        Level = (ushort)avgLevel,
                    };
            }
        }

        // set maximum pkm, don't modify hordes
        if (t.NumPokemon > rMaxPKM && t.BattleType != 4)
        {
            Array.Resize(ref t.Team, (int)rMaxPKM);
            t.NumPokemon = (byte)rMaxPKM;
        }
    }

    private void SetFullParties(TrainerData6 t, bool important)
    {
        int lastPKM = Math.Max(t.NumPokemon - 1, 0); // 0,1-6 => 0-5 (never is 0)
        var avgBST = (int)t.Team.Average(pk => stats[pk.Species].BST);
        int avgLevel = (int)t.Team.Average(pk => pk.Level);
        var pinfo = stats.OrderBy(pk => Math.Abs(avgBST - pk.BST)).First();
        int avgSpec = Array.IndexOf(stats, pinfo);

        // 6 pkm for important trainers, skip the first rival battles
        if (!r6PKM || important)
            return;

        t.NumPokemon = 6;
        for (int f = lastPKM + 1; f < t.NumPokemon; f++)
        {
            Array.Resize(ref t.Team, t.NumPokemon);
            t.Team[f] = // clone last pkm, keeping an average level for all new pkm
                new TrainerData6.Pokemon(t.Team[lastPKM].Write(t.Item, t.Moves), t.Item, t.Moves)
                {
                    Species = (ushort)rSpeciesRand.GetRandomSpecies(avgSpec),
                    Level = (ushort)avgLevel,
                };
        }
    }

    private void RandomizeTrainerAIClass(TrainerData6 t)
    {
        if (rDiffAI)
            t.AI |= 7; // Set first 3 bits, keep any other flag if present

        if (rClass && rModelRestricted.Contains(t.Class) && !rIgnoreClass.Contains(t.Class)) // shuffle classes with 3D models
        {
            int randClass() => (int)(Rand() % rModelRestricted.Length);
            t.Class = rModelRestricted[randClass()];
        }
        else
        if (
            rClass // Classes selected to be randomized
            && (!rOnlySingles || t.BattleType == 0) //  Nonsingles only get changed if rOnlySingles
            && !rIgnoreClass.Contains(t.Class) // Current class isn't a special class
        )
        {
            int randClass() => (int)(Rand() % trClass.Length);
            int rv; do { rv = randClass(); }
            while (rIgnoreClass.Contains(rv) || trClass[rv].StartsWith("[~") || (config.ORAS && rv is >= 0 and <= 63) || rv is >= 68 and <= 126); // don't allow disallowed classes
            t.Class = rv;
        }
    }

    private void RandomizeTrainerPrizeItem(TrainerData6 t)
    {
        if (rGift && Rand() % 100 < rGiftPercent)
        {
            ushort[] items;
            uint rnd = Rand() % 10;
            if (rnd < 2) // held item
                items = config.ORAS ? Legal.Pouch_Items_AO : Legal.Pouch_Items_XY;
            else if (rnd < 5) // medicine
                items = config.ORAS ? Legal.Pouch_Medicine_AO : Legal.Pouch_Medicine_XY;
            else // berry
                items = Legal.Pouch_Berry_XY;
            t.Prize = items[Rand() % items.Length];
        }
        else if (rGift)
        {
            t.Prize = 0;
        }
    }

    private string[] GetTagsORAS()
    {
        string[] tags = Enumerable.Repeat("", trainers.Length).ToArray();
        ImportantTrainers = true;

        // Rival Battles
        TagTrainer(tags, "RIVAL1", 289, 292, 295, 298, 527, 530, 674, 677, 699, 906); // Rival w/ Grass Starter
        TagTrainer(tags, "RIVAL2", 290, 293, 296, 299, 528, 531, 675, 678, 700, 907); // Rival w/ Fire Starter
        TagTrainer(tags, "RIVAL3", 291, 294, 297, 300, 529, 532, 676, 679, 701, 908); // Rival w/ Water Starter

        // Aqua Admins
        TagTrainer(tags, "AQUA1", 178, 231, 266);           // Archie
        TagTrainer(tags, "AQUA2", 683, 684, 685, 686, 687); // Matt
        TagTrainer(tags, "AQUA3", 688, 689, 690);           // Shelly

        // Magma Admins
        TagTrainer(tags, "MAGMA1", 235, 236, 271);           // Maxie
        TagTrainer(tags, "MAGMA2", 694, 695, 696, 697, 698); // Courtney
        TagTrainer(tags, "MAGMA3", 691, 692, 693);           // Tabitha

        // Gym Leaders
        TagTrainer(tags, "GYM1", 561);      // Roxanne
        TagTrainer(tags, "GYM2", 563);      // Brawly
        TagTrainer(tags, "GYM3", 567);      // Wattson
        TagTrainer(tags, "GYM4", 569);      // Flannery
        TagTrainer(tags, "GYM5", 570);      // Norman
        TagTrainer(tags, "GYM6", 571);      // Winona
        TagTrainer(tags, "GYM7", 552);      // Liza & Tate
        TagTrainer(tags, "GYM8", 572, 943); // Wallace

        // Elite Four
        TagTrainer(tags, "ELITE1", 553, 909);             // Sidney
        TagTrainer(tags, "ELITE2", 554, 910);             // Phoebe
        TagTrainer(tags, "ELITE3", 555, 911);             // Glacia
        TagTrainer(tags, "ELITE4", 556, 912);             // Drake
        TagTrainer(tags, "CHAMPION", 557, 680, 913, 942); // Champion Steven

        // Wally
        TagTrainer(tags, "WALLY", 518, 583, 944, 945, 946, 947);

        // Zinnia
        TagTrainer(tags, "LOREKEEPER", 713, 898);

        // Aarune
        TagTrainer(tags, "EXPERT", 856, 857);

        ImportantTrainers = false;
        TagTrainer(tags, "RIVAL1", 1, 4); // Rival w/ Grass Starter
        TagTrainer(tags, "RIVAL2", 2, 5); // Rival w/ Fire Starter
        TagTrainer(tags, "RIVAL3", 3, 6); // Rival w/ Water Starter

        // Gym Trainers (Tagged in order of appearance on Bulbapedia's lists)
        if (rTypeGymTrainers)
        {
            TagTrainer(tags, "GYM1", 562, 22, 667);
            TagTrainer(tags, "GYM2", 60, 56, 59);
            TagTrainer(tags, "GYM3", 34, 568, 614, 35);
            TagTrainer(tags, "GYM4", 81, 824, 83, 615, 823, 613, 85);
            TagTrainer(tags, "GYM5", 63, 67, 64, 68, 65, 69, 66);
            TagTrainer(tags, "GYM6", 115, 517, 516, 118, 730);
            TagTrainer(tags, "GYM7", 157, 226, 320, 159, 225, 158);
            TagTrainer(tags, "GYM8", 647, 342, 594, 646, 338, 339, 340, 341);
        }
        return tags;
    }

    private string[] GetTagsXY()
    {
        string[] tags = Enumerable.Repeat("", trainers.Length).ToArray();
        ImportantTrainers = true;

        // Rival Battles
        TagTrainer(tags, "RIVAL1", 130, 184, 329, 332, 335, 338, 341, 435, 519, 604, 575, 578, 581, 584, 587, 590, 593, 596, 599, 607); // Rival w/ Fire Starter
        TagTrainer(tags, "RIVAL2", 131, 185, 330, 333, 336, 339, 342, 436, 520, 605, 576, 579, 582, 585, 588, 591, 594, 597, 600, 608); // Rival w/ Water Starter
        TagTrainer(tags, "RIVAL3", 132, 186, 331, 334, 337, 340, 343, 437, 521, 606, 577, 580, 583, 586, 589, 592, 595, 598, 601, 609); // Rival w/ Grass Starter

        // Team Flare Admins
        TagTrainer(tags, "FLAREBOSS", 303, 525, 526); // Lysandre
        TagTrainer(tags, "FLARE1", 175, 344);         // Aliana
        TagTrainer(tags, "FLARE2", 350, 351);         // Bryony
        TagTrainer(tags, "FLARE3", 348, 349);         // Celosia
        TagTrainer(tags, "FLARE4", 346, 347);         // Mable
        TagTrainer(tags, "FLARE5", 345);              // Xerosic

        // Gym Leaders
        TagTrainer(tags, "GYM1", 6, 254, 262);       // Viola
        TagTrainer(tags, "GYM2", 76, 261, 279);      // Grant
        TagTrainer(tags, "GYM3", 21, 255, 263, 613); // Korrina
        TagTrainer(tags, "GYM4", 22, 256, 264);      // Ramos
        TagTrainer(tags, "GYM5", 23, 257, 265);      // Clemont
        TagTrainer(tags, "GYM6", 24, 258, 266);      // Valerie
        TagTrainer(tags, "GYM7", 25, 259, 267);      // Olympia
        TagTrainer(tags, "GYM8", 26, 260, 268);      // Wulfric

        // Elite Four
        TagTrainer(tags, "ELITE1", 269, 273, 507); // Malva
        TagTrainer(tags, "ELITE2", 271, 275);      // Siebold
        TagTrainer(tags, "ELITE3", 187, 272);      // Wikstrom
        TagTrainer(tags, "ELITE4", 270, 274);      // Drasna
        TagTrainer(tags, "CHAMPION", 276, 277);    // Champion Diantha

        // Friends
        TagTrainer(tags, "SHAUNA", 321, 322, 323);
        TagTrainer(tags, "TREVOR", 325, 439);
        TagTrainer(tags, "TIERNO", 324, 438, 573);

        // Professor
        TagTrainer(tags, "PROFESSOR", 327, 328);

        // Suspicious Trainer ???
        TagTrainer(tags, "ESSENTIA", 503, 504, 505, 511, 512, 513, 514, 515); // Emma

        // AZ
        TagTrainer(tags, "AZ", 602);

        // Battle Chatelaines
        TagTrainer(tags, "CHATELAINE1", 559); // Nita
        TagTrainer(tags, "CHATELAINE2", 560); // Evelyn
        TagTrainer(tags, "CHATELAINE3", 561); // Dana
        TagTrainer(tags, "CHATELAINE4", 562); // Morgan

        ImportantTrainers = false;
        TagTrainer(tags, "SHAUNA", 137, 138, 139);
        TagTrainer(tags, "GYM3", 188);

        // Gym Trainers (Tagged in order of appearance on Bulbapedia's lists)
        if (rTypeGymTrainers)
        {
            TagTrainer(tags, "GYM1", 39, 40, 48);
            TagTrainer(tags, "GYM2", 64, 63, 106, 105);
            TagTrainer(tags, "GYM3", 83, 147, 84, 146);
            TagTrainer(tags, "GYM4", 123, 121, 124, 122);
            TagTrainer(tags, "GYM5", 461, 462, 463, 464, 465, 466, 28, 29, 30, 467, 468, 469);
            TagTrainer(tags, "GYM6", 245, 250, 248, 243);
            TagTrainer(tags, "GYM7", 170, 171, 172, 365, 366);
            TagTrainer(tags, "GYM8", 169, 32, 168, 31);
        }
        return tags;
    }

    // Theme Methods
    private void TagTrainer(string[] trTags, string tag, params int[] ids)
    {
        foreach (int id in ids.Where(id => id < trTags.Length))
            trTags[id] = tag;

        if (!Tags.Contains(tag))
            Tags.Add(tag);

        if (!ImportantTrainers) return;
        foreach (int id in ids)
            rImportant[id] = tag;
    }

    private int[] GetRandomMega(out int species)
    {
        int rnd = Util.Rand.Next(0, MegaDictionary.Count - 1);
        species = MegaDictionary.Keys.ElementAt(rnd);
        return MegaDictionary.Values.ElementAt(rnd);
    }

    private int GetRandomType(int trainer)
    {
        if (rTags[trainer].Length != 0)
            return TagTypes[rTags[trainer]];
        if (!rEnsureMEvo.Contains(trainer))
            return (int)(Rand() % typeCount);
        return mEvoTypes[Rand() % mEvoTypes.Length];
    }

    private int[] GetMegaEvolvableTypes()
    {
        List<int> MEvoTypes = [];
        foreach (int spec in megaEvos)
        {
            if (!MEvoTypes.Contains(stats[spec].Types[0]))
                MEvoTypes.Add(stats[spec].Types[0]);
            if (!MEvoTypes.Contains(stats[spec].Types[1]))
                MEvoTypes.Add(stats[spec].Types[1]);
        }
        MEvoTypes.Sort();
        return [.. MEvoTypes];
    }
}
