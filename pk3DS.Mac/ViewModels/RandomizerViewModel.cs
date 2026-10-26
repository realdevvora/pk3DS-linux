using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using pk3DS.Core;

namespace pk3DS.Mac.ViewModels;

/// <summary>
/// A randomize dialog generated from a Core options record: one control per property, with the record's defaults (the pk3DS forms').
/// </summary>
public sealed partial class RandomizerViewModel : ObservableObject
{
    private readonly MainViewModel main;
    private readonly Type optionsType;
    private readonly Func<GameConfig, object, int> run;

    public string Title { get; }
    public IReadOnlyList<RandomizerOption> Options { get; }

    /// <summary>Raised after a run that changed the game files.</summary>
    public event Action Completed;

    [ObservableProperty]
    public partial string Seed { get; set; } = "";

    [ObservableProperty]
    public partial string Status { get; set; } = "";

    public static RandomizerViewModel Create<T>(MainViewModel main, string title, Func<GameConfig, T, int> run) where T : new() =>
        new(main, title, typeof(T), (config, options) => run(config, (T)options));

    private RandomizerViewModel(MainViewModel main, string title, Type optionsType, Func<GameConfig, object, int> run)
    {
        this.main = main;
        this.optionsType = optionsType;
        this.run = run;
        Title = title;
        object defaults = Activator.CreateInstance(optionsType);
        Options = [.. optionsType.GetProperties().Where(p => p.CanWrite && p.Name != "EqualityContract").Select(p => new RandomizerOption(p, p.GetValue(defaults)))];
    }

    [RelayCommand]
    private async Task Run()
    {
        int? seed = null;
        if (Seed.Trim().Length != 0)
        {
            if (!int.TryParse(Seed.Trim(), out int s))
            {
                Status = $"Invalid seed '{Seed}'.";
                return;
            }
            seed = s;
        }
        object options = Activator.CreateInstance(optionsType);
        foreach (var option in Options)
            option.Property.SetValue(options, option.GetValue());

        int changed = 0;
        if (!await main.Run(Title, _ =>
            {
                if (seed is { } s)
                    Util.ReseedRand(s);
                changed = run(main.Config, options);
            }))
        {
            Status = "Failed; see the Output tab.";
            return;
        }
        Status = $"Done: {changed} entries changed" + (seed == null ? "" : $" (seed {seed})");
        main.AppendLog($"{Title}: {changed} entries changed");
        Completed?.Invoke();
    }

    /// <summary>"SimilarBST" -> "Similar BST", "ForceFullyEvolvedLevel" -> "Force fully evolved level".</summary>
    internal static string Label(string name)
    {
        var words = new List<string>();
        var word = new StringBuilder();
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            bool boundary = i > 0 && ((char.IsUpper(c) && (char.IsLower(name[i - 1]) || char.IsDigit(name[i - 1])))
                || (char.IsUpper(c) && char.IsUpper(name[i - 1]) && i + 1 < name.Length && char.IsLower(name[i + 1]))
                || (char.IsDigit(c) && char.IsLetter(name[i - 1])));
            if (boundary)
            {
                words.Add(word.ToString());
                word.Clear();
            }
            word.Append(c);
        }
        words.Add(word.ToString());
        return string.Join(' ', words.Select((w, i) => i == 0 || w.All(char.IsUpper) ? w : w.ToLowerInvariant()));
    }
}

/// <summary>One option of a randomizer: a checkbox, a number or a choice.</summary>
public sealed partial class RandomizerOption : ObservableObject
{
    public PropertyInfo Property { get; }
    public string Label { get; }
    public bool IsFlag => Property.PropertyType == typeof(bool);
    public bool IsChoice => Property.PropertyType.IsEnum;
    public bool IsNumber => !IsFlag && !IsChoice;
    public bool IsWholeNumber => Property.PropertyType == typeof(int);
    public IReadOnlyList<string> Choices { get; }

    [ObservableProperty]
    public partial bool Flag { get; set; }

    [ObservableProperty]
    public partial decimal? Number { get; set; }

    [ObservableProperty]
    public partial int Choice { get; set; }

    public RandomizerOption(PropertyInfo property, object value)
    {
        Property = property;
        Label = RandomizerViewModel.Label(property.Name);
        Choices = IsChoice ? [.. Enum.GetNames(property.PropertyType).Select(RandomizerViewModel.Label)] : [];
        if (IsFlag)
            Flag = (bool)value;
        else if (IsChoice)
            Choice = Array.IndexOf(Enum.GetValues(property.PropertyType), value);
        else
            Number = Convert.ToDecimal(value);
    }

    public object GetValue()
    {
        if (IsFlag)
            return Flag;
        if (IsChoice)
            return Enum.GetValues(Property.PropertyType).GetValue(Choice);
        decimal n = Number ?? 0;
        return IsWholeNumber ? (object)(int)Math.Max(0, n) : n;
    }
}
