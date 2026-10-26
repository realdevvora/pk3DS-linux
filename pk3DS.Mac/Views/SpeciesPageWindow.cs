using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Media;
using pk3DS.Mac.ViewModels;

namespace pk3DS.Mac.Views;

/// <summary>The species page: entry list, sprite and the selected entry's fields in groups.</summary>
public sealed class SpeciesPageWindow : Window
{
    private readonly StackPanel groups = new();

    public SpeciesPageWindow()
    {
        Width = 1100;
        Height = 720;
        DataContextChanged += (_, _) => Build();
    }

    private void Build()
    {
        if (DataContext is not SpeciesPageViewModel vm)
            return;
        Title = vm.Title;
        var header = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 12,
            Margin = new(0, 0, 0, 12),
            Children =
            {
                FormControls.Sprite(96, nameof(vm.Sprite)),
                new TextBlock { FontSize = 20, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, [!TextBlock.TextProperty] = new Binding(nameof(vm.Heading)) },
            },
        };
        var toolbar = FormControls.Toolbar(
            new Button { Content = "Save", Command = vm.SaveCommand, [!IsEnabledProperty] = new Binding("Table.HasChanges") },
            new Button { Content = "Revert", Command = vm.RevertCommand, [!IsEnabledProperty] = new Binding("Table.HasChanges") },
            new Button { Content = "Table view…", Command = vm.TableViewCommand },
            new TextBlock { Foreground = Brushes.Gray, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, [!TextBlock.TextProperty] = new Binding("Table.Status") });

        var right = new DockPanel();
        DockPanel.SetDock(toolbar, Dock.Top);
        DockPanel.SetDock(header, Dock.Top);
        right.Children.Add(toolbar);
        right.Children.Add(header);
        right.Children.Add(new ScrollViewer { Content = groups });

        var root = new DockPanel { Margin = new(8) };
        var list = FormControls.EntryList(vm, nameof(vm.Entries), nameof(vm.Selected), nameof(vm.Search), row => $"{row[0],4}  {row[1]}");
        DockPanel.SetDock(list, Dock.Left);
        root.Children.Add(list);
        root.Children.Add(right);
        Content = root;

        vm.Groups.CollectionChanged += (_, _) => ShowGroups(vm);
        ShowGroups(vm);
    }

    private void ShowGroups(SpeciesPageViewModel vm)
    {
        groups.Children.Clear();
        foreach (var g in vm.Groups)
            groups.Children.Add(FormControls.Group(g.Title, g.Fields));
    }
}
