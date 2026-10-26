using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace pk3DS.Mac.Services;

/// <summary>Pokémon and item sprites, chosen the way pk3DS's WinFormsUtil.GetSprite chooses them.</summary>
public static class Sprites
{
    private static readonly int[] GenderedSpecies = [592, 593, 521, 668]; // Frillish & Jellicent, Unfezant & Pyroar
    private static readonly int[] DefaultSprites = [778, 664, 665, 414, 493, 773]; // show their default sprite regardless of form
    private static readonly Dictionary<string, Bitmap> Cache = [];

    public static Bitmap Pokemon(int species, int form, int gender = 0)
    {
        if (DefaultSprites.Contains(species))
            form = 0;
        // pk3DS names the resource _species_form; the files spell the separator as '-' or '_'.
        string suffix = form > 0 ? form.ToString() : gender == 1 && GenderedSpecies.Contains(species) ? "1" : null;
        string[] names = suffix == null ? [$"{species}"] : [$"{species}-{suffix}", $"{species}_{suffix}", $"{species}"];
        return names.Select(n => Load($"Pokemon/{n}.png")).FirstOrDefault(b => b != null) ?? Load("Pokemon/unknown.png");
    }

    public static Bitmap Item(int item) => item <= 0 ? null : Load($"Items/item_{item}.png");

    private static Bitmap Load(string path)
    {
        if (Cache.TryGetValue(path, out var cached))
            return cached;
        var uri = new Uri($"avares://pk3DS/Assets/{path}");
        var bitmap = AssetLoader.Exists(uri) ? new Bitmap(AssetLoader.Open(uri)) : null;
        Cache[path] = bitmap;
        return bitmap;
    }
}
