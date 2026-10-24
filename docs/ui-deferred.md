# UI-related work deferred from the Core/CLI split

The first change set decouples `pk3DS.Core` from the UI and adds the `pk3DS.Cli` endpoints. The items below touch the UI or image side and were intentionally left out of it. Nothing here is wired in.

## State after the first change set

- `pk3DS.Core` targets `net10.0` and has no WinForms dependency. Long-running operations report through `IProgressSink` instead of `ProgressBar` / `RichTextBox`.
- `pk3DS.WinForms/` is untouched on disk but **no longer builds**: it still passes `ProgressBar` / `RichTextBox` to Core (e.g. `Main.cs` `new BLZCoder([...], pBar1)`) and is not in `pk3DS.slnx`.
- Image code is excluded from the Core build via `<Compile Remove>` in `pk3DS.Core.csproj`: `CTR/Images/**`, `CTR/ETC1.cs`, `CTR/SMDH.cs`, `ImageUtil.cs`, `Structures/TypeChart.cs`. These still use `System.Drawing` (Windows-only on .NET 6+) and the native `ETC1Lib.dll`.

## 1. Rewire the WinForms project to Core

Add a small adapter in `pk3DS.WinForms` implementing `IProgressSink` over a `ProgressBar` and `RichTextBox`. It must marshal to the UI thread, because Core now calls it from whatever thread runs the operation:

| `IProgressSink` | Old inline behavior |
|---|---|
| `Log(message)` | `RichTextBox.AppendText`, scroll to caret, via `Invoke` when `InvokeRequired` |
| `Reset(max)` | `Minimum = 0; Step = 1; Value = 0; Maximum = max` |
| `Step()` | `PerformStep()` |
| `Set(value)` | `Value = value` (used by `BLZCoder`) |

Then update the call sites that pass `TB_Progress` / `PB_Show` (`BLZCoder`, `CTRUtil.BuildROM`, `NCSD.ExtractFilesFromNCSD`, `CRO.E_HashCRR`, `RomFS.*`) and add the project back to `pk3DS.slnx` (Windows-only).

## 2. Move editing logic out of the forms

Only 3 of the 62 hand-written WinForms files are free of UI references (`Legality/Randomizer.cs`, `Subforms/Gen7/Wild/Wild7Randomizer.cs`, `Tools/Scripts.cs`). The other 59 mix UI with editing logic (GARC reads, data mapping, randomizing). Move logic into Core one editor at a time, leaving each form as a thin view, and expose it as CLI commands. Delete a form's logic from `pk3DS.WinForms` only after it has moved.

`Resources/img` (1,684 PNGs: Pokémon/item sprites, UI icons) is GUI data and stays with the front end.

## 3. Cross-platform image layer

Drafts were written but never compiled or tested, so they were removed from the tree. They remain in commit `92a953e` (for example `git show 92a953e:docs/deferred-ui/PngCodec.cs`):

- `PixelImage.cs`: `Rgba` struct and a BGRA `PixelImage` (`GetPixel`, `SetPixel`, `Crop`, `Transpose` = GDI+ `Rotate90FlipX`), replacing `Bitmap` / `Color`.
- `PngCodec.cs`: dependency-free PNG encode/decode (8-bit RGBA out; gray, RGB, palette, gray+alpha, RGBA in).
- `ETC1.managed.cs`: managed ETC1/ETC1A4 decoder to replace the native `ETC1Lib.dll`.

Remaining work:

- Rewrite `ImageUtil`, `BCLIM`, `SMDH` and `TypeChart` on `PixelImage`.
- Remove `System.Media.SystemSounds.Beep` in `BCLIM` (Windows-only).
- `BCLIM.GetBCLIM` used `Image.FromStream`, which accepted PNG/BMP/JPEG/GIF; the draft codec reads PNG only.
- Delete `ETC1Lib.dll` and its `Resources.resx` entry once the managed decoder is in.
- Drop the `<Compile Remove>` line.

**Unverified:** the ETC1 decoder was written from the ETC1 spec and the 3DS block layout, not compared against the old DLL. The DLL's output layout (row-major vs column-major, any Y flip) is inferred from how `ImageUtil.DecodeETC` indexes it. Verify against a real `.bclim` / `.bflim` before relying on it.

## 4. Backend issues found while building the CLI (not UI, not yet fixed)

- `Stream.Read` return values are ignored in several places (`CA2022` warnings in `RomFS`, `NCSD`, `LZSS`, `SARC`, `CTR`).
- `RomFS` keeps `ROOT_DIR` / `OutFile` as statics and writes `tempRomFS.bin`, `romfs.bin` and `exefs.bin` to the current directory, so builds are not reentrant and depend on the working directory.
- `BLZCoder` and a few others write to `Console` directly instead of the sink.
- `GARC` used static events (`FileCountDetermined`, `PackProgressed`, `UnpackProgressed`) for progress; they are replaced by `IProgressSink` parameters, so `pk3DS.WinForms/GarcUtil.cs` needs the same adapter as the other call sites.
- Not yet exposed by the CLI: `Mini`, `SARC`, `DARC`, `ALYT`, `Exheader` info, `TextFile`/`TextData`, the personal/learnset/egg-move structures, the randomizers and `GameConfig`. Most of the editor logic still lives inside the WinForms forms.
- Fixed after testing against a real Omega Ruby CXI: `NCSD.ExtractCXIfromNCSD` wrote a temp CXI 10x too large; `RomFS.isTempFile` was always true so `rom build` deleted the caller's RomFS file; extraction used `FileMode.Append`, so re-extracting into an existing folder doubled the files.
- `NCSD.ExtractCXIfromNCSD` still hardcodes the first partition offset to `0x4000` instead of reading it from the header.
- `rom build` output uses fixed-crypto (zero key) encryption, so `ncsd extract` cannot read a `.3ds` that pk3DS itself built.
