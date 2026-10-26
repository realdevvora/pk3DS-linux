using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;

namespace pk3DS.Mac.Services;

/// <summary>The dialogs of the app, shown over the main window.</summary>
public sealed class Dialogs(Window owner) : IDialogs
{
    public async Task<string> PickFolder(string title)
    {
        var folders = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = title });
        return folders.FirstOrDefault()?.TryGetLocalPath();
    }

    public async Task<string> PickFile(string title, params string[] extensions)
    {
        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            FileTypeFilter = [new FilePickerFileType(string.Join(", ", extensions)) { Patterns = [.. extensions.Select(e => "*." + e)] }],
        });
        return files.FirstOrDefault()?.TryGetLocalPath();
    }

    public async Task<string> PickSaveFile(string title, string suggestedName, string extension)
    {
        var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedName,
            DefaultExtension = extension,
        });
        return file?.TryGetLocalPath();
    }

    public Task Alert(string title, string message) => Ask(title, message, "OK", null);

    public Task<bool> Confirm(string title, string message) => Ask(title, message, "OK", "Cancel");

    private Task<bool> Ask(string title, string message, string ok, string cancel)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 460,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8 };
        var okButton = new Button { Content = ok, IsDefault = true };
        okButton.Click += (_, _) => dialog.Close(true);
        if (cancel != null)
        {
            var cancelButton = new Button { Content = cancel, IsCancel = true };
            cancelButton.Click += (_, _) => dialog.Close(false);
            buttons.Children.Add(cancelButton);
        }
        buttons.Children.Add(okButton);
        dialog.Content = new StackPanel
        {
            Margin = new(16),
            Spacing = 16,
            Children = { new SelectableTextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap }, buttons },
        };
        return dialog.ShowDialog<bool>(owner);
    }

    /// <summary>Opens the window for an editor's view model.</summary>
    public void Show(object viewModel)
    {
        Window window = viewModel switch
        {
            _ => throw new System.ArgumentException($"No window for {viewModel.GetType().Name}."),
        };
        window.DataContext = viewModel;
        window.Show(owner);
    }
}
