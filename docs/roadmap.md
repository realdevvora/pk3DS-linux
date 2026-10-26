# Roadmap: remaining editors

Status: proposed, 2026-10-25. Follows [data-commands-spec.md](data-commands-spec.md), which is done and verified in game.

This covers every Gen 6 editor in `pk3DS.WinForms` that the CLI does not expose yet, grouped by **where the data lives**, because that decides how much work each one is. Omega Ruby / Alpha Sapphire only, as before. X/Y and Sun/Moon are out of scope.

The same rules apply as before: the logic moves into Core (and is moved, not rewritten), the CLI only adds commands, behavior matches upstream, and any difference is listed in `docs/differences-from-upstream.md`.

## Where the remaining data lives

| Source | Editors | What is in Core today | Effort |
|---|---|---|---|
| **GARC** (same as the finished tables) | items, Mega Evolutions, trainers, Battle Maison, story text | Structures for all of them (`Item`, `MegaEvolutions`, `TrainerData6`, `Maison6`). The load/save glue sits in `Main.cs` | Low: reuse `GameConfig` save methods + `DataTableFile` |
| **ExeFS `code.bin`** (game executable) | TM/HM list, mart inventories, move tutors, Pickup, O-Powers, shiny rate | Nothing. Each editor finds its table in `code.bin` by searching for a fixed byte pattern, then edits bytes in place | Medium: move the pattern search and table layout into Core |
| **CRO modules** in the RomFS (`DllField.cro`, `DllPoke3Select.cro`, `DllBattle.cro`) | starters, gift Pokémon, static encounters, type chart | `EncounterGift6`, `EncounterStatic6`. Offsets and file handling are in the forms | Medium; edited CROs also need their hashes updated (see decisions) |
| **`encdata` GARC, custom layout** | ORAS wild encounters (`RSWE`, 834 lines) | Nothing: the format (zone data, encounter slots, DexNav) is parsed inside the form | High: largest extraction |
| **Randomizers** | personal, learnset, egg move, evolution, move, species (already in `pk3DS.Core/Randomizers`); trainer and wild randomizers (inside `RSTE`, `TrainerRand`, `RSWE`) | 10 randomizer classes, driven by settings that the forms fill in from checkboxes | Low for the first group, high for trainer and wild (they depend on the trainer and wild work above) |

Not planned: title screen (image editing, waits for the image layer in `docs/ui-deferred.md`) and the Gen 7-only editors.

## Tasks

Each task is one branch and one pull request, verified like the data tables (unchanged round trip is byte-identical, one edit changes only its entry, bad input is rejected, one in-game check in Lime3DS), with README and docs updated.

### Phase 1: GARC tables (pattern already proven)

1. **Story text.** *(done)* `storytext` is the same text format as game text, in a different GARC (upstream opens both in the same `TextEditor`). Add `--story` to `text dump` / `text import`. Small. In game: an NPC line.
2. **Items.** *(done)* `item` GARC, `Item` struct (`ItemEditor6`). New `DataTable.Item`; `GameConfig.Items` + `SaveItems`. In game: an item's buy price.
3. **Mega Evolutions.** *(done)* `megaevo` GARC, `MegaEvolutions` (`MegaEvoEditor6`). New `DataTable.MegaEvo`. In game: hard to see without a Mega Stone; optional.
4. **Trainers.** *(done)* `trclass` / `trdata` / `trpoke` GARCs, `TrainerData6` (the save path in `Main.B_Trainer_Click`, the editing in `RSTE`). The team format depends on per-trainer flags (held items, custom moves), so the spreadsheet layout is a decision (see below). In game: a route trainer's team.
5. **Battle Maison.** *(done)* `maisontr*` / `maisonpk*` GARCs, `Maison6`. Same layout question as trainers. Low priority.

### Phase 2: `code.bin` tables

6. **Core `CodeBin` helper.** *(done)* One class that loads `exefs/code.bin`, finds a table by the byte patterns the editors use today, and writes it back. Moving the search code is shared by every task in this phase. Depends on the compression decision below.
7. **TM/HM list** *(done)* (`TMHMEditor6`). Move tutors (`TutorEditor6`) are **skipped**: the upstream editor itself warns "Changes made do not reflect ingame. Still needs more research." In game: the move a TM teaches.
8. **Marts** *(done)* (`MartEditor6`). Item lists per shop. In game: a Poké Mart's stock.
9. **Pickup, O-Powers, shiny rate** *(done)* (`PickupEditor6`, `OPower`, `ShinyRate`). Small tables; the shiny rate is a code patch, not a table. Low priority.

### Phase 3: CRO tables

10. **Static and gift encounters** *(done)* (`StaticEncounterEditor6`, `GiftEditor6`), both in `DllField.cro`, using the existing `EncounterStatic6` / `EncounterGift6`. In game: a legendary's level or a gift Pokémon.
11. **Starters** *(done)* (`StarterEditor6`): `DllPoke3Select.cro` plus `DllField.cro`. In game: the starter choice screen.
12. **Type chart data** *(done)* (`TypeChart6`, `DllBattle.cro`). Only the 18 × 18 effectiveness table, not the chart image. Low priority.

### Phase 4: wild encounters

13. **Move the ORAS wild encounter format into Core.** Parse and write `encdata` (zones, slots, DexNav data) as `RSWE` does, as its own structure class. No CLI yet. Verified by a byte-identical round trip of the whole GARC.
14. **`wild dump` / `wild import`.** One row per zone and encounter type. In game: a route's grass encounters.

### Phase 5: randomizers

15. **`randomize` commands for the existing Core randomizers** (personal, learnset, egg move, evolution, move). They run on the loaded tables and save through the same `GameConfig.Save*` methods. Options mirror the upstream checkboxes.
16. **Trainer and wild randomizers.** Move their logic out of `RSTE` / `TrainerRand` / `RSWE` into Core. Depends on tasks 4 and 13.

## Order

Phase 1 first (quick and proven), then 2, 3, 4, 5. Phases 2 and 3 do not depend on each other and can swap. Phase 5's first task can start any time after phase 1; its second needs phases 1 and 4.

## Decisions (confirmed by the user, 2026-10-25)

1. **Trainer and Maison spreadsheets: two files.** `trainer.tsv` has one row per trainer (class, battle type, AI, items, team size). `trainerpoke.tsv` has one row per team slot (`trainer`, `slot`, species, form, level, item, moves). Each cell holds one value, like the finished tables. The Maison uses the same split.
2. **Compressed `code.bin`: match upstream.** `code.bin` commands refuse a compressed file and say to run `pk3ds blz decode`. The user runs `pk3ds blz encode` before `rom build`.
3. **CRO hashes: match upstream.** CRO edit commands only edit. `pk3ds cro hash` stays a separate step, and the docs say when it is needed.
4. **Randomizers get `--seed`.** The same seed gives the same result. Without `--seed` a run is random, as upstream. This departure goes into `differences-from-upstream.md`.
