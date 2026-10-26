using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using pk3DS.Core;
using pk3DS.Core.Structures;

namespace pk3DS.Mac.ViewModels;

/// <summary>The shiny rate patch in code.bin (as the pk3DS Shiny Rate window).</summary>
public sealed partial class ShinyViewModel : ObservableObject
{
    private readonly MainViewModel main;
    private readonly CodeBin code;
    private readonly ShinyRate6 shiny;

    public bool AlwaysAvailable { get; }

    [ObservableProperty]
    public partial string Current { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Chance))]
    public partial decimal? Rerolls { get; set; } = 1;

    [ObservableProperty]
    public partial bool AlwaysShiny { get; set; }

    [ObservableProperty]
    public partial string Status { get; set; } = "";

    public string Chance => $"~{ShinyRate6.Chance((int)(Rerolls ?? 0)):P} per Pokémon";

    public ShinyViewModel(MainViewModel main)
    {
        this.main = main;
        code = CodeBin.Load(main.Config.ExeFS);
        shiny = new ShinyRate6(code);
        AlwaysAvailable = shiny.AlwaysShiny != null;
        AlwaysShiny = shiny.AlwaysShiny == true;
        if (shiny.Rerolls is { } r)
            Rerolls = r;
        Describe();
    }

    private void Describe() => Current = shiny.IsOriginal ? "Rerolls: original (the game's own count)"
        : shiny.Rerolls is { } r ? $"Rerolls: {r} (~{ShinyRate6.Chance(r):P} per Pokémon)"
        : "Rerolls: patched with an unknown instruction";

    [RelayCommand]
    private async Task Save()
    {
        int requested = (int)Math.Max(1, Rerolls ?? 1);
        int written = shiny.SetRerolls(requested);
        if (AlwaysAvailable)
            shiny.AlwaysShiny = AlwaysShiny;
        bool saved = code.Save();
        Rerolls = written;
        Describe();
        Status = (written == requested ? "" : $"Raised to {written}, the next count the patch can encode. ") + (saved ? "code.bin written." : "code.bin unchanged.");
        main.AppendLog("Shiny rate: " + Status);
        await Task.CompletedTask;
    }

    [RelayCommand]
    private void RestoreOriginal()
    {
        shiny.Restore();
        if (AlwaysAvailable)
            shiny.AlwaysShiny = AlwaysShiny = false;
        bool saved = code.Save();
        Describe();
        Status = saved ? "Original restored; code.bin written." : "Already original.";
        main.AppendLog("Shiny rate: " + Status);
    }
}
