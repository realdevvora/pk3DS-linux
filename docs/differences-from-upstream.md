# Differences from upstream pk3DS

Base: [kwsch/pk3DS](https://github.com/kwsch/pk3DS) at `6daaca9` ("Update to .NET 10"). Upstream has no newer commits, so there is nothing to merge. Only `pk3DS.Core` was changed; the new `pk3DS.Cli` project is additive, and `pk3DS.WinForms` is untouched.

How the changes were checked: the rebuilt RomFS and ExeFS, all 298 GARCs and all 613 mini containers of a decrypted Omega Ruby dump reproduce the original bytes, and the rebuilt game boots in Lime3DS and on hardware. Upstream itself cannot run on macOS (it needs Windows Forms), so there is no side-by-side run; the original game files serve as the reference.

## Required to remove the UI dependency

| Change | Effect |
|---|---|
| `ProgressBar` / `RichTextBox` parameters replaced by an optional `IProgressSink` in `NCCH`, `NCSD`, `RomFS`, `CTRUtil`, `CRO`, `BLZCoder`, `GARC` | Source-incompatible for callers; behavior is the same, and `null` is now safe (before, several methods threw `NullReferenceException` without controls) |
| `GARC.FileCountDetermined` / `PackProgressed` / `UnpackProgressed` events removed | Replaced by the same sink |
| `RomFS` constructor takes an optional `isTempFile` | See "output-affecting fixes" |
| Target `net10.0` instead of `net10.0-windows`; image code (`CTR/Images`, `ETC1`, `SMDH`, `ImageUtil`, `TypeChart`) excluded from the build | Those files are unchanged on disk; they depend on `System.Drawing` and a Windows-only DLL |
| `Console.WriteLine` removed from `GARCFile.Save` | Library no longer prints on save |

## Fixes that change output or behavior

| Change | Before | After |
|---|---|---|
| File and directory enumeration sorted case-insensitively (`SortedDirectory`) in `RomFS`, `ExeFS`, `CTRUtil`, `Mini`, `DARC`, `CRO` | Filesystem order: alphabetical on NTFS, arbitrary on macOS/Linux | Same order NTFS gives, so a built RomFS and ExeFS match the original game's bytes on every OS |
| `ExeFS` superblock hash covers `0x200` bytes | Hashed the first 200 (decimal) bytes, so the hash field of a rebuilt NCCH header differed from retail | Matches the original header |
| `BuildROM` deletes the RomFS / ExeFS only if it generated them from a folder | Always deleted the RomFS file it was given, and left a generated `exefs.bin` behind | Caller's files are never deleted; generated ones are cleaned up |
| `NCSD` extraction copies exactly the first partition | Wrote a temporary CXI ten times too large | Correct size |
| Extraction opens files with `FileMode.Create` instead of `Append` / `OpenWrite` | Re-extracting into a used folder doubled or left stale bytes in files | Overwrites |
| `GARC.PackGARC` resets the entry vector per folder entry | A folder entry inherited the previous folder's bits and claimed files it did not contain | Correct vector |
| `GARC.PackGARC` compresses `dec_*` inputs to temporary files | Deleted the user's `dec_*` file and left a compressed copy in the input folder | Input folder untouched |
| `GARC.PackGARC` writes folder sub-files in file-number order | Filesystem order | Numeric order |
| `RomFS.BuildRomFS` uses the full path of its input | A relative input folder made the root entry get a wrong name | Works with relative paths |
| `LZSS` compress / decompress create the output directory from the full path | Threw for an output name without a directory | Works |
| `Stream.Read` replaced by `ReadExactly` at seven sites (`CTR`, `LZSS` x2, `RomFS` x3, `SARC`) | A short read silently produced wrong data | Throws `EndOfStreamException` |

## Added for the game data commands

| Change | Upstream | Here |
|---|---|---|
| `GameConfig.SavePersonal` / `SaveLearnsets` / `SaveMoves` / `SaveEvolutions` / `SaveEggMoves` | The same save steps lived in the WinForms `Main` button handlers, run after each editor closed | Moved into Core with their Gen 6 / Gen 7 branches, so any front end can save |
| Save writes only entries whose contents changed | Every entry the user had opened was rewritten | Untouched entries keep their original bytes. This matters for egg moves: Omega Ruby stores an empty list as `00 00`, but `EggMoves6.Write()` returns zero bytes (as pk3DS always has, likely the X/Y layout), so rewriting unchanged entries changed 431 files |
| `SaveEvolutions` reloads the table after saving | `Main` reloaded it before saving, so `Evolutions` held the old data until restart | Reloads after |
| `GameConfig.SaveGameText` / `SaveStoryText`, `InitializeStoryText` | Text was saved by `Main` (game text) or after the story text editor closed; every file was rewritten | In Core; only files whose text changed are written, as `text import` already did. Story text is loaded on demand, as upstream's editor did |
| `Item.Revive` getter | Reported "revive" when bit 0 was clear, the opposite of its setter and of the data (Revive and Max Revive have the bit set) | Reports the bit as stored |
| `Item.NaturalGiftType` setter | Rebuilt the packed field from `NaturalGiftEffect`, wiping the flags and pockets | Changes only the type bits |
| `Item.PocketBattle` setter | Masked with `0x077F`, clearing the lowest bit of the field pocket (Potion moved from pocket 1 to 0) | Masks with `0x07FF` |
| `GameConfig.Items`, `InitializeItems`, `SaveItems` | Items were read and saved by `Main` around the item editor | In Core, like the other tables |
| `GameConfig.MegaEvolutions`, `InitializeMegaEvolutions`, `SaveMegaEvolutions` | Read and saved by `Main` around the Mega Evolution editor | In Core, like the other tables |
| `GameConfig.Trainers`, `InitializeTrainers`, `SaveTrainers` (Gen 6) | Read and saved by `Main` around `RSTE`; every trainer opened in the editor was rewritten | In Core; only changed trainers are written. Entry 0, a placeholder RSTE never opened, is not loaded. Gen 7 trainers (`TrainerData7`) are not covered yet |
| `GameConfig.MaisonTrainers` / `MaisonPokemon` / `SuperMaisonTrainers` / `SuperMaisonPokemon`, `InitializeMaison`, `SaveMaisonTrainers` / `SaveMaisonPokemon` (Gen 6) | Read and saved by `Main` around `MaisonEditor6` | In Core; only changed entries are written |
| `OPower6` | The O-Power editor's offset and field layout lived in the form | In Core |
| `Pickup6` | The pickup editor's offset and list lengths lived in the form | In Core |
| `Mart6` | The mart editor's offsets, slot counts and location names lived in the form | In Core |
| `CodeBin`, `TMHM6` | The TM/HM editor found and edited its table in `code.bin` inside the form | In Core |
| TM/HM item descriptions | `TMHMEditor6` copied every TM/HM move's description into its item's description on close, rewriting the 44 retail descriptions worded differently from the move's (line breaks, commas). It only updated the text in memory | Only TMs/HMs whose move changed get the new description, and it is saved to the game text |
| `GameConfig.EggMoves` and `InitializeEggMoves`, loaded by `InitializeAll` | Egg moves were only read inside the egg move editor | Loaded like the other tables |

## Behavior kept as upstream

- Text encoding trims each line and pads to four bytes; see `docs/ui-deferred.md`.
- LZSS and BLZ compressors are unchanged (BLZ encoding is slow: about 75 s for a 3 MB `.code.bin`).
- The remaining items under "Backend issues still open" in `docs/ui-deferred.md`.
