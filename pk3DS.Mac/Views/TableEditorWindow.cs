using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using pk3DS.Mac.ViewModels;

namespace pk3DS.Mac.Views;

/// <summary>
/// The table editor. Its grids are built in code because each table has its own columns.
/// </summary>
public sealed class TableEditorWindow : Window
{
    public TableEditorWindow()
    {
        Width = 1000;
        Height = 640;
        DataContextChanged += (_, _) => Build();
    }

    private void Build()
    {
        if (DataContext is not TableEditorViewModel vm)
            return;
        Title = vm.Title;
        var tabs = new TabControl { Margin = new(8) };
        foreach (var page in vm.Pages)
            tabs.Items.Add(new TabItem { Header = page.Name, Content = BuildPage(vm, page) });

        // A single table needs no tab strip.
        Content = vm.Pages.Count == 1 ? ((TabItem)tabs.Items[0]).Content : tabs;
        if (vm.Pages.Count == 1)
            ((TabItem)tabs.Items[0]).Content = null;
    }

    private static Control BuildPage(TableEditorViewModel vm, TablePageViewModel page)
    {
        var grid = new DataGrid
        {
            ItemsSource = page.Rows,
            AutoGenerateColumns = false,
            CanUserSortColumns = false,
            CanUserReorderColumns = false,
            GridLinesVisibility = DataGridGridLinesVisibility.All,
            FrozenColumnCount = 1,
        };
        for (int c = 0; c < page.Columns.Count; c++)
        {
            var (name, readOnly) = page.Columns[c];
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = name,
                Binding = new Binding($"[{c}]") { Mode = readOnly ? BindingMode.OneWay : BindingMode.TwoWay },
                IsReadOnly = readOnly,
                Foreground = readOnly ? Avalonia.Media.Brushes.Gray : null,
            });
        }

        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new(0, 0, 0, 8) };
        toolbar.Children.Add(new TextBox { Watermark = "Filter", Width = 220, [!TextBox.TextProperty] = new Binding(nameof(page.Filter)) { Mode = BindingMode.TwoWay } });
        toolbar.Children.Add(new Button { Content = "Save", Command = page.SaveCommand, [!IsEnabledProperty] = new Binding(nameof(page.HasChanges)) });
        toolbar.Children.Add(new Button { Content = "Revert", Command = page.RevertCommand, [!IsEnabledProperty] = new Binding(nameof(page.HasChanges)) });
        toolbar.Children.Add(new Button { Content = "Export spreadsheet…", Command = page.ExportCommand });
        toolbar.Children.Add(new Button { Content = "Import spreadsheet…", Command = page.ImportCommand });
        foreach (var button in vm.Randomizers)
        {
            var b = new Button { Content = button.Name + "…" };
            b.Click += (_, _) => vm.Randomize(button);
            toolbar.Children.Add(b);
        }

        var status = new TextBlock { Margin = new(0, 8, 0, 0), Foreground = Avalonia.Media.Brushes.Gray, [!TextBlock.TextProperty] = new Binding(nameof(page.Status)) };
        var panel = new DockPanel { DataContext = page, [!IsEnabledProperty] = new Binding("!" + nameof(page.IsBusy)) };
        DockPanel.SetDock(toolbar, Dock.Top);
        DockPanel.SetDock(status, Dock.Bottom);
        panel.Children.Add(toolbar);
        panel.Children.Add(status);
        panel.Children.Add(grid);
        return panel;
    }
}
