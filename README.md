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
- **`pk3DS.Cli`** builds the `pk3ds` command. It covers the game container, archive, compression and hashing operations, plus game text and the species, level-up move, egg move, move, evolution, item, Mega Evolution, trainer, Battle Maison, TM/HM, Poké Mart, Pickup, O-Power, static encounter, gift, starter, type chart and wild encounter tables; the rest of the data-editing code (encounters, trainers, randomizers and so on) is not exposed yet:

| Command | What it does |
|---|---|
| `ncsd extract <rom.3ds> <outDir>` | Extract exheader, ExeFS and RomFS from a decrypted `.3ds` |
| `ncch extract <game.cxi> <outDir>` | Same, from a decrypted CXI |
| `romfs extract <romfs.bin> <outDir>` / `romfs build <inDir> <out.bin>` | Unpack or build a RomFS image |
| `exefs unpack <exefs.bin> <outDir>` / `exefs pack <inDir> <out.bin>` | Unpack an ExeFS image, or pack the files of a folder (up to 10, in name order) into one |
| `garc unpack <file.garc> <outDir>` / `garc pack <inDir> <out.garc> --version 4\|6` | Unpack or pack a GARC archive (version 4 for X/Y and ORAS, 6 for Sun/Moon and later) |
| `mini unpack <file> <outDir>` / `mini pack <inDir> <out> --ident XX [--header-size N]` | Unpack or pack a mini container (numbered `0000.bin`, ... files). `unpack` prints the identifier and header size; pass the size back to `pack` to rebuild containers with a padded header exactly |
| `game info (--dir <dir> \| --romfs <dir> --exefs <dir>)` | Detect the game in an extracted RomFS/ExeFS and print its version and limits |
| `text dump <outDir>` / `text import <inDir>` (with the same game options, plus `--story`) | Write each game text file to `NNNN.txt`, one string per line, and write edited files back into the game text archive. With `--story`, the same for the story text (NPC and event dialogue) |
| `personal dump <out.tsv>` / `personal import <in.tsv>` (with the same game options) | Write species stats, types, abilities, items and TM/tutor flags to a spreadsheet file (one row per species or form), and write edited rows back. Omega Ruby / Alpha Sapphire only for now |
| `learnset dump <out.tsv>` / `learnset import <in.tsv>` | The same for level-up moves, written as `level:move` pairs |
| `move dump <out.tsv>` / `move import <in.tsv>` | The same for move data: type, category, power, accuracy, PP, priority, effects and flags |
| `evolution dump <out.tsv>` / `evolution import <in.tsv>` | The same for evolutions: 8 `method:argument:species` slots per species or form |
| `eggmove dump <out.tsv>` / `eggmove import <in.tsv>` | The same for egg moves, one row per species |
| `item dump <out.tsv>` / `item import <in.tsv>` | The same for item data: price, effects, pockets, boosts |
| `megaevo dump <out.tsv>` / `megaevo import <in.tsv>` | The same for Mega Evolutions: 3 `form:method:argument` slots per species or form (method 1 = holding the item in `argument`) |
| `trainer dump <out.tsv>` / `trainer import <in.tsv>` | Trainer data, one row per trainer: class, whether team members carry items and custom moves, battle type, items, AI, money, prize |
| `trainerpoke dump <out.tsv>` / `trainerpoke import <in.tsv>` | Trainer teams, one row per team slot (`trainer`, `slot`, species, form, level, IVs, ability, gender, item, moves). A trainer that appears in the file gets exactly those rows as its team, so slots can be added or removed |
| `maisontrainer dump/import` and `maisonpoke dump/import` (`--super` for the Super Battle Maison) | Battle Maison trainers (class and the pool Pokémon they pick from) and the Pokémon pool (species, moves, EV spreads, nature, item) |
| `tmhm dump <out.tsv>` / `tmhm import <in.tsv>` | The move each TM and HM teaches. Changing a TM's move also copies the move's description into the TM item's description |
| `mart dump <out.tsv>` / `mart import <in.tsv>` | Poké Mart inventories, one row per mart; each mart keeps its number of item slots |
| `pickup dump <out.tsv>` / `pickup import <in.tsv>` | The items the Pickup ability finds: a common list (18) and a rare list (11) |
| `opower dump <out.tsv>` / `opower import <in.tsv>` | O-Powers 1-64: energy costs, usability, stage, level-up, efficacy, duration (the fields the pk3DS editor edits) |
| `static dump/import` and `gift dump/import` | Static encounters (legendaries and other fixed Pokémon) and gift Pokémon (including the starters, gifts 0-2), stored in `DllField.cro`. After editing a CRO, run `cro hash` if your console checks CRO hashes |
| `starter dump/import` | The starter choices, one row per set (ORAS: Gen 3, 2, 4, 5). Writes the selection screen (`DllPoke3Select.cro`) and the gifts that give the starters (`DllField.cro`) |
| `typechart dump/import` | The type effectiveness chart (`DllBattle.cro`): row N is the attacking type, column `vsM` its effectiveness against type M: 0 no effect, 2 not very effective, 4 regular, 8 super effective |
| `wild dump/import` | Omega Ruby / Alpha Sapphire wild encounters, one row per slot: grass, tall grass, swarm, surf, rock smash, the three rods and the three horde groups, with species, form and level range. Writes the map and the DexNav copy of its table |
| `randomize personal\|learnset\|eggmove\|evolution\|evolve-every-level\|trade-evolutions\|move\|trainer\|wild [--seed <n>] [options]` | The randomize buttons of the pk3DS editors, with the same settings as options (`--stats yes`, `--stat-deviation 25`, ...; `pk3ds randomize <name> --help` lists them with their defaults, which are the editors'). The same `--seed` gives the same result |
| `shiny info` / `shiny set [--rerolls <count> \| --original] [--always yes\|no]` | Show or patch the shiny rate: a fixed number of PID rerolls per Pokémon (a count the patch cannot encode is raised to the next it can, as in pk3DS), the game's own count, or every Pokémon shiny |
| `blz decode\|encode <file>...` | Bottom-LZ (de)compression in place |
| `lzss compress\|decompress <in> <out>` | LZSS (de)compression |
| `cro hash <crr> <croDir>` | Recompute CRO hashes and update the CRR |
| `rom build --exefs --romfs --exheader --serial --out` | Assemble a `.3ds` (fixed-key encrypted, for custom firmware or emulators) |

Game-data commands take `--dir <folder>` (a folder holding the extracted `romfs` and `exefs`), or `--romfs` and `--exefs` separately, and `--lang <0-9>` (2 is English, the default). Text lines may contain bracketed control codes such as `[VAR ...]`; `text import` rejects malformed ones with the file name, skips files whose text you did not change, and trims leading and trailing spaces from each line, as pk3DS always has.

Tables and patches in the game executable (`tmhm`, `mart`, `pickup`, `opower`, `shiny`) need a decompressed `exefs/.code.bin`, as in pk3DS: run `pk3ds blz decode` on it once, and `pk3ds blz encode` before `rom build` (about 75 seconds).

Spreadsheet files (`.tsv`) are tab-separated with a header row and open in Numbers or Excel; the columns are listed in [docs/data-commands-spec.md](docs/data-commands-spec.md). Name columns are only for reading: edit the number columns. `import` checks every row before writing anything, reports errors by line and column, accepts files with only the rows you changed, and writes only entries whose data changed.

Run `pk3ds <group> <command> --help` for a command's arguments. Results go to stdout and progress to stderr; the exit code is 0 on success, 1 on failure and 2 for a usage error. Inputs must be decrypted dumps.

```
dotnet run --project pk3DS.Cli -- ncch extract game.cxi out
dotnet run --project pk3DS.Cli -- romfs build out/romfs romfs.bin
```

Fixes made while testing against a real Omega Ruby dump: rebuilt RomFS and ExeFS images now match the original byte for byte, because files are added in a fixed sorted order (filesystem order differs between operating systems); `rom build` no longer deletes the RomFS file you pass in; `ncsd extract` no longer writes a temporary file ten times too large; re-extracting no longer doubles files; the ExeFS header hash is computed over the correct 0x200 bytes; and `garc pack` no longer deletes your `dec_*` input files or writes vectors that claim files a folder entry does not have. Every GARC in the Omega Ruby RomFS (298) unpacks and repacks byte for byte.

`pk3DS.WinForms` is still in the repository, untouched, but it is not part of the solution and no longer builds against Core. What was left out on purpose, and the remaining known issues, are listed in [docs/ui-deferred.md](docs/ui-deferred.md).

## macOS app (this fork)

`pk3DS.Mac` is a macOS app (Avalonia) on the same Core API as the CLI, laid out like the pk3DS window: RomFS, ExeFS, CRO and Output tabs, with File and Tools menus to open, extract and build a game, compress or decompress `code.bin`, update CRO hashes and restore the original files. Every table opens in a grid editor with the CLI's checks and spreadsheet export/import; game and story text have their own editor; randomize buttons sit in the editors they change. Opening a game backs up its original files to a `backup` folder next to the game folder, as pk3DS did.

```
pk3DS.Mac/build-app.sh                       # builds pk3DS.Mac/bin/app/pk3DS.app (self-contained, ad-hoc signed)
open pk3DS.Mac/bin/app/pk3DS.app
open pk3DS.Mac/bin/app/pk3DS.app --args <game folder>   # opens a game at launch
```

See [docs/ui-spec.md](docs/ui-spec.md) for the design.

## Building

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```
dotnet build pk3DS.slnx
```

## Support

If any bugs or errors are caught or experienced come to our [forum page](https://projectpokemon.org/home/forums/topic/34377-pk3ds-pok%C3%A9mon-3ds-rom-editor-and-randomizer/) and communicate with us on what the issue is.
Many community members as well as contributors are active and can be found there. 
