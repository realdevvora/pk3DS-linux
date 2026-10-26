using System.Threading.Tasks;

namespace pk3DS.Mac.Services;

/// <summary>File pickers and message boxes, behind an interface so view models do not depend on windows.</summary>
public interface IDialogs
{
    Task<string> PickFolder(string title);
    Task<string> PickFile(string title, params string[] extensions);
    Task<string> PickSaveFile(string title, string suggestedName, string extension);
    Task Alert(string title, string message);
    Task<bool> Confirm(string title, string message);
    void Show(object viewModel);
}
