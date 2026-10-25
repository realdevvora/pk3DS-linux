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

## 4. Backend issues still open

What was fixed is listed in [differences-from-upstream.md](differences-from-upstream.md). Still open:

- `RomFS` keeps `ROOT_DIR` / `OutFile` as statics and writes `tempRomFS.bin` and `romfs.bin` to the current directory while building, so builds are not reentrant and depend on the working directory.
- `BLZCoder` (19 calls), `FileFormat`, `GameBackup` and a few others write to `Console` directly instead of the progress sink.
- One unchecked `Stream.Read` remains in `RomFS.MakeRomFSData`; it fills a buffer that is never used.
- `pk3DS.WinForms/GarcUtil.cs` still uses the removed static GARC events, and every other WinForms call site still passes `ProgressBar` / `RichTextBox`; they need an `IProgressSink` adapter.
- `NCSD.ExtractCXIfromNCSD` hardcodes the first partition offset to `0x4000` instead of reading it from the header.
- `rom build` output uses fixed-crypto (zero key) encryption, so `ncsd extract` cannot read a `.3ds` that pk3DS itself built.
- Not yet exposed by the CLI: `SARC`, `DARC`, `ALYT`, `Exheader` info, the egg-move and evolution structures, static and wild encounters, the randomizers and trainer, item and shop data. Most of the editor logic still lives inside the WinForms forms.
- The text writer (`TextFile`) does not reproduce the original bytes: it always terminates and pads each line, writes the line-table flag word as 0 (the original has non-zero values such as `4` on some lines), and trims each line. The decoded text round-trips exactly (160 of the 172 files in a test edit; the other 12 only lost a leading space), but whether the flag word matters in game is unverified. `text import` therefore leaves unchanged files alone.
