using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using pk3DS.Mac.Services;
using pk3DS.Mac.ViewModels;
using pk3DS.Mac.Views;

namespace pk3DS.Mac;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow();
            var vm = new MainViewModel(new Dialogs(window));
            window.DataContext = vm;
            desktop.MainWindow = window;
            // A game folder given on the command line opens at launch.
            if (desktop.Args is [{ } dir, ..] && System.IO.Directory.Exists(dir))
                window.Opened += async (_, _) => await vm.Open(dir);
        }
        base.OnFrameworkInitializationCompleted();
    }
}
