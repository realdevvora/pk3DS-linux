using System;
using System.IO;
using System.Linq;
using pk3DS.Core;

namespace pk3DS.Cli;

/// <summary>
/// Loads a <see cref="GameConfig"/> from the <c>--dir</c> / <c>--romfs</c> / <c>--exefs</c> / <c>--lang</c> options shared by game-data commands.
/// </summary>
public static class GameOptions
{
    private const int EnglishLanguage = 2;

    public static readonly string[] ValueOptions = ["dir", "romfs", "exefs", "lang"];

    public const string Synopsis = "(--dir <extracted dir> | --romfs <dir> --exefs <dir>) [--lang <0-9>]";

    public static GameConfig Load(CommandArgs a)
    {
        string dir = a.Get("dir");
        string romfs = a.Get("romfs") ?? (dir == null ? null : Path.Combine(dir, "romfs"));
        string exefs = a.Get("exefs") ?? (dir == null ? null : Path.Combine(dir, "exefs"));
        if (romfs == null || exefs == null)
            throw new UsageException("Specify --dir, or both --romfs and --exefs.");
        if (!Directory.Exists(Path.Combine(romfs, "a")))
            throw new UsageException($"'{romfs}' is not a RomFS folder (no 'a' directory).");
        if (!Directory.Exists(exefs))
            throw new UsageException($"ExeFS folder '{exefs}' does not exist.");

        int lang = EnglishLanguage;
        if (a.Get("lang") is { } l && (!int.TryParse(l, out lang) || lang is < 0 or > 9))
            throw new UsageException($"Invalid language '{l}'; use 0-9 (2 is English).");

        // Games are told apart by how many numbered files the 'a' tree holds.
        int fileCount = Directory.GetFiles(Path.Combine(romfs, "a"), "*", SearchOption.AllDirectories)
            .Count(f => Path.GetFileName(f).Length == 1);
        var config = new GameConfig(fileCount);
        if (config.Version == GameVersion.Invalid)
            throw new UsageException($"Unrecognized game: found {fileCount} data files in '{Path.Combine(romfs, "a")}'.");
        if ((config.XY || config.ORAS) && lang > 7)
            throw new UsageException("Languages above 7 are not available in this game.");

        config.Initialize(romfs, exefs, lang);
        return config;
    }
}
