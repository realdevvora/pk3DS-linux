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
            window.DataContext = new MainViewModel(new Dialogs(window));
            desktop.MainWindow = window;
        }
        base.OnFrameworkInitializationCompleted();
    }
}
