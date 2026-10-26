using System;
using pk3DS.Core;
using pk3DS.Core.Randomizers;

namespace pk3DS.Mac.ViewModels;

public enum EditorTab { RomFS, ExeFS, CRO }

/// <summary>An editor button of the main window: its tab and name as in pk3DS, and what it opens.</summary>
public sealed record Editor(EditorTab Tab, string Name, Action<MainViewModel> Open);

public static class Editors
{
    /// <summary>The editors, in the order of the pk3DS main window. Editors not in Core (title screen, move tutors, Gen 7) are left out.</summary>
    public static readonly Editor[] All =
    [
        new(EditorTab.RomFS, "Game Text", main => main.Dialogs.Show(new TextEditorViewModel(main, story: false))),
        new(EditorTab.RomFS, "Story Text", main => main.Dialogs.Show(new TextEditorViewModel(main, story: true))),
        new(EditorTab.RomFS, "Personal Stats", main => main.Dialogs.Show(new SpeciesPageViewModel(main))),
        Table(EditorTab.RomFS, "Evolutions", [("Evolutions", DataTable.Evolution)],
            Randomizer<EvolutionRandomizerOptions>("Randomize", "Randomize evolutions", TableRandomizers.Evolutions),
            Randomizer<EvolutionRandomizerOptions>("Evolve every level", "Evolve every level", TableRandomizers.EvolveEveryLevel),
            Randomizer<NoOptions>("Remove trade evolutions", "Remove trade evolutions", (c, _) => TableRandomizers.RemoveTradeEvolutions(c))),
        Table(EditorTab.RomFS, "Level Up Moves", [("Level up moves", DataTable.Learnset)],
            Randomizer<LearnsetRandomizerOptions>("Randomize", "Randomize level up moves", TableRandomizers.Learnset)),
        Table(EditorTab.RomFS, "Wild Encounters", [("Wild encounters", DataTable.Wild)],
            Randomizer<WildRandomizerOptions>("Randomize", "Randomize wild encounters", TableRandomizers.Wild)),
        Table(EditorTab.RomFS, "Mega Evolutions", [("Mega Evolutions", DataTable.MegaEvo)]),
        Table(EditorTab.RomFS, "Egg Moves", [("Egg moves", DataTable.EggMove)],
            Randomizer<EggMoveRandomizerOptions>("Randomize", "Randomize egg moves", TableRandomizers.EggMoves)),
        new(EditorTab.RomFS, "Trainers", main => main.Dialogs.Show(new TrainerPageViewModel(main))),
        Table(EditorTab.RomFS, "Battle Maison", [("Trainers", DataTable.MaisonTrainer), ("Pokémon", DataTable.MaisonPokemon),
            ("Super trainers", DataTable.SuperMaisonTrainer), ("Super Pokémon", DataTable.SuperMaisonPokemon)]),
        Table(EditorTab.RomFS, "Item Stats", [("Items", DataTable.Item)]),
        Table(EditorTab.RomFS, "Move Stats", [("Moves", DataTable.Move)],
            Randomizer<MoveRandomizerOptions>("Randomize", "Randomize moves", TableRandomizers.Moves)),

        Table(EditorTab.ExeFS, "TMs/HMs", [("TMs and HMs", DataTable.TMHM)]),
        Table(EditorTab.ExeFS, "Poké Mart", [("Marts", DataTable.Mart)]),
        Table(EditorTab.ExeFS, "Pickup Items", [("Pickup", DataTable.Pickup)]),
        Table(EditorTab.ExeFS, "O-Powers", [("O-Powers", DataTable.OPower)]),
        new(EditorTab.ExeFS, "Shiny Rate", main => main.Dialogs.Show(new ShinyViewModel(main))),

        Table(EditorTab.CRO, "Type Chart", [("Type chart", DataTable.TypeChart)]),
        Table(EditorTab.CRO, "Starter Pokémon", [("Starters", DataTable.Starter)]),
        Table(EditorTab.CRO, "Gift Pokémon", [("Gifts", DataTable.Gift)]),
        Table(EditorTab.CRO, "Static Encounters", [("Static encounters", DataTable.StaticEncounter)]),
    ];

    /// <summary>The randomize buttons of the species and trainer pages' table views.</summary>
    public static readonly RandomizerButton[] PersonalRandomizers =
        [Randomizer<PersonalRandomizerOptions>("Randomize", "Randomize personal data", TableRandomizers.Personal)];

    public static readonly RandomizerButton[] TrainerRandomizers =
        [Randomizer<TrainerRandomizerOptions>("Randomize", "Randomize trainers", TableRandomizers.Trainers)];

    private static Editor Table(EditorTab tab, string name, (string, DataTable)[] tables, params RandomizerButton[] randomizers) =>
        new(tab, name, main => main.Dialogs.Show(new TableEditorViewModel(main, name, tables, randomizers)));

    private static RandomizerButton Randomizer<T>(string button, string title, Func<GameConfig, T, int> run) where T : new() =>
        new(button, main => RandomizerViewModel.Create(main, title, run));

    /// <summary>A randomizer without options.</summary>
    private sealed record NoOptions;
}
