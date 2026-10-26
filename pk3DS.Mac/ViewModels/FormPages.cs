using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using pk3DS.Core;
using pk3DS.Mac.Services;

namespace pk3DS.Mac.ViewModels;

/// <summary>
/// One field of a form page: a cell of the selected row, shown as a list of names, a checkbox or a text box. Values go through
/// the table's checks; a refused value leaves the field as it was and the page shows why.
/// </summary>
public sealed partial class FormField : ObservableObject
{
    private readonly TableRow row;
    private readonly int column;
    private readonly Action changed;

    public string Label { get; }
    public bool IsReadOnly { get; }
    public IReadOnlyList<string> Choices { get; }
    public bool IsChoice => Choices != null;
    public bool IsFlag { get; }
    public bool IsText => !IsChoice && !IsFlag;
    public bool IsWide { get; }

    public FormField(TableRow row, int column, string label, bool readOnly, IReadOnlyList<string> choices = null, bool flag = false, bool wide = false, Action changed = null)
    {
        this.row = row;
        this.column = column;
        this.changed = changed;
        Label = label;
        IsReadOnly = readOnly;
        Choices = choices;
        IsFlag = flag;
        IsWide = wide;
    }

    public string Value
    {
        get => row[column];
        set
        {
            row[column] = value;
            OnPropertyChanged(nameof(Value));
            OnPropertyChanged(nameof(Index));
            OnPropertyChanged(nameof(Flag));
            changed?.Invoke();
        }
    }

    public int Index
    {
        get => int.TryParse(Value, out int i) && Choices != null && i < Choices.Count ? i : -1;
        set
        {
            if (value >= 0 && value.ToString() != Value)
                Value = value.ToString();
        }
    }

    public bool Flag
    {
        get => Value == "1";
        set => Value = value ? "1" : "0";
    }
}

/// <summary>A titled group of fields on a form page.</summary>
public sealed record FormGroup(string Title, IReadOnlyList<FormField> Fields);

/// <summary>Name lists for the fields that hold an ID, as "123 Name".</summary>
internal static class Names
{
    public static string[] Numbered(string[] names) => [.. names.Select((n, i) => $"{i} {n}")];
}

/// <summary>
/// The species page (pk3DS's Personal editor): a list of species and forms, and the selected entry's data in groups, with its sprite.
/// </summary>
public sealed partial class SpeciesPageViewModel : ObservableObject
{
    private readonly MainViewModel main;
    private readonly string[] types, abilities, items;

    public string Title => "Personal Stats";
    public TablePageViewModel Table { get; }
    public ObservableCollection<TableRow> Entries { get; } = [];
    public ObservableCollection<FormGroup> Groups { get; } = [];

    [ObservableProperty]
    public partial TableRow Selected { get; set; }

    [ObservableProperty]
    public partial string Search { get; set; } = "";

    [ObservableProperty]
    public partial Bitmap Sprite { get; private set; }

    [ObservableProperty]
    public partial string Heading { get; private set; } = "";

    public SpeciesPageViewModel(MainViewModel main)
    {
        this.main = main;
        Table = new TablePageViewModel(main, "Personal", DataTable.Personal);
        types = Names.Numbered(main.Config.GetText(TextName.Types));
        abilities = Names.Numbered(main.Config.GetText(TextName.AbilityNames));
        items = Names.Numbered(main.Config.GetText(TextName.ItemNames));
        RefreshEntries();
    }

    private int Col(string name) => Table.Columns.Select((c, i) => (c, i)).First(z => z.c.Name == name).i;

    partial void OnSearchChanged(string value) => RefreshEntries();

    private void RefreshEntries()
    {
        string index = Selected?[0];
        Entries.Clear();
        string s = Search.Trim();
        foreach (var row in Table.Rows.Where(r => s.Length == 0 || r[1].Contains(s, StringComparison.OrdinalIgnoreCase) || r[0] == s))
            Entries.Add(row);
        Selected = Entries.FirstOrDefault(r => r[0] == index) ?? Entries.FirstOrDefault(r => r[0] == "1") ?? Entries.FirstOrDefault();
    }

    partial void OnSelectedChanged(TableRow value)
    {
        Groups.Clear();
        if (value == null)
            return;
        FormField F(string column, string label, IReadOnlyList<string> choices = null, bool wide = false) =>
            new(value, Col(column), label, Table.Columns[Col(column)].ReadOnly, choices, wide: wide, changed: UpdateHeading);

        Groups.Add(new("Base stats", [F("hp", "HP"), F("atk", "Attack"), F("def", "Defense"), F("spa", "Sp. Atk"), F("spd", "Sp. Def"), F("spe", "Speed")]));
        Groups.Add(new("EV yield", [F("ev_hp", "HP"), F("ev_atk", "Attack"), F("ev_def", "Defense"), F("ev_spa", "Sp. Atk"), F("ev_spd", "Sp. Def"), F("ev_spe", "Speed")]));
        Groups.Add(new("Types and abilities", [F("type1", "Type 1", types), F("type2", "Type 2", types),
            F("ability1", "Ability 1", abilities), F("ability2", "Ability 2", abilities), F("ability3", "Hidden ability", abilities)]));
        Groups.Add(new("Wild held items", [F("item1", "Common", items), F("item2", "Rare", items), F("item3", "Very rare", items)]));
        Groups.Add(new("Breeding and growth", [F("gender", "Gender ratio"), F("hatch_cycles", "Hatch cycles"), F("egg_group1", "Egg group 1"),
            F("egg_group2", "Egg group 2"), F("base_friendship", "Base friendship"), F("exp_growth", "EXP growth")]));
        Groups.Add(new("Other", [F("catch_rate", "Catch rate"), F("evo_stage", "Evolution stage"), F("escape_rate", "Escape rate"), F("base_exp", "Base EXP"),
            F("color", "Color"), F("height", "Height (m)"), F("weight", "Weight (kg)"), F("forme_count", "Form count"), F("forme_sprite", "Form sprite"),
            F("form_stats_index", "Form stats index")]));
        Groups.Add(new("TM and tutor compatibility (numbers of the checked boxes)", [F("tmhm", "TMs/HMs", wide: true), F("type_tutors", "Type tutors", wide: true),
            F("special_tutors1", "Tutors 1", wide: true), F("special_tutors2", "Tutors 2", wide: true), F("special_tutors3", "Tutors 3", wide: true),
            F("special_tutors4", "Tutors 4", wide: true)]));
        UpdateHeading();
    }

    private void UpdateHeading()
    {
        var row = Selected;
        if (row == null)
            return;
        int entry = int.Parse(row[0]);
        int bst = new[] { "hp", "atk", "def", "spa", "spd", "spe" }.Sum(c => int.TryParse(row[Col(c)], out int v) ? v : 0);
        Heading = $"{row[1]}  ·  BST {bst}";
        // Forms are personal entries after the species; their sprite is the base species' form sprite, as the level-up editor picks it.
        int[] specForm = main.Config.Personal.GetSpeciesForm(entry, main.Config);
        Sprite = entry > main.Config.MaxSpeciesID && specForm[0] > 0 ? Sprites.Pokemon(specForm[0], specForm[1] + 1) : Sprites.Pokemon(entry, 0);
    }

    [RelayCommand]
    private async Task Save()
    {
        await Table.SaveCommand.ExecuteAsync(null);
        RefreshEntries();
    }

    [RelayCommand]
    private void Revert()
    {
        Table.RevertCommand.Execute(null);
        RefreshEntries();
    }

    [RelayCommand]
    private void TableView() => main.Dialogs.Show(new TableEditorViewModel(main, "Personal Stats", [("Personal", DataTable.Personal)],
        Editors.PersonalRandomizers));
}

/// <summary>
/// The trainer page (pk3DS's trainer editor): a list of trainers, the selected trainer's data, and a card per team slot with its sprite.
/// </summary>
public sealed partial class TrainerPageViewModel : ObservableObject
{
    private readonly MainViewModel main;
    private readonly string[] species, items, moves, classes;
    private static readonly string[] BattleTypes = ["0 Single", "1 Double", "2 Triple", "3 Rotation", "4 Horde"];
    private static readonly string[] AbilityChoices = ["0 Any (1 or 2)", "1 Ability 1", "2 Ability 2", "3 Hidden ability"];
    private static readonly string[] GenderChoices = ["0 - / Genderless/Random", "1 ♂ / Male", "2 ♀ / Female"]; // RSTE's list

    public string Title => "Trainers";
    public TablePageViewModel Trainers { get; }
    public TablePageViewModel Teams { get; }
    public ObservableCollection<TableRow> Entries { get; } = [];
    public ObservableCollection<FormField> TrainerFields { get; } = [];
    public ObservableCollection<TeamSlot> Slots { get; } = [];

    [ObservableProperty]
    public partial TableRow Selected { get; set; }

    [ObservableProperty]
    public partial string Search { get; set; } = "";

    [ObservableProperty]
    public partial string Status { get; set; } = "";

    public TrainerPageViewModel(MainViewModel main)
    {
        this.main = main;
        Trainers = new TablePageViewModel(main, "Trainers", DataTable.Trainer);
        Teams = new TablePageViewModel(main, "Teams", DataTable.TrainerPokemon);
        TablePageViewModel.Link([Trainers, Teams]);
        species = Names.Numbered(main.Config.GetText(TextName.SpeciesNames)[..(main.Config.MaxSpeciesID + 1)]);
        items = Names.Numbered(main.Config.GetText(TextName.ItemNames));
        moves = Names.Numbered(main.Config.GetText(TextName.MoveNames));
        classes = Names.Numbered(main.Config.GetText(TextName.TrainerClasses));
        foreach (var page in new[] { Trainers, Teams })
            page.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(TablePageViewModel.Status)) Status = page.Status; };
        RefreshEntries();
    }

    public bool HasChanges => Trainers.HasChanges || Teams.HasChanges;

    private static int Col(TablePageViewModel page, string name) => page.Columns.Select((c, i) => (c, i)).First(z => z.c.Name == name).i;

    partial void OnSearchChanged(string value) => RefreshEntries();

    private void RefreshEntries()
    {
        string index = Selected?[0];
        Entries.Clear();
        string s = Search.Trim();
        foreach (var row in Trainers.Rows.Where(r => s.Length == 0 || r[1].Contains(s, StringComparison.OrdinalIgnoreCase)
                     || r[Col(Trainers, "class_name")].Contains(s, StringComparison.OrdinalIgnoreCase) || r[0] == s))
            Entries.Add(row);
        Selected = Entries.FirstOrDefault(r => r[0] == index) ?? Entries.FirstOrDefault();
    }

    partial void OnSelectedChanged(TableRow value) => ShowTrainer();

    private void ShowTrainer()
    {
        TrainerFields.Clear();
        Slots.Clear();
        var row = Selected;
        if (row == null)
            return;
        FormField F(string column, string label, IReadOnlyList<string> choices = null, bool flag = false) =>
            new(row, Col(Trainers, column), label, Trainers.Columns[Col(Trainers, column)].ReadOnly, choices, flag);
        foreach (var field in new[]
        {
            F("class", "Class", classes), F("battle_type", "Battle type", BattleTypes), F("has_items", "Team holds items", flag: true),
            F("has_moves", "Team has custom moves", flag: true), F("ai", "AI"), F("money", "Money"), F("prize", "Prize item", items),
            F("item1", "Item 1", items), F("item2", "Item 2", items), F("item3", "Item 3", items), F("item4", "Item 4", items),
            F("healer", "Healer", flag: true), F("team_size", "Team size"),
        })
            TrainerFields.Add(field);

        string trainer = row[0];
        foreach (var slot in Teams.Rows.Where(r => r[0] == trainer))
        {
            TableRow s = slot;
            FormField T(string column, string label, IReadOnlyList<string> choices = null) =>
                new(s, Col(Teams, column), label, Teams.Columns[Col(Teams, column)].ReadOnly, choices);
            var card = new TeamSlot(s, int.Parse(s[Col(Teams, "slot")]));
            card.Fields =
            [
                T("species", "Species", species), T("form", "Form"), T("level", "Level"), T("item", "Held item", items),
                T("ability", "Ability", AbilityChoices), T("gender", "Gender", GenderChoices), T("ivs", "IVs"),
                T("move1", "Move 1", moves), T("move2", "Move 2", moves), T("move3", "Move 3", moves), T("move4", "Move 4", moves),
            ];
            foreach (var f in card.Fields.Where(f => f.Label is "Species" or "Form"))
                f.PropertyChanged += (_, _) => card.UpdateSprite(int.Parse(s[Col(Teams, "species")]), int.Parse(s[Col(Teams, "form")]));
            card.UpdateSprite(int.Parse(s[Col(Teams, "species")]), int.Parse(s[Col(Teams, "form")]));
            Slots.Add(card);
        }
    }

    [RelayCommand]
    private void AddSlot()
    {
        if (Selected == null)
            return;
        Teams.SelectedRow = Teams.Rows.FirstOrDefault(r => r[0] == Selected[0]);
        Teams.AddSlotCommand.Execute(null);
        ShowTrainer();
    }

    [RelayCommand]
    private void RemoveSlot(TeamSlot slot)
    {
        Teams.SelectedRow = slot.Row;
        Teams.RemoveSlotCommand.Execute(null);
        ShowTrainer();
    }

    /// <summary>Saves the trainers and their teams (Core writes both together).</summary>
    [RelayCommand]
    private async Task Save()
    {
        await Trainers.SaveCommand.ExecuteAsync(null);
        string saved = Trainers.Status; // Core writes trainers and teams together, so the first save reports the count
        await Teams.SaveCommand.ExecuteAsync(null);
        RefreshEntries();
        ShowTrainer();
        Status = saved;
    }

    [RelayCommand]
    private void Revert()
    {
        Trainers.RevertCommand.Execute(null);
        Teams.RevertCommand.Execute(null);
        RefreshEntries();
        ShowTrainer();
    }

    [RelayCommand]
    private void TableView() => main.Dialogs.Show(new TableEditorViewModel(main, "Trainers", [("Trainers", DataTable.Trainer), ("Teams", DataTable.TrainerPokemon)],
        Editors.TrainerRandomizers));
}

/// <summary>One team slot on the trainer page.</summary>
public sealed partial class TeamSlot(TableRow row, int slot) : ObservableObject
{
    public TableRow Row { get; } = row;
    public string Title { get; } = $"Slot {slot + 1}";
    public IReadOnlyList<FormField> Fields { get; set; } = [];

    [ObservableProperty]
    public partial Bitmap Sprite { get; private set; }

    public void UpdateSprite(int species, int form) => Sprite = Sprites.Pokemon(species, form);
}
