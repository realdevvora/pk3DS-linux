</p>

<h1 align="center">pk3DS </h1>

<br />

pk3DS is a ROM editor for all 3DS Pokémon games that utilizes a variety of tools developed by a large group of contributors. pk3DS was created 
using C# and primarily focuses on its randomizer to provide users with a fresh and new experience in the beloved Pokémon games. 

## Table of contents

- [Features](#features)
- [Installation](#installation)
- [Usage](#usage)
- [Support](#support)

## Features

Our editor features a vast variety of randomizers to make every run as unique as possible. The Randomizers currently available are:

- Trainer Battles (Pokemon / Items / Moves / Abilities / Difficulty / Classes)
- Wild Encounters (Species, Level, Gen/Legend Specific, ORAS DexNav won't crash!)
- Personal Data (Pokemon Types / Stats / Abilities / TM Learnset)
- Move Randomizer (Type / Damage Category)
- Move Learnset (Level Up / Egg Move)
- Evolutions
- TM Moves
- Special Mart Inventory

## Installation

To download pk3DS, all you need to do is go into our [forum page](https://projectpokemon.org/home/forums/topic/34377-pk3ds-pok%C3%A9mon-3ds-rom-editor-and-randomizer/) and following the instructions there.

## Usage

To begin using pk3DS you must first download the pk3DS editor zip file. Once you've downloaded the zip file for the editor, dump your ROM from the 3DS Pokémon game of your choosing.

Place the files in the same folder then simply run the pk3DS.exe file.
Once you open up the executable you can begin having fun with our editor and randomizing all the attributes and characteristics of the game to your liking.
Below are some images of how the editor should look when you run it.
![RomFS Editing Tools](https://i.imgur.com/IDVCMfx.png)
![ExeFS Editing Tools](https://i.imgur.com/Ied0sVV.png)
![CRO Editing Tools](https://i.imgur.com/lUSGbw5.png)

## Command line (this fork)

This fork separates pk3DS's file tooling from the Windows UI so it can run on macOS and Linux:

- **`pk3DS.Core`** is a plain `net10.0` class library with no Windows dependency. Long-running operations report through an `IProgressSink` instead of WinForms `ProgressBar` / `RichTextBox` controls, and `null` is accepted. The image code that needs `System.Drawing` is excluded from the build for now.
- **`pk3DS.Cli`** builds the `pk3ds` command, with one command per operation:

| Command | What it does |
|---|---|
| `ncsd extract <rom.3ds> <outDir>` | Extract exheader, ExeFS and RomFS from a decrypted `.3ds` |
| `ncch extract <game.cxi> <outDir>` | Same, from a decrypted CXI |
| `romfs extract <romfs.bin> <outDir>` / `romfs build <inDir> <out.bin>` | Unpack or build a RomFS image |
| `exefs unpack <exefs.bin> <outDir>` / `exefs pack <out.bin> <file>...` | Unpack or pack an ExeFS image |
| `blz decode\|encode <file>...` | Bottom-LZ (de)compression in place |
| `lzss compress\|decompress <in> <out>` | LZSS (de)compression |
| `cro hash <crr> <croDir>` | Recompute CRO hashes and update the CRR |
| `rom build --exefs --romfs --exheader --serial --out` | Assemble a `.3ds` (fixed-key encrypted, for custom firmware or emulators) |

Run `pk3ds <group> <command> --help` for a command's arguments. Results go to stdout and progress to stderr; the exit code is 0 on success, 1 on failure and 2 for a usage error. Inputs must be decrypted dumps.

```
dotnet run --project pk3DS.Cli -- ncch extract game.cxi out
dotnet run --project pk3DS.Cli -- romfs build out/romfs romfs.bin
```

Fixes made while testing against a real Omega Ruby dump: rebuilt RomFS and ExeFS images now match the original byte for byte, because files are added in a fixed sorted order (filesystem order differs between operating systems); `rom build` no longer deletes the RomFS file you pass in; `ncsd extract` no longer writes a temporary file ten times too large; re-extracting no longer doubles files; and the ExeFS header hash is computed over the correct 0x200 bytes.

`pk3DS.WinForms` is still in the repository, untouched, but it is not part of the solution and no longer builds against Core. What was left out on purpose, and the remaining known issues, are listed in [docs/ui-deferred.md](docs/ui-deferred.md).

## Building

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```
dotnet build pk3DS.slnx
```

## Support

If any bugs or errors are caught or experienced come to our [forum page](https://projectpokemon.org/home/forums/topic/34377-pk3ds-pok%C3%A9mon-3ds-rom-editor-and-randomizer/) and communicate with us on what the issue is.
Many community members as well as contributors are active and can be found there. 
