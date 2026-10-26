using Avalonia.Controls;
using pk3DS.Mac.ViewModels;

namespace pk3DS.Mac.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => AddEditorButtons();
    }

    /// <summary>One button per editor, on the tab where pk3DS has it.</summary>
    private void AddEditorButtons()
    {
        if (DataContext is not MainViewModel vm)
            return;
        foreach (var panel in new[] { RomFSButtons, ExeFSButtons, CROButtons })
            panel.Children.Clear();
        foreach (var editor in Editors.All)
        {
            var button = new Button { Content = editor.Name, Width = 160, Margin = new(4) };
            button.Click += async (_, _) => await vm.OpenEditor(editor);
            (editor.Tab switch { EditorTab.ExeFS => ExeFSButtons, EditorTab.CRO => CROButtons, _ => RomFSButtons }).Children.Add(button);
        }
    }
}
