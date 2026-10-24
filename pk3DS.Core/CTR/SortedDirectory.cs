using System;
using System.IO;
using System.Linq;

namespace pk3DS.Core.CTR;

/// <summary>
/// Directory listings in a fixed, case-insensitive name order. The OS enumeration order differs between
/// filesystems, and it decides the layout of the images built from a folder.
/// </summary>
internal static class SortedDirectory
{
    public static FileInfo[] GetFiles(DirectoryInfo dir) =>
        [.. dir.GetFiles().OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase).ThenBy(f => f.Name, StringComparer.Ordinal)];

    public static DirectoryInfo[] GetDirectories(DirectoryInfo dir) =>
        [.. dir.GetDirectories().OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase).ThenBy(d => d.Name, StringComparer.Ordinal)];

    public static string[] GetFilePaths(string path) => [.. GetFiles(new DirectoryInfo(path)).Select(f => f.FullName)];
}
