# Spec: macOS GUI

Status: implemented, 2026-10-26 (steps 1-6 of the order of work; step 7 is optional polish). Follows [roadmap.md](roadmap.md), which is complete: every Gen 6 editor except move tutors and the image editors is in `pk3DS.Core` and exposed by the CLI.

## Goal

A macOS app that does what the pk3DS window did, on top of the same Core API the CLI uses. The app adds views, not logic: every load, edit check and save goes through Core, so the GUI and the CLI always write the same bytes.

Rules, as for the CLI:

- **No logic in the GUI.** If a view needs something Core does not offer, it is added to Core first (and the CLI can use it too).
- **Match upstream.** Same window layout, same editors, same defaults; differences are listed in `docs/differences-from-upstream.md`.
- **Omega Ruby / Alpha Sapphire**, macOS only, tested in Lime3DS.

## Framework

**Recommended: Avalonia** (C#, .NET 10, MVVM). The app references `pk3DS.Core` directly and runs it in-process, so there is no second data model, no process launching and no serialization layer. It runs natively on Apple Silicon, has a data grid control for the table editors, and loads PNG sprites without `System.Drawing`.

Alternatives considered:

| | Avalonia | .NET MAUI (Mac Catalyst) | SwiftUI calling the CLI |
|---|---|---|---|
| Uses Core directly | Yes | Yes | No: shells out, parses TSV |
| Desktop controls (grids, menus, file dialogs) | Good | Limited on Mac | Native |
| Logic duplicated | None | None | Validation and models re-implemented in Swift |
| Fit with "decouple the API, port the UI" | Direct | Direct, heavier | Weak |

## Project

- New project `pk3DS.Mac/` (Avalonia application, `net10.0`, `osx-arm64`), added to `pk3DS.slnx`. It references `pk3DS.Core` only.
- MVVM with view models per window, using `CommunityToolkit.Mvvm` for observable properties and commands (small and source-generated). Exact package versions are pinned when the project is created.
- Sprites: copy `pk3DS.WinForms/Resources/img` (Pokémon and item sprites, icons; 1,684 PNGs) into the app's assets. They are plain PNGs, so no image code from Core is needed.
- Build: `dotnet publish pk3DS.Mac -r osx-arm64` produces a `.app` bundle. Unsigned, for local use; opened with right-click > Open the first time.

## Main window

Mirrors the upstream main window.

**Menu**

| Menu | Item | Core call |
|---|---|---|
| File | Open game folder… (a folder with `romfs/` and `exefs/`) | `GameConfig` load (moved from the CLI's `GameOptions`, see Core changes) |
| File | Extract .cxi / .3ds… | `NCCH.ExtractNCCHFromFile`, `NCSD.ExtractFilesFromNCSD` |
| File | Build .3ds… | compress `code.bin` if needed (`BLZCoder`), `RomFS.BuildRomFS`, `CTRUtil.BuildROM` |
| Tools | Decompress / compress code.bin | `BLZCoder` |
| Tools | Update CRO hashes | `CRO.E_HashCRR` (CRR not saved, as upstream's menu item) |
| Tools | Restore original files | `GameBackup.RestoreFiles` |
| Options | Language | `GameConfig.Language` + reload text |

On open, the game is backed up once like upstream (`GameBackup.BackupFiles`), and the title shows the game and language.

**Tabs** (as upstream), each a grid of buttons that open an editor window:

| Tab | Buttons → table / Core API |
|---|---|
| RomFS | Game Text, Story Text (`SaveGameText` / `SaveStoryText`); Personal Stats; Evolutions; Level Up Moves; Egg Moves; Move Stats; Item Stats; Mega Evolutions; Trainers (trainer + team); Battle Maison (normal / Super); Wild Encounters |
| ExeFS | TMs/HMs; Poké Mart; Pickup Items; O-Powers; Shiny Rate |
| CRO | Static Encounters; Gift Pokémon; Starter Pokémon; Type Chart |
| Output | Log of operations (the `IProgressSink` messages) and a progress bar |

Buttons for things not in Core (Title Screen, OWSE, Move Tutors, Gen 7 editors) are not shown. `code.bin` tables are disabled with a "Decompress code.bin" hint while it is compressed, matching the CLI's message.

## Editors

### Table editor (one view for all tables)

Every table the CLI dumps (`DataTable`: personal, learnset, move, evolution, egg move, item, megaevo, trainer, trainerpoke, maison ×4, tmhm, mart, pickup, opower, static, gift, starter, typechart, wild) opens in the same window:

- A data grid with the table's columns in CLI order. Name columns are read-only and shown greyed out; editable cells are validated on commit with the same Core check as `import` (the same message, for example "300 is not between 0 and 255").
- A filter box (matches any cell) and the entry count.
- **Save** writes through the table's Core save (only changed entries are written); **Revert** reloads.
- **Export / Import spreadsheet…** call `DataTableFile.Dump` / `Import`, so a TSV edited in Numbers can be brought back in.

This needs the table definition that `DataTableFile` already builds (columns, getters, validating setters, save, reload) to be public; see Core changes. One editor then covers all 22 tables, and new tables appear in the GUI with no GUI work.

### Text editor

Game text and story text: a list of text files on the left, the lines of the selected file on the right (editable, one row per line), and a search box over all files. Save goes through `SaveGameText` / `SaveStoryText`; malformed control codes are reported with the file and line, as in `text import`.

### Randomizer dialogs

Each randomize button (personal, learnset, egg moves, evolutions, evolve every level, trade evolutions, moves, trainers, wild) opens a dialog generated from its options record (`PersonalRandomizerOptions` and so on): a checkbox per yes/no option, a number box per number, a dropdown per choice, with the record's defaults (the upstream forms' starting values), plus an optional seed. Run calls the `TableRandomizers` method and shows how many entries changed. The CLI builds its flags from the same records, so both stay in step automatically.

### Shiny rate

A small dialog over `ShinyRate6`: current state, reroll count (with the chance it gives, `ShinyRate6.Chance`), "original", and "everything shiny".

### Later, optional

Form-style editors closer to the upstream windows (a species page with sprite and stats, a trainer page with the six team slots) can sit on the same Core objects. They are polish; the table editor covers the editing.

## Long operations

Extraction, building and BLZ compression run on a background task. A GUI `IProgressSink` adapter posts to the UI thread (`Dispatcher.UIThread.Post`): `Log` appends to the Output tab, `Reset` / `Step` / `Set` drive the progress bar. Editors and menus are disabled while an operation runs, as upstream disabled its window.

## Core changes needed first

Small, each its own commit, each also used by the CLI:

1. **Public table API.** Expose the per-table definition `DataTableFile` builds (`Table` and `Column`: name, read-only flag, `Get(entry)`, validating `Set(entry, text)`, count, which entries exist, `Save`, `Reload`), for example `DataTableFile.Open(config, table)`. `Dump` / `Import` keep working on top of it. The two-key team table (`trainerpoke`) gets the same shape with rows keyed by trainer and slot.
2. **Opening a game in Core.** Move the CLI's `GameOptions.Load` logic (find `romfs` / `exefs`, detect the game from the file count, check the language) into Core, e.g. `GameConfig.Open(romfs, exefs, lang)`. The CLI calls it.
3. **No working-directory files.** A Mac app's working directory is `/` (read-only), and two things write there today:
   - `GameBackup` writes to a relative `backup` folder: give it a backup root. The app passes the folder that contains the game folder, so the backup lands beside the game as `<parent>/backup/<game folder>/` (upstream's layout under a known parent instead of the working directory); the CLI keeps its current behavior.
   - `RomFS` builds keep `ROOT_DIR` / `OutFile` as statics and write `tempRomFS.bin` / `romfs.bin` to the working directory (already listed as open in `ui-deferred.md`): move them next to the output file and make them instance state.
4. **Library `Console` writes** (`BLZCoder`, `GameBackup`, `FileFormat`, a few others) go through the `IProgressSink`, so they show in the Output tab instead of being lost.

## Order of work

One branch and pull request per step:

1. Core changes 1–4 (separately), with the CLI switched to them and the existing regression checks passing.
2. App shell: project, main window, open / extract, language, Output tab, progress, backup on open.
3. Table editor for all tables, with export / import.
4. Text editor.
5. Build .3ds, `code.bin` compress / decompress, CRO hashes, restore.
6. Randomizer dialogs and the shiny rate dialog.
7. Optional: sprites in the grids, form-style species and trainer pages.

## Verification

- **Same bytes as the CLI.** For each editor, make an edit in the GUI and the same edit with the CLI on a copy of the game; the changed files must be identical. No-edit save must change nothing.
- **App runs**: launched on macOS, each tab and editor opened and saved once (smoke run).
- **In game**: one combined test ROM per batch in Lime3DS, as before.

## Out of scope

- Image editing (title screen, icon/SMDH, BCLIM): waits for the image layer in `ui-deferred.md` section 3.
- X/Y, Sun/Moon and the Gen 7 editors.
- Rewiring the old WinForms project (`ui-deferred.md` section 1).

## Decisions (confirmed by the user, 2026-10-25)

1. **Framework: Avalonia**, in-process on `pk3DS.Core`.
2. **Generic table editor first** for every table; form-style pages later as polish.
3. **Backups next to the game folder**: `<parent>/backup/<game folder>/`.
4. **Distribution: unsigned local `.app`** from `dotnet publish`.
