using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace pk3DS.Mac.ViewModels;

/// <summary>
/// Game text or story text: a list of text files and the lines of the selected one. Saving writes only changed files, as text import does.
/// </summary>
public sealed partial class TextEditorViewModel : ObservableObject
{
    private readonly MainViewModel main;
    private readonly bool story;

    public string Title { get; }
    public ObservableCollection<TextFileItem> Files { get; } = [];
    public ObservableCollection<TextLine> Lines { get; } = [];

    [ObservableProperty]
    public partial TextFileItem SelectedFile { get; set; }

    [ObservableProperty]
    public partial string Search { get; set; } = "";

    [ObservableProperty]
    public partial string Status { get; set; } = "";

    [ObservableProperty]
    public partial bool HasChanges { get; set; }

    public TextEditorViewModel(MainViewModel main, bool story)
    {
        this.main = main;
        this.story = story;
        Title = story ? "Story Text" : "Game Text";
        if (story)
            main.Config.InitializeStoryText();
        LoadFiles();
    }

    private string[][] Text => story ? main.Config.StoryTextStrings : main.Config.GameTextStrings;

    private void LoadFiles()
    {
        int selected = SelectedFile?.Index ?? 0;
        Files.Clear();
        string s = Search.Trim();
        for (int i = 0; i < Text.Length; i++)
        {
            if (s.Length == 0 || Text[i].Any(line => line.Contains(s, StringComparison.OrdinalIgnoreCase)))
                Files.Add(new TextFileItem(i, Text[i].FirstOrDefault() ?? ""));
        }
        SelectedFile = Files.FirstOrDefault(f => f.Index == selected) ?? Files.FirstOrDefault();
        Status = s.Length == 0 ? $"{Text.Length} text files" : $"{Files.Count} of {Text.Length} files contain '{s}'";
    }

    partial void OnSearchChanged(string value) => LoadFiles();

    partial void OnSelectedFileChanged(TextFileItem value)
    {
        Lines.Clear();
        if (value == null)
            return;
        string[] lines = Text[value.Index];
        for (int i = 0; i < lines.Length; i++)
            Lines.Add(new TextLine(this, value.Index, i, lines[i]));
    }

    internal void SetLine(int file, int line, string value)
    {
        Text[file][line] = value;
        HasChanges = true;
        Status = "Edited (not saved)";
    }

    [RelayCommand]
    private async Task Save()
    {
        try
        {
            int changed = story ? main.Config.SaveStoryText() : main.Config.SaveGameText();
            HasChanges = false;
            Status = $"Saved: {changed} files changed";
            main.AppendLog($"{Title}: {changed} files changed");
        }
        catch (Exception e)
        {
            // A malformed control code: the message names the text file.
            Status = $"Not saved: {e.Message}";
            await main.Dialogs.Alert("Not saved", e.Message);
        }
    }

    [RelayCommand]
    private void Revert()
    {
        if (story)
            main.Config.InitializeStoryText();
        else
            main.Config.InitializeGameText();
        HasChanges = false;
        LoadFiles();
        Status = "Reverted";
    }
}

public sealed record TextFileItem(int Index, string FirstLine)
{
    public string Label => $"{Index:D4}  {(FirstLine.Length > 40 ? FirstLine[..40] + "…" : FirstLine)}";
}

/// <summary>One line of a text file; editing it changes the loaded text until saved.</summary>
public sealed class TextLine(TextEditorViewModel editor, int file, int line, string text)
{
    private string text = text;

    public int Number => line;

    public string Text
    {
        get => text;
        set
        {
            if (value == text)
                return;
            text = value;
            editor.SetLine(file, line, value);
        }
    }
}
