using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using pk3DS.Core;

namespace pk3DS.Mac.ViewModels;

/// <summary>A randomize button of an editor, as pk3DS has them inside the editor of the table they change.</summary>
public sealed record RandomizerButton(string Name, Func<MainViewModel, RandomizerViewModel> Create);

/// <summary>An editor window over one or more data tables (for example trainers and their teams), one tab each.</summary>
public sealed class TableEditorViewModel(MainViewModel main, string title, IEnumerable<(string Name, DataTable Table)> tables, IEnumerable<RandomizerButton> randomizers = null)
{
    public string Title { get; } = title;
    public MainViewModel Main { get; } = main;
    public IReadOnlyList<TablePageViewModel> Pages { get; } = [.. tables.Select(t => new TablePageViewModel(main, t.Name, t.Table))];
    public IReadOnlyList<RandomizerButton> Randomizers { get; } = [.. randomizers ?? []];

    /// <summary>Opens a randomizer; when it has run, the pages show the randomized data.</summary>
    public void Randomize(RandomizerButton button)
    {
        var vm = button.Create(Main);
        vm.Completed += () => { foreach (var page in Pages) page.Reload(); };
        Main.Dialogs.Show(vm);
    }
}

/// <summary>
/// One table in a grid: the same columns, checks and save as the spreadsheet commands (<see cref="DataTableView"/>).
/// </summary>
public sealed partial class TablePageViewModel : ObservableObject
{
    private readonly MainViewModel main;
    private DataTableView view;
    private TableRow[] all = [];

    public string Name { get; }
    public DataTable Table { get; }

    public TablePageViewModel(MainViewModel main, string name, DataTable table)
    {
        this.main = main;
        Name = name;
        Table = table;
        view = DataTableFile.Open(main.Config, table);
        Load();
    }

    /// <summary>Column names and whether each can be edited, in the spreadsheet's order.</summary>
    public IReadOnlyList<(string Name, bool ReadOnly)> Columns { get; private set; }

    public ObservableCollection<TableRow> Rows { get; } = [];

    [ObservableProperty]
    public partial string Filter { get; set; } = "";

    [ObservableProperty]
    public partial string Status { get; set; } = "";

    [ObservableProperty]
    public partial bool HasChanges { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    private void Load()
    {
        Columns = [.. Enumerable.Range(0, view.ColumnCount).Select(c => (view.ColumnName(c), view.IsReadOnly(c)))];
        all = [.. Enumerable.Range(0, view.RowCount).Select(r => new TableRow(this, r))];
        ApplyFilter();
        HasChanges = false;
    }

    partial void OnFilterChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        Rows.Clear();
        string f = Filter.Trim();
        foreach (var row in all)
        {
            if (f.Length == 0 || Enumerable.Range(0, Columns.Count).Any(c => row[c].Contains(f, StringComparison.OrdinalIgnoreCase)))
                Rows.Add(row);
        }
        Status = f.Length == 0 ? $"{all.Length} entries" : $"{Rows.Count} of {all.Length} entries";
    }

    internal string Get(int row, int column) => view.Get(row, column);

    /// <summary>Applies a cell edit through Core's check; an invalid value is reported and the cell keeps its value.</summary>
    internal bool TrySet(int row, int column, string value)
    {
        if (value == view.Get(row, column))
            return true;
        try
        {
            view.Set(row, column, value);
            HasChanges = true;
            Status = "Edited (not saved)";
            return true;
        }
        catch (FormatException e)
        {
            Status = $"{Columns[column].Name}: {e.Message}";
            return false;
        }
    }

    [RelayCommand]
    private async Task Save()
    {
        int changed = 0;
        IsBusy = true;
        try
        {
            await Task.Run(() => changed = view.Save());
            Load();
            Status = $"Saved: {changed} entries changed";
            main.AppendLog($"{Name}: {changed} entries changed");
        }
        catch (Exception e)
        {
            Status = $"Not saved: {e.Message}";
            await main.Dialogs.Alert("Not saved", e.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Revert()
    {
        view.Revert();
        Load();
        Status = "Reverted";
    }

    /// <summary>Re-reads the table from the game files, e.g. after a randomizer changed them.</summary>
    public void Reload()
    {
        view = DataTableFile.Open(main.Config, Table);
        Load();
    }

    [RelayCommand]
    private async Task Export()
    {
        string path = await main.Dialogs.PickSaveFile("Export spreadsheet", $"{Table}.tsv".ToLowerInvariant(), "tsv");
        if (path == null)
            return;
        try
        {
            int count = DataTableFile.Dump(main.Config, Table, path);
            Status = $"Exported {count} entries to {Path.GetFileName(path)}";
        }
        catch (Exception e)
        {
            await main.Dialogs.Alert("Export failed", e.Message);
        }
    }

    [RelayCommand]
    private async Task Import()
    {
        if (HasChanges && !await main.Dialogs.Confirm("Import spreadsheet", "Unsaved edits in this table will be discarded."))
            return;
        string path = await main.Dialogs.PickFile("Import spreadsheet", "tsv", "txt");
        if (path == null)
            return;
        try
        {
            int changed = DataTableFile.Import(main.Config, Table, path);
            Reload();
            Status = $"Imported {Path.GetFileName(path)}: {changed} entries changed";
            main.AppendLog($"{Name}: imported {Path.GetFileName(path)}, {changed} entries changed");
        }
        catch (Exception e)
        {
            Reload();
            await main.Dialogs.Alert("Import failed", e.Message);
        }
    }
}

/// <summary>A grid row; cell <c>c</c> is bound as <c>[c]</c>.</summary>
public sealed class TableRow(TablePageViewModel page, int row) : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;

    public string this[int column]
    {
        get => page.Get(row, column);
        set
        {
            if (!page.TrySet(row, column, value))
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]")); // show the kept value again
        }
    }
}
