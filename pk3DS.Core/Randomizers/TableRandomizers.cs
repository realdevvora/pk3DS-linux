using System;
using System.Collections.Generic;
using System.Linq;

namespace pk3DS.Core.Randomizers;

/// <summary>Options of <see cref="TableRandomizers.Personal"/>; the defaults are PersonalEditor6's.</summary>
public sealed record PersonalRandomizerOptions
{
    public bool CatchRate { get; init; } = true;
    public bool EggGroups { get; init; } = true;
    public decimal SameEggGroupChance { get; init; } = 50;
    public bool Stats { get; init; } = true;
    public bool ShuffleStats { get; init; } = true;
    public decimal StatDeviation { get; init; } = 25;
    // Which stats to randomize, in PersonalEditor6's order (HP, Atk, Def, SpA, SpD, Spe).
    public bool StatHp { get; init; } = true;
    public bool StatAtk { get; init; } = true;
    public bool StatDef { get; init; } = true;
    public bool StatSpa { get; init; } = true;
    public bool StatSpd { get; init; } = true;
    public bool StatSpe { get; init; } = true;
    public bool Abilities { get; init; } = true;
    public bool WonderGuard { get; init; }
    public bool Types { get; init; } = true;
    public decimal SameTypeChance { get; init; } = 50;
    public bool HeldItems { get; init; } = true;
    public bool TM { get; init; } = true;
    public bool HM { get; init; } = true;
    /// <summary>The "Special Tutors" checkbox.</summary>
    public bool TypeTutors { get; init; } = true;
    /// <summary>The "Battle Resort Tutors" checkbox (ORAS only).</summary>
    public bool MoveTutors { get; init; } = true;
}

/// <summary>Options of <see cref="TableRandomizers.Learnset"/>; the defaults are LevelUpEditor6's.</summary>
public sealed record LearnsetRandomizerOptions
{
    public bool Expand { get; init; }
    public int ExpandTo { get; init; } = 25;
    public bool Spread { get; init; }
    public int SpreadTo { get; init; } = 75;
    /// <summary>Bias by type: prefer moves of the Pokémon's types, and start with one.</summary>
    public bool STAB { get; init; }
    public decimal STABPercent { get; init; } = 52;
    public bool AllowHms { get; init; }
    /// <summary>Ban Dragon Rage and Sonic Boom.</summary>
    public bool NoFixedDamage { get; init; } = true;
    public bool FourMovesAtLevel1 { get; init; }
}

/// <summary>Options of <see cref="TableRandomizers.EggMoves"/>; the defaults are EggMoveEditor6's.</summary>
public sealed record EggMoveRandomizerOptions
{
    public bool Expand { get; init; }
    public int ExpandTo { get; init; } = 18;
    public bool STAB { get; init; }
    public decimal STABPercent { get; init; } = 32;
    public bool AllowHms { get; init; }
}

/// <summary>Options of the evolution randomizers; the defaults are EvolutionEditor6's.</summary>
public sealed record EvolutionRandomizerOptions
{
    public bool SimilarBST { get; init; } = true;
    public bool SameEXPGrowth { get; init; } = true;
    public bool SharedType { get; init; } = true;
    public bool GameLegends { get; init; }
    public bool EventLegends { get; init; }
}

/// <summary>Options of <see cref="TableRandomizers.Moves"/>; MoveEditor6 starts with both unchecked and needs at least one.</summary>
public sealed record MoveRandomizerOptions
{
    public bool Category { get; init; }
    public bool Type { get; init; }
}

/// <summary>
/// The randomize buttons of the WinForms editors: each runs the editor's randomizer with the same settings on the loaded table and saves it.
/// Seed <see cref="Util.Rand"/> with <see cref="Util.ReseedRand"/> first to make a run repeatable.
/// </summary>
/// <returns>Each method returns the number of entries that changed.</returns>
public static class TableRandomizers
{
    private static readonly int[] AlwaysBanned = [165, 621]; // Struggle, Hyperspace Fury
    // The editors ban these default HM moves unless HMs are allowed.
    private static readonly int[] DefaultHMs = [15, 19, 57, 70, 127, 249, 291];

    public static int Personal(GameConfig config, PersonalRandomizerOptions o)
    {
        RequireORAS(config);
        new PersonalRandomizer(config.Personal.Table, config)
        {
            TypeCount = config.GetText(TextName.Types).Length,
            ModifyCatchRate = o.CatchRate,
            ModifyEggGroup = o.EggGroups,
            ModifyStats = o.Stats,
            ShuffleStats = o.ShuffleStats,
            StatsToRandomize = [o.StatHp, o.StatAtk, o.StatDef, o.StatSpa, o.StatSpd, o.StatSpe],
            ModifyAbilities = o.Abilities,
            ModifyLearnsetTM = o.TM,
            ModifyLearnsetHM = o.HM,
            ModifyLearnsetTypeTutors = o.TypeTutors,
            ModifyLearnsetMoveTutors = config.ORAS && o.MoveTutors,
            ModifyTypes = o.Types,
            ModifyHeldItems = o.HeldItems,
            SameTypeChance = o.SameTypeChance,
            SameEggGroupChance = o.SameEggGroupChance,
            StatDeviation = o.StatDeviation,
            AllowWonderGuard = o.WonderGuard,
        }.Execute();
        return config.SavePersonal();
    }

    public static int Learnset(GameConfig config, LearnsetRandomizerOptions o)
    {
        RequireORAS(config);
        var banned = new List<int>(AlwaysBanned);
        if (!o.AllowHms)
            banned.AddRange(DefaultHMs);
        if (o.NoFixedDamage)
            banned.AddRange(MoveRandomizer.FixedDamageMoves);
        new LearnsetRandomizer(config, config.Learnsets)
        {
            Expand = o.Expand,
            ExpandTo = o.ExpandTo,
            Spread = o.Spread,
            SpreadTo = o.SpreadTo,
            STAB = o.STAB,
            STABPercent = o.STABPercent,
            STABFirst = o.STAB,
            BannedMoves = [.. banned],
            Learn4Level1 = o.FourMovesAtLevel1,
        }.Execute();
        return config.SaveLearnsets();
    }

    public static int EggMoves(GameConfig config, EggMoveRandomizerOptions o)
    {
        RequireORAS(config);
        new EggMoveRandomizer(config, config.EggMoves)
        {
            Expand = o.Expand,
            ExpandTo = o.ExpandTo,
            STAB = o.STAB,
            STABPercent = o.STABPercent,
            BannedMoves = [.. AlwaysBanned.Concat(o.AllowHms ? [] : DefaultHMs)],
        }.Execute();
        return config.SaveEggMoves();
    }

    /// <summary>Randomizes the species each Pokémon evolves into; methods and arguments stay the same.</summary>
    public static int Evolutions(GameConfig config, EvolutionRandomizerOptions o)
    {
        RequireORAS(config);
        var rand = NewEvolutionRandomizer(config, o);
        rand.Execute();
        return config.SaveEvolutions();
    }

    /// <summary>Replaces trade evolutions with level-up ones, so every evolution works in one game (the editor's "remove trade evolutions").</summary>
    public static int RemoveTradeEvolutions(GameConfig config)
    {
        RequireORAS(config);
        var rand = new EvolutionRandomizer(config, config.Evolutions);
        rand.Randomizer.Initialize();
        rand.ExecuteTrade();
        return config.SaveEvolutions();
    }

    /// <summary>Makes every Pokémon evolve into a random species at every level up (the editor's "evolve every level").</summary>
    public static int EvolveEveryLevel(GameConfig config, EvolutionRandomizerOptions o)
    {
        RequireORAS(config);
        var rand = NewEvolutionRandomizer(config, o);
        rand.ExecuteEvolveEveryLevel();
        rand.Execute(); // randomize right after, as the editor does
        return config.SaveEvolutions();
    }

    public static int Moves(GameConfig config, MoveRandomizerOptions o)
    {
        RequireORAS(config);
        new MoveInfoRandomizer(config.Moves) { Category = o.Category, Type = o.Type }.Execute();
        return config.SaveMoves();
    }

    private static void RequireORAS(GameConfig config)
    {
        if (!config.ORAS)
            throw new NotSupportedException("The randomizers are only supported for Omega Ruby / Alpha Sapphire so far.");
    }

    private static EvolutionRandomizer NewEvolutionRandomizer(GameConfig config, EvolutionRandomizerOptions o)
    {
        var rand = new EvolutionRandomizer(config, config.Evolutions)
        {
            Randomizer =
            {
                rBST = o.SimilarBST,
                rEXP = o.SameEXPGrowth,
                rType = o.SharedType,
                L = o.GameLegends,
                E = o.EventLegends,
            },
        };
        rand.Randomizer.Initialize();
        return rand;
    }
}
