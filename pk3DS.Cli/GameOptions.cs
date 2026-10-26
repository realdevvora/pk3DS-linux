using System;
using System.IO;
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

        int lang = EnglishLanguage;
        if (a.Get("lang") is { } l && !int.TryParse(l, out lang))
            throw new UsageException($"Invalid language '{l}'; use 0-9 (2 is English).");

        try
        {
            return GameConfig.Open(romfs, exefs, lang);
        }
        catch (ArgumentException e)
        {
            throw new UsageException(e.Message);
        }
    }
}
