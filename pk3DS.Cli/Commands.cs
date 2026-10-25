using System;
using System.IO;
using System.Linq;
using System.Text;
using pk3DS.Core;
using pk3DS.Core.CTR;

namespace pk3DS.Cli;

/// <summary>
/// Maps each command-line operation onto the corresponding <c>pk3DS.Core</c> call.
/// </summary>
public static class Commands
{
    private static readonly string[] NoOptions = [];

    private static readonly UTF8Encoding Utf8 = new(false);

    public static readonly Command[] All =
    [
        new("ncsd", "extract", "<rom.3ds> <outDir>", "Extract exheader, exefs, romfs and more from a .3ds cartridge dump",
            NoOptions, a =>
            {
                var (rom, outDir) = Two(a);
                new NCSD().ExtractFilesFromNCSD(rom, outDir, new ConsoleProgressSink());
                return true;
            }),

        new("ncch", "extract", "<game.cxi> <outDir>", "Extract exheader, exefs and romfs from a CXI",
            NoOptions, a =>
            {
                var (cxi, outDir) = Two(a);
                new NCCH().ExtractNCCHFromFile(cxi, outDir, new ConsoleProgressSink());
                return true;
            }),

        new("romfs", "extract", "<romfs.bin> <outDir>", "Extract a RomFS image to a directory",
            NoOptions, a =>
            {
                var (romfs, outDir) = Two(a);
                new RomFS(romfs).ExtractRomFS(outDir, new ConsoleProgressSink());
                return true;
            }),

        new("romfs", "build", "<inDir> <out.bin>", "Build a RomFS image from a directory",
            NoOptions, a =>
            {
                var (inDir, outFile) = Two(a);
                RomFS.BuildRomFS(inDir, outFile, new ConsoleProgressSink());
                return true;
            }),

        new("exefs", "unpack", "<exefs.bin> <outDir>", "Unpack an ExeFS image to a directory of .bin files",
            NoOptions, a =>
            {
                var (exefs, outDir) = Two(a);
                return ExeFS.UnpackExeFS(exefs, outDir);
            }),

        new("exefs", "pack", "<out.bin> <file>...", "Pack up to 8 files into an ExeFS image",
            NoOptions, a =>
            {
                string[] args = a.ExpectAtLeast(2);
                return ExeFS.PackExeFS(args[1..], args[0]);
            }),

        new("garc", "unpack", "<file.garc> <outDir> [--no-decompress]", "Unpack a GARC archive to numbered files (LZSS-compressed entries are written as dec_*.bin)",
            NoOptions, a =>
            {
                var (garc, outDir) = Two(a);
                a.RejectUnknown("no-decompress");
                int count = GARC.GarcUnpack(garc, outDir, a.Has("no-decompress"), new ConsoleProgressSink());
                Console.WriteLine($"{count} files");
                return true;
            }),

        new("garc", "pack", "<inDir> <out.garc> --version <4|6> [--padding <bytes>]", "Pack numbered files into a GARC (version 4 for X/Y and ORAS, 6 for Sun/Moon and later)",
            ["version", "padding"], a =>
            {
                var (inDir, garc) = Two(a);
                a.RejectUnknown("version", "padding");
                int version = a.Require("version") switch
                {
                    "4" => GARC.VER_4,
                    "6" => GARC.VER_6,
                    var v => throw new UsageException($"Unknown GARC version '{v}'; use 4 or 6."),
                };
                int padding = 4;
                if (a.Get("padding") is { } p && (!int.TryParse(p, out padding) || padding < 1))
                    throw new UsageException($"Invalid padding '{p}'.");
                int count = GARC.PackGARC(inDir, garc, version, padding, new ConsoleProgressSink());
                Console.WriteLine($"{count} files");
                return true;
            }),

        new("mini", "unpack", "<file> <outDir>", "Unpack a mini container to numbered files (0000.bin, ...) and print its two-letter identifier",
            NoOptions, a =>
            {
                var (file, outDir) = Two(a);
                byte[] data = File.ReadAllBytes(file);
                string ident = MiniIdentifier(data);
                byte[][] files = Mini.UnpackMini(data, ident) ?? throw new InvalidDataException("Not a valid mini container.");
                Directory.CreateDirectory(outDir);
                for (int i = 0; i < files.Length; i++)
                    File.WriteAllBytes(Path.Combine(outDir, $"{i:D4}.bin"), files[i]);
                Console.WriteLine($"{ident} {files.Length} files header={BitConverter.ToInt32(data, 4)}");
                return true;
            }),

        new("mini", "pack", "<inDir> <out> --ident <XX> [--header-size <bytes>]", "Pack the files of a folder (in name order) into a mini container; pass the header size printed by 'mini unpack' to rebuild containers with a padded header",
            ["ident", "header-size"], a =>
            {
                var (inDir, outFile) = Two(a);
                a.RejectUnknown("ident", "header-size");
                string ident = a.Require("ident");
                if (ident.Length != 2 || ident.Any(c => c > 0x7F))
                    throw new UsageException("--ident must be two ASCII characters.");
                byte[][] files = [.. Directory.GetFiles(inDir).OrderBy(f => Path.GetFileName(f), StringComparer.Ordinal).Select(File.ReadAllBytes)];
                byte[] packed = Mini.PackMini(files, ident);
                if (a.Get("header-size") is { } h)
                {
                    if (!int.TryParse(h, out int headerSize) || headerSize < 8 + (4 * files.Length) || headerSize % 4 != 0)
                        throw new UsageException($"Invalid header size '{h}'; it must be a multiple of 4 and at least {8 + (4 * files.Length)}.");
                    if (headerSize != 8 + (4 * files.Length))
                        packed = Mini.AdjustMiniHeader(packed, headerSize);
                }
                File.WriteAllBytes(outFile, packed);
                Console.WriteLine($"{files.Length} files");
                return true;
            }),

        new("game", "info", GameOptions.Synopsis, "Detect the game in an extracted RomFS/ExeFS and print its basic data",
            GameOptions.ValueOptions, a =>
            {
                a.Expect(0);
                a.RejectUnknown(GameOptions.ValueOptions);
                var config = GameOptions.Load(a);
                Console.WriteLine($"version {config.Version}");
                Console.WriteLine($"generation {config.Generation}");
                Console.WriteLine($"language {config.Language}");
                Console.WriteLine($"species {config.Info.MaxSpeciesID}");
                Console.WriteLine($"moves {config.Info.MaxMoveID}");
                Console.WriteLine($"items {config.Info.MaxItemID}");
                Console.WriteLine($"abilities {config.Info.MaxAbilityID}");
                Console.WriteLine($"text files {config.GameTextStrings.Length}");
                return true;
            }),

        new("text", "dump", $"{GameOptions.Synopsis} <outDir>", "Write each game text file to <outDir>/NNNN.txt, one line per string",
            GameOptions.ValueOptions, a =>
            {
                string[] args = a.Expect(1);
                a.RejectUnknown(GameOptions.ValueOptions);
                var config = GameOptions.Load(a);
                Directory.CreateDirectory(args[0]);
                for (int i = 0; i < config.GameTextStrings.Length; i++)
                {
                    string text = string.Concat(config.GameTextStrings[i].Select(line => line + "\n"));
                    File.WriteAllText(Path.Combine(args[0], $"{i:D4}.txt"), text, Utf8);
                }
                Console.WriteLine($"{config.GameTextStrings.Length} files");
                return true;
            }),

        new("text", "import", $"{GameOptions.Synopsis} <inDir>", "Write NNNN.txt files from <inDir> back into the game text archive",
            GameOptions.ValueOptions, a =>
            {
                string[] args = a.Expect(1);
                a.RejectUnknown(GameOptions.ValueOptions);
                var config = GameOptions.Load(a);
                byte[][] files = config.GARCGameText.Files;
                int changed = 0;
                foreach (string path in Directory.GetFiles(args[0], "*.txt"))
                {
                    if (!int.TryParse(Path.GetFileNameWithoutExtension(path), out int index) || index < 0 || index >= files.Length)
                        throw new InvalidDataException($"'{Path.GetFileName(path)}' is not a text file number between 0 and {files.Length - 1}.");
                    string text = File.ReadAllText(path, Utf8);
                    string[] lines = text.Length == 0 ? [] : text.EndsWith('\n') ? text[..^1].Split('\n') : throw new InvalidDataException($"'{Path.GetFileName(path)}' must end with a newline.");
                    // Skip files whose text is unchanged so their original bytes stay as they are.
                    if (lines.SequenceEqual(config.GameTextStrings[index]))
                        continue;
                    try
                    {
                        files[index] = TextFile.GetBytes(config, lines, config.RemapCharacters);
                    }
                    catch (Exception e)
                    {
                        throw new InvalidDataException($"{Path.GetFileName(path)}: {e.Message}", e);
                    }
                    changed++;
                }
                if (changed > 0)
                {
                    config.GARCGameText.Files = files;
                    config.GARCGameText.Save();
                }
                Console.WriteLine($"{changed} files changed");
                return true;
            }),

        new("blz", "decode", "<file>...", "Decompress bottom-LZ files in place",
            NoOptions, a => Blz(a, "-d")),

        new("blz", "encode", "<file>... [--best] [--arm9]", "Compress files with bottom-LZ in place",
            NoOptions, a => Blz(a, "-e" + (a.Has("best") ? "o" : "n") + (a.Has("arm9") ? "9" : ""))),

        new("lzss", "decompress", "<in> <out>", "Decompress an LZSS (LZ10/LZ11) file",
            NoOptions, a =>
            {
                var (inFile, outFile) = Two(a);
                long size = LZSS.Decompress(inFile, outFile);
                Console.WriteLine($"{size} bytes");
                return true;
            }),

        new("lzss", "compress", "<in> <out>", "Compress a file with LZSS (LZ11)",
            NoOptions, a =>
            {
                var (inFile, outFile) = Two(a);
                int size = LZSS.Compress(inFile, outFile);
                Console.WriteLine($"{size} bytes");
                return true;
            }),

        new("cro", "hash", "<crr> <croDir> [--no-save-cro] [--no-save-crr]", "Recompute CRO hashes and write them into the CRR",
            NoOptions, a =>
            {
                var (crr, croDir) = Two(a);
                a.RejectUnknown("no-save-cro", "no-save-crr");
                return CRO.E_HashCRR(crr, croDir, !a.Has("no-save-cro"), !a.Has("no-save-crr"), new ConsoleProgressSink());
            }),

        new("rom", "build", "--exefs <path> --romfs <path> --exheader <file> --serial <id> --out <file> [--logo <name>] [--card2] [--trimmed]",
            "Assemble a .3ds from ExeFS, RomFS and exheader (logo: Nintendo, Licensed, Distributed, iQue, iQueForSystem)",
            ["exefs", "romfs", "exheader", "serial", "out", "logo"], a =>
            {
                a.Expect(0);
                a.RejectUnknown("exefs", "romfs", "exheader", "serial", "out", "logo", "card2", "trimmed");
                string exefs = a.Require("exefs");
                string romfs = a.Require("romfs");
                string exheader = a.Require("exheader");
                string serial = a.Require("serial");
                string output = a.Require("out");
                string logo = a.Get("logo") ?? "Nintendo";
                if (!IsLogo(logo))
                    throw new UsageException($"Unknown logo '{logo}'.");
                return CTRUtil.BuildROM(a.Has("card2"), logo, exefs, romfs, exheader, serial, output, a.Has("trimmed"), new ConsoleProgressSink());
            }),
    ];

    private static (string First, string Second) Two(CommandArgs a)
    {
        string[] args = a.Expect(2);
        return (args[0], args[1]);
    }

    private static bool Blz(CommandArgs a, string mode)
    {
        string[] files = a.ExpectAtLeast(1);
        foreach (string file in files)
        {
            if (!File.Exists(file))
                throw new FileNotFoundException("File not found.", file);
        }
        _ = new BLZCoder([mode, .. files]);
        return true;
    }

    private static string MiniIdentifier(byte[] data)
    {
        if (data.Length < 4 || data[0] is < 0x20 or > 0x7E || data[1] is < 0x20 or > 0x7E)
            throw new InvalidDataException("Not a valid mini container.");
        return new string([(char)data[0], (char)data[1]]);
    }

    private static bool IsLogo(string name) =>
        name is "Nintendo" or "Licensed" or "Distributed" or "iQue" or "iQueForSystem";
}
