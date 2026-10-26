using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using pk3DS.Core;
using pk3DS.Core.CTR;
using pk3DS.Mac.Services;

namespace pk3DS.Mac.ViewModels;

/// <summary>
/// The main window: the open game, its log and progress, and the menu actions. Every action is a Core call.
/// </summary>
public partial class MainViewModel(IDialogs dialogs) : ObservableObject
{
    private const int EnglishLanguage = 2;

    /// <summary>The language list of the pk3DS main window, in game order.</summary>
    public static readonly string[] Languages = ["カタカナ", "漢字", "English", "Français", "Italiano", "Deutsch", "Español", "한국", "汉字简化方案", "漢字簡化方案"];

    public IDialogs Dialogs { get; } = dialogs;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGameOpen), nameof(Title))]
    public partial GameConfig Config { get; private set; }

    /// <summary>The folder holding the extracted romfs and exefs.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    public partial string GameDir { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    public partial bool IsBusy { get; private set; }

    [ObservableProperty]
    public partial string Log { get; private set; } = "";

    [ObservableProperty]
    public partial int ProgressMax { get; private set; } = 1;

    [ObservableProperty]
    public partial int ProgressValue { get; private set; }

    [ObservableProperty]
    public partial int Language { get; set; } = EnglishLanguage;

    /// <summary>The tables in code.bin can only be edited while it is decompressed, as in pk3DS.</summary>
    [ObservableProperty]
    public partial bool CodeBinDecompressed { get; private set; }

    public bool IsGameOpen => Config != null;
    public bool IsIdle => !IsBusy;
    public string Title => Config == null ? "pk3DS" : $"pk3DS - {Config.Version} - {Path.GetFileName(GameDir)}";

    public IProgressSink Progress => new UiProgressSink(AppendLog, max => ProgressMax = Math.Max(max, 1), value => ProgressValue = value);

    public void AppendLog(string message) => Log += message + Environment.NewLine;

    /// <summary>Runs a Core operation on a background thread with the window disabled, and reports a failure instead of crashing.</summary>
    public async Task<bool> Run(string what, Action<IProgressSink> action)
    {
        IsBusy = true;
        AppendLog(what + "...");
        try
        {
            var progress = Progress;
            await Task.Run(() => action(progress));
            return true;
        }
        catch (Exception e)
        {
            AppendLog($"{what} failed: {e.Message}");
            await Dialogs.Alert($"{what} failed", e.Message);
            return false;
        }
        finally
        {
            ProgressValue = 0;
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task OpenGame()
    {
        string dir = await Dialogs.PickFolder("Open a folder with the extracted romfs and exefs");
        if (dir != null)
            await Open(dir);
    }

    public async Task Open(string dir)
    {
        GameConfig config = null;
        bool ok = await Run($"Opening {dir}", progress =>
        {
            config = GameConfig.Open(Path.Combine(dir, "romfs"), Path.Combine(dir, "exefs"), Language);
            // Back up the original files once, as pk3DS does when a game is opened; the backup sits next to the game folder.
            config.BackupFiles(root: BackupRoot(dir), progress: progress);
        });
        if (!ok)
            return;
        GameDir = dir;
        Config = config;
        RefreshCodeBinState();
        AppendLog($"Opened {config.Version}. Backup: {Path.Combine(BackupRoot(dir), Path.GetFileName(dir))}");
    }

    private static string BackupRoot(string gameDir) => Path.Combine(Path.GetDirectoryName(Path.GetFullPath(gameDir)), "backup");

    public void RefreshCodeBinState()
    {
        string path = new[] { ".code.bin", "code.bin" }.Select(f => Path.Combine(Config.ExeFS, f)).FirstOrDefault(File.Exists);
        CodeBinDecompressed = path != null && new FileInfo(path).Length % 0x200 == 0;
    }

    partial void OnLanguageChanged(int value)
    {
        if (Config != null && !IsBusy)
            _ = Open(GameDir);
    }

    [RelayCommand]
    private async Task ExtractRom()
    {
        string rom = await Dialogs.PickFile("Choose a decrypted .3ds or .cxi", "3ds", "cxi", "cci");
        if (rom == null)
            return;
        string parent = await Dialogs.PickFolder("Choose where to create the extracted game folder");
        if (parent == null)
            return;
        string dir = Path.Combine(parent, Path.GetFileNameWithoutExtension(rom));
        bool ok = await Run($"Extracting {Path.GetFileName(rom)}", progress =>
        {
            if (rom.EndsWith(".cxi", StringComparison.OrdinalIgnoreCase))
                new NCCH().ExtractNCCHFromFile(rom, dir, progress);
            else
                new NCSD().ExtractFilesFromNCSD(rom, dir, progress);
        });
        if (ok)
            await Open(dir);
    }

    [RelayCommand]
    private async Task BuildRom()
    {
        if (Config == null)
            return;
        RefreshCodeBinState();
        if (CodeBinDecompressed && !await Dialogs.Confirm("code.bin is decompressed",
                "The game needs it compressed. Compress it now? This takes about a minute and a half."))
            return;
        string output = await Dialogs.PickSaveFile("Save the .3ds", Path.GetFileName(GameDir) + ".3ds", "3ds");
        if (output == null)
            return;
        string serial = ReadSerial(Path.Combine(GameDir, "exheader.bin"));
        await Run($"Building {Path.GetFileName(output)}", progress =>
        {
            if (CodeBinDecompressed)
                _ = new BLZCoder(["-en", CodeBinPath()], progress);
            if (!CTRUtil.BuildROM(true, "Nintendo", Config.ExeFS, Config.RomFS, Path.Combine(GameDir, "exheader.bin"), serial, output, false, progress))
                throw new InvalidOperationException("The ExeFS, RomFS or exheader.bin was not found.");
        });
        RefreshCodeBinState();
    }

    /// <summary>The game's serial (CTR-P-XXXX), looked up from its title ID as pk3DS does when building.</summary>
    private static string ReadSerial(string exheader) => new Exheader(exheader).GetSerial();

    public string CodeBinPath() =>
        new[] { ".code.bin", "code.bin" }.Select(f => Path.Combine(Config.ExeFS, f)).First(File.Exists);

    [RelayCommand]
    private async Task DecompressCodeBin()
    {
        if (Config == null || CodeBinDecompressed)
            return;
        await Run("Decompressing code.bin", progress => _ = new BLZCoder(["-d", CodeBinPath()], progress));
        RefreshCodeBinState();
    }

    [RelayCommand]
    private async Task CompressCodeBin()
    {
        if (Config == null || !CodeBinDecompressed)
            return;
        await Run("Compressing code.bin (about a minute and a half)", progress => _ = new BLZCoder(["-en", CodeBinPath()], progress));
        RefreshCodeBinState();
    }

    [RelayCommand]
    private async Task UpdateCroHashes()
    {
        if (Config == null)
            return;
        // As pk3DS's "rebuild CRO/CRR" menu item: the CROs are rehashed, the signed CRR is left alone.
        await Run("Updating CRO hashes", progress =>
            CRO.E_HashCRR(Path.Combine(Config.RomFS, ".crr", "static.crr"), Config.RomFS, true, false, progress));
    }

    [RelayCommand]
    private async Task RestoreOriginals()
    {
        if (Config == null || !await Dialogs.Confirm("Restore original files", "Replace the game's files with the backup made when it was first opened? Edits are lost."))
            return;
        string[] counts = null;
        if (await Run("Restoring original files", progress => counts = Config.RestoreFiles(BackupRoot(GameDir), progress)))
        {
            AppendLog(string.Join(Environment.NewLine, counts ?? []));
            await Open(GameDir);
        }
    }

    [RelayCommand]
    private void ClearLog() => Log = "";
}
