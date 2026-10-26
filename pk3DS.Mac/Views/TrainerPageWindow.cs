using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using pk3DS.Mac.ViewModels;

namespace pk3DS.Mac.Views;

/// <summary>The trainer page: trainer list, the trainer's data, and a card per team slot.</summary>
public sealed class TrainerPageWindow : Window
{
    private readonly StackPanel trainer = new();
    private readonly WrapPanel slots = new();

    public TrainerPageWindow()
    {
        Width = 1200;
        Height = 760;
        DataContextChanged += (_, _) => Build();
    }

    private void Build()
    {
        if (DataContext is not TrainerPageViewModel vm)
            return;
        Title = vm.Title;
        var toolbar = FormControls.Toolbar(
            new Button { Content = "Save", Command = vm.SaveCommand },
            new Button { Content = "Revert", Command = vm.RevertCommand },
            new Button { Content = "Add slot", Command = vm.AddSlotCommand },
            new Button { Content = "Table view…", Command = vm.TableViewCommand },
            new TextBlock { Foreground = Brushes.Gray, VerticalAlignment = VerticalAlignment.Center, [!TextBlock.TextProperty] = new Binding(nameof(vm.Status)) });

        var right = new DockPanel();
        DockPanel.SetDock(toolbar, Dock.Top);
        right.Children.Add(toolbar);
        right.Children.Add(new ScrollViewer { Content = new StackPanel { Children = { trainer, slots } } });

        var root = new DockPanel { Margin = new(8) };
        var list = FormControls.EntryList(vm, nameof(vm.Entries), nameof(vm.Selected), nameof(vm.Search), row => $"{row[0],4}  {row[1]} ({row[3]})");
        DockPanel.SetDock(list, Dock.Left);
        root.Children.Add(list);
        root.Children.Add(right);
        Content = root;

        vm.Slots.CollectionChanged += (_, _) => Show(vm);
        Show(vm);
    }

    private void Show(TrainerPageViewModel vm)
    {
        trainer.Children.Clear();
        trainer.Children.Add(FormControls.Group("Trainer", vm.TrainerFields));
        slots.Children.Clear();
        foreach (var slot in vm.Slots)
        {
            var remove = new Button { Content = "Remove", Command = vm.RemoveSlotCommand, CommandParameter = slot, HorizontalAlignment = HorizontalAlignment.Right };
            var fields = new WrapPanel { Width = 470 };
            foreach (var f in slot.Fields)
                fields.Children.Add(FormControls.Field(f));
            var head = new DockPanel { Children = { FormControls.Sprite(64, nameof(slot.Sprite), slot),
                new TextBlock { Text = slot.Title, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new(8, 0) }, remove } };
            slots.Children.Add(new Border
            {
                BorderBrush = Brushes.Gray,
                BorderThickness = new(1),
                CornerRadius = new(6),
                Padding = new(8),
                Margin = new(0, 0, 8, 8),
                Child = new StackPanel { Children = { head, fields } },
            });
        }
    }
}
