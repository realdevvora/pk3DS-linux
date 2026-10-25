# Spec: game data commands (personal, learnset, egg move, evolution, move)

Status: proposed. Next build after the text commands.

## Goal

Edit structured game data from the CLI with the same loop that already works for text: `dump` a table to a spreadsheet file, edit it, `import` it, then `romfs build` + `rom build` and test in Lime3DS.

Rules for every change in this spec:

- **Core does the work.** Loading, saving, the spreadsheet format and the checks all live in `pk3DS.Core`. A CLI command only reads its arguments and calls one Core method, like the existing commands. A GUI can call the same methods.
- **Match upstream.** Save sequences are moved from the upstream WinForms code unchanged, values use the units the upstream editors show, and Core keeps working for every generation it supports today. Where this spec departs from upstream, it says so.

## Scope

Five tables. Core already loads each one, and an upstream WinForms editor already edits it:

| Table | GARC (ORAS) | Entries | Core class (Gen 6 / Gen 7) | Upstream editor / save path |
|---|---|---|---|---|
| personal | `a/1/9/5` | 826 files of 0x50 + 1 master table | `PersonalInfoORAS` / `PersonalInfoSM` | `PersonalEditor6`, `Main.B_Personal_Click` |
| learnset | `a/1/9/1` | 826 | `Learnset6` / `Learnset6` | `LevelUpEditor6`, `Main.B_LevelUp_Click` |
| eggmove | `a/1/9/0` | 722 | `EggMoves6` / `EggMoves7` | `EggMoveEditor6`, `Main.B_EggMove_Click` |
| evolution | `a/1/9/2` | 826 | `EvolutionSet6` / `EvolutionSet7` | `EvolutionEditor6`, `Main.B_Evolution_Click` |
| move | `a/1/8/9` | 1 file: a `WD` mini of 622 × 36 bytes | `Move6` / `Move7` | `MoveEditor6`, `Main.B_Move_Click` |

Personal, learnset and evolution use personal-table indexes: 0–721 are species and 722–825 are alternate forms (`PersonalInfo.FormStatsIndex` points to them). Egg moves are indexed by species only.

The Core save methods support every generation that `Main.cs` supports, because they are its code. The spreadsheet columns below are the Gen 6 layout, and only ORAS has been tested, so the spreadsheet methods reject any other game with a clear message. Gen 7 and X/Y columns can be added later without changing anything else.

Out of scope: randomizers, encounters, trainers, items, shops, image code, GUI work.

## Facts checked on the Omega Ruby dump

Checked with a scratch program against the decrypted CXI, before any code change:

- The personal master table (the last file) is exactly the 826 entry files joined together. Upstream writes both: it edits the entry files, then copies them into the master table (`Main.cs` lines 779–784).
- Passing every entry through its Core writer and saving the GARC gives a byte-identical GARC for personal, learnset, evolution and move. The `WD` mini repacks identically too.
- All 826 learnsets are sorted by level.
- Move entries are 36 bytes, but `Move6` maps only the first 0x22. The last two bytes must be carried through unchanged.
- The highest evolution method in the data is 34, the same limit `EvolutionEditor6` checks.
- **Egg moves do not round-trip.** 431 of the 722 entries have no egg moves and are stored as two bytes, `00 00` (a count of zero). `EggMoves6.Write()` returns zero bytes for an empty list, so writing back every entry changes 431 files.

### Why `EggMoves6` writes empty sets as zero bytes

This is deliberate and older than ORAS support. The first egg move editor (before commit `614a09d`, 2015) wrote a zero-length file for an empty list, and the reader still accepts zero-length entries (`data.Length < 2` means empty). `EggMoves6` is shared by X/Y and ORAS, and the most likely explanation is that X/Y stores empty entries as zero-length files. That last point is an inference: there is no X/Y dump to check it against. Either way, the Gen 6 class has to handle both layouts. The ORAS data shows they cannot share one fixed way of writing empty sets.

**Fix:** do not change how `EggMoves6` writes. Instead, the Core save writes an entry only if its contents changed (see "Saving" below). An untouched empty entry keeps whatever bytes the game had, which is `00 00` in ORAS and presumably zero bytes in X/Y. An entry the user empties is written by the existing writer, exactly as upstream writes it. `text import` already uses the same rule.

## Design

### Saving (Core, all tables)

Add one save method per table to `GameConfig`, next to the `Initialize*` methods that already load them: `SavePersonal()`, `SaveLearnsets()`, `SaveEggMoves()`, `SaveEvolutions()`, `SaveMoves()`. Each one is the save half of the matching `Main.cs` handler, moved into Core, generation switch included:

- personal: entry files plus their slots in the master table, then save the GARC and re-run `InitializePersonal()`
- learnset and evolution: replace the GARC files, then save
- move: on ORAS and Gen 7, pack the entries into a `WD` mini; on X/Y, use one file per move. Then save
- egg moves are not held by `GameConfig` yet. Add an `EggMoves` property, loaded with the existing `EggMoves6.GetArray` / `EggMoves7.GetArray` like the other `Initialize*` methods do

One rule shared by all five: **an entry is written only if it changed.** "Changed" means its `Write()` output differs from the output of a fresh object read from the entry's original bytes. Comparing writer output to writer output, rather than to the raw file, is what keeps the empty egg move entries untouched. If no entry changed, the GARC is not saved. Each method returns the number of entries it wrote.

The one departure from upstream: upstream rewrites every entry the user opened in the editor, even if nothing changed. For the four lossless tables the bytes are the same either way. For egg moves this is the fix described above.

### Spreadsheet files (Core)

Core gets one class, `DataTableFile` in `pk3DS.Core/Game/`, with two methods per table:

```
DataTableFile.Dump(config, DataTable.Personal, "stats.tsv");
int changed = DataTableFile.Import(config, DataTable.Personal, "stats.tsv"); // calls config.SavePersonal()
```

The CLI and a future GUI ("Export to spreadsheet…" / "Import…") call the same two methods. `TableUtil`, which upstream uses for its "copy table" buttons, is not reused: it prints arrays as `System.Int32[]` and cannot read anything back.

Format, the same for all five tables:

- Tab-separated text (`.tsv`), UTF-8 without BOM, `\n` line endings. Numbers, Excel and any text editor open it.
- Line 1 is a header row of fixed column names in a fixed order. Every following line is one entry (one Pokémon, form or move), starting with its `index`.
- Lists inside a cell are space-separated.
- **Name columns are for reading only.** Columns named `name`, `form` or ending in `_name` are written by `Dump` (in the `--lang` language) and ignored by `Import`. You change a value by editing its number column, the same way upstream dropdowns show a name but store a number.
- **Import only changes what is in the file.** Each row is applied onto the entry's current data, so bytes no column covers are kept. A row you delete means that entry is left alone. An index may appear at most once.
- **All or nothing.** The whole file is checked before anything is written. An error names the file, line and column (`learnset.tsv:254: moves: move 700 is above the maximum 621`). A missing or unknown header column is an error.
- Value ranges come from each field's size. IDs are also checked against `config.Info` (`MaxSpeciesID`, `MaxMoveID`, `MaxItemID`, `MaxAbilityID`) and the length of the type list.

### CLI commands

Ten commands, two per table. Each takes the existing game options (`--dir` or `--romfs`/`--exefs`, `--lang`) plus the spreadsheet path, and calls `DataTableFile`:

```
pk3ds personal  dump   --dir <extracted> <out.tsv>
pk3ds personal  import --dir <extracted> <in.tsv>
pk3ds learnset  dump|import ...
pk3ds eggmove   dump|import ...
pk3ds evolution dump|import ...
pk3ds move      dump|import ...
```

`import` writes into the extracted RomFS in place, like `text import`, and prints `N entries changed`. Output, errors and exit codes follow the existing CLI: results on stdout, errors on stderr, exit code 0 / 1 / 2.

### Columns

Column names are the Core property names in snake case. Values are shown the way the upstream editor shows them.

**personal** (one row per personal index, 826 rows):
`index name form hp atk def spe spa spd ev_hp ev_atk ev_def ev_spe ev_spa ev_spd type1 type2 catch_rate evo_stage item1 item2 item3 gender hatch_cycles base_friendship exp_growth egg_group1 egg_group2 ability1 ability2 ability3 escape_rate form_stats_index forme_sprite forme_count color base_exp height weight tmhm type_tutors special_tutors1 special_tutors2 special_tutors3 special_tutors4`

- `form` is the form name from `PersonalTable.GetPersonalEntryList`; it is empty for base species.
- `form_stats_index` is read-only. Its setter is internal and the upstream editor does not edit it.
- `height` is in metres and `weight` in kilograms, in the editor's formats (`00.00` and `000.0`). The editor stores `value × 100` and `value × 10`. Showing and reading back two and one decimals is exact, so nothing is rounded. Import rejects more decimals than that.
- `color` is the value of `PersonalInfo.Color`.
- `tmhm`, `type_tutors` and `special_tutors1..4` list the indexes of the checked boxes (`0 5 12`), in the order of the editor's lists. Every stored bit is covered.

**learnset** (826 rows): `index name form moves move_names`
- `moves` is a list of `level:move` pairs (`1:33 1:45 5:39`), in the order the editor's grid shows them. All retail learnsets are sorted by level and nobody has checked whether the game needs that, so import rejects levels out of order. An empty cell means no level-up moves.

**eggmove** (722 rows): `index name moves move_names`
- `moves` is a list of move IDs. An empty cell means no egg moves.

**evolution** (826 rows): `index name form evo1 ... evo8 evo_names`
- Each `evoN` is `method:argument:species`, for the 8 slots of the editor. `0:0:0` is an empty slot. `method` is 0–34, as in the editor. `argument` is not range-checked because its meaning depends on the method (level, item, move, species, ...).

**move** (622 rows): `index name type quality category power accuracy pp priority hit_min hit_max inflict inflict_percent inflict_count turn_min turn_max crit_stage flinch effect_sequence recoil healing target stat1 stat2 stat3 stat1_stage stat2_stage stat3_stage stat1_percent stat2_percent stat3_percent flags`
- Signed exactly where the editor reads a signed value: `priority`, `recoil` and `statN_stage` are −128 to 127, and `inflict` is −1 for none (the editor reads it as a signed 16-bit value).
- `flags` lists the indexes of the checked flag boxes (`0 1 4`), the same way `tmhm` does.

## Verification

For each table, before it is committed:

1. **Unchanged round trip:** `dump`, then `import` the same file. It prints `0 entries changed` and the GARC's hash is unchanged.
2. **Full rewrite:** marking every entry changed and saving gives a byte-identical GARC for the four lossless tables.
3. **One edit:** change one cell, then `garc unpack` the GARC before and after. Only that entry's file differs (for personal, that entry and its slot in the master table).
4. **Bad input:** a value out of range, an unknown header column and a duplicate row each fail and leave the GARC untouched.
5. **In game (Lime3DS, done by the user):** rebuild and check one visible change:
   - personal: a starter's type or base stats on the summary screen
   - learnset: a starter's moves at level 5
   - move: Tackle's power on the move summary
   - evolution: a starter evolving at a lower level
   - eggmove: optional, since it needs breeding

## Order of work

One branch and pull request per step, small commits, no attribution lines:

1. Core: the five save methods and the `EggMoves` property. Update `docs/differences-from-upstream.md` for the changed-entries-only rule.
2. Core: `DataTableFile` with the shared format code and the personal table. CLI: `personal dump|import`.
3. `learnset` (Core table + CLI commands)
4. `move`
5. `evolution`
6. `eggmove`

Each step updates the README command table and the "Not yet exposed" line in `docs/ui-deferred.md`.

## For the GUI later

The GUI edits the same Core objects and calls the same save methods, with no CLI code:

```
config.Personal.Table[i].HP = 80;   // or Learnsets, Moves, Evolutions, EggMoves
config.SavePersonal();
```

Spreadsheet import and export come for free through `DataTableFile`. The choice of UI framework belongs in `docs/ui-deferred.md`. Two Core issues already listed there matter more for a GUI than for the CLI: the static state in `RomFS`, and the remaining `Console` writes in library code.
