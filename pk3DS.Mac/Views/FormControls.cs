using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using pk3DS.Mac.ViewModels;

namespace pk3DS.Mac.Views;

/// <summary>Controls shared by the form pages.</summary>
internal static class FormControls
{
    /// <summary>A labeled editor for one field: a list of names, a checkbox or a text box.</summary>
    public static Control Field(FormField field)
    {
        Control input;
        if (field.IsFlag)
        {
            input = new CheckBox { Content = field.Label, IsEnabled = !field.IsReadOnly, [!CheckBox.IsCheckedProperty] = new Binding(nameof(field.Flag)) { Mode = BindingMode.TwoWay } };
            input.DataContext = field;
            return input;
        }
        input = field.IsChoice
            ? new ComboBox { ItemsSource = field.Choices, Width = 200, IsEnabled = !field.IsReadOnly, [!ComboBox.SelectedIndexProperty] = new Binding(nameof(field.Index)) { Mode = BindingMode.TwoWay } }
            : new TextBox { Width = field.IsWide ? 520 : 90, IsReadOnly = field.IsReadOnly, [!TextBox.TextProperty] = new Binding(nameof(field.Value)) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.LostFocus } };
        var panel = new StackPanel { Spacing = 2, Margin = new(0, 0, 12, 8), Children = { new TextBlock { Text = field.Label, FontSize = 11, Foreground = Brushes.Gray }, input } };
        panel.DataContext = field;
        return panel;
    }

    public static Control Group(string title, System.Collections.Generic.IEnumerable<FormField> fields)
    {
        var wrap = new WrapPanel();
        foreach (var f in fields)
            wrap.Children.Add(Field(f));
        return new StackPanel { Margin = new(0, 0, 0, 12), Children = { new TextBlock { Text = title, FontWeight = FontWeight.SemiBold, Margin = new(0, 0, 0, 6) }, wrap } };
    }

    /// <summary>A searchable list of entries (rows) on the left of a page.</summary>
    public static Control EntryList(object viewModel, string itemsPath, string selectedPath, string searchPath, System.Func<TableRow, string> label)
    {
        var search = new TextBox { Watermark = "Search", Margin = new(0, 0, 0, 6), [!TextBox.TextProperty] = new Binding(searchPath) { Mode = BindingMode.TwoWay } };
        var list = new ListBox
        {
            ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<TableRow>((row, _) => new TextBlock { Text = row == null ? "" : label(row) }),
            [!ItemsControl.ItemsSourceProperty] = new Binding(itemsPath),
            [!ListBox.SelectedItemProperty] = new Binding(selectedPath) { Mode = BindingMode.TwoWay },
        };
        var panel = new DockPanel { Width = 260, Margin = new(0, 0, 8, 0), DataContext = viewModel };
        DockPanel.SetDock(search, Dock.Top);
        panel.Children.Add(search);
        panel.Children.Add(list);
        return panel;
    }

    /// <summary>A sprite scaled up without smoothing, so the small pixel sprites stay crisp.</summary>
    public static Image Sprite(double size, string path, object source = null)
    {
        var image = new Image { Width = size, Height = size, [!Image.SourceProperty] = new Binding(path) };
        if (source != null)
            image.DataContext = source;
        RenderOptions.SetBitmapInterpolationMode(image, Avalonia.Media.Imaging.BitmapInterpolationMode.None);
        return image;
    }

    public static StackPanel Toolbar(params Control[] controls)
    {
        var bar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new(0, 0, 0, 8) };
        foreach (var c in controls)
            bar.Children.Add(c);
        return bar;
    }
}
