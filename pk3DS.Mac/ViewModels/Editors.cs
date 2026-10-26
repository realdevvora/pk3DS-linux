using System;

namespace pk3DS.Mac.ViewModels;

public enum EditorTab { RomFS, ExeFS, CRO }

/// <summary>An editor button of the main window: its tab and name as in pk3DS, and what it opens.</summary>
public sealed record Editor(EditorTab Tab, string Name, Action<MainViewModel> Open);

public static class Editors
{
    /// <summary>The editors, in the order of the pk3DS main window. Editors not in Core (title screen, move tutors, Gen 7) are left out.</summary>
    public static readonly Editor[] All =
    [
    ];
}
