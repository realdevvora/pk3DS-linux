using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using pk3DS.Mac.ViewModels;

namespace pk3DS.Mac.Views;

/// <summary>A randomize dialog, built from the options of its view model.</summary>
public sealed class RandomizerWindow : Window
{
    public RandomizerWindow()
    {
        Width = 460;
        Height = 600;
        DataContextChanged += (_, _) => Build();
    }

    private void Build()
    {
        if (DataContext is not RandomizerViewModel vm)
            return;
        Title = vm.Title;
        var list = new StackPanel { Spacing = 6, Margin = new(16) };
        foreach (var option in vm.Options)
        {
            Control control;
            if (option.IsFlag)
            {
                control = new CheckBox { Content = option.Label, [!CheckBox.IsCheckedProperty] = new Binding(nameof(option.Flag)) { Mode = BindingMode.TwoWay } };
            }
            else
            {
                Control input = option.IsChoice
                    ? new ComboBox { ItemsSource = option.Choices, MinWidth = 140, [!ComboBox.SelectedIndexProperty] = new Binding(nameof(option.Choice)) { Mode = BindingMode.TwoWay } }
                    : new NumericUpDown { Width = 140, Minimum = 0, Increment = option.IsWholeNumber ? 1 : 0.1m, FormatString = option.IsWholeNumber ? "0" : "0.##", [!NumericUpDown.ValueProperty] = new Binding(nameof(option.Number)) { Mode = BindingMode.TwoWay } };
                control = new DockPanel { Children = { new TextBlock { Text = option.Label, VerticalAlignment = VerticalAlignment.Center, Width = 220 }, input } };
            }
            control.DataContext = option;
            list.Children.Add(control);
        }

        var seed = new TextBox { Watermark = "Seed (optional, repeats a run)", Width = 220, [!TextBox.TextProperty] = new Binding(nameof(vm.Seed)) { Mode = BindingMode.TwoWay } };
        var run = new Button { Content = "Randomize", IsDefault = true, Command = vm.RunCommand };
        var status = new TextBlock { Foreground = Avalonia.Media.Brushes.Gray, TextWrapping = Avalonia.Media.TextWrapping.Wrap, [!TextBlock.TextProperty] = new Binding(nameof(vm.Status)) };
        var footer = new StackPanel { Spacing = 8, Margin = new(16), Children = { new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { seed, run } }, status } };
        var root = new DockPanel();
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);
        root.Children.Add(new ScrollViewer { Content = list });
        Content = root;
    }
}
