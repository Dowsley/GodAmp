using System;
using System.Collections.Generic;
using GodAmp.Skins.Archives;
using GodAmp.Skins.Fonts;
using GodAmp.Skins.Regions;
using GodAmp.Skins.Cursors;
using Godot;

namespace GodAmp.Skins;

/// <summary>Resolves a complete skin against explicit defaults without publishing or mutating live atlases.</summary>
internal sealed class SkinStateBuilder
{
    private readonly SkinState _defaults;
    private readonly IReadOnlyDictionary<string, Image> _images;

    /// <summary>Retains built-in assets for independent fallback resolution on every load.</summary>
    /// <param name="defaults">Complete built-in skin resources.</param>
    /// <param name="images">Built-in source images indexed by classic sheet name.</param>
    public SkinStateBuilder(SkinState defaults, IReadOnlyDictionary<string, Image> images)
    {
        _defaults = defaults;
        _images = images;
    }

    /// <summary>Prepares fallback resources from Inspector-authored artwork and cursor assets.</summary>
    /// <param name="textures">Built-in source textures indexed by sheet name.</param>
    /// <param name="images">Built-in images, including text and number glyph sheets.</param>
    /// <param name="cursors">Built-in cursor roles and assets.</param>
    /// <param name="systemFonts">Available font families supplied by the engine boundary.</param>
    /// <param name="fallbackFont">Engine fallback when a requested family is unavailable.</param>
    /// <param name="zoom">Requested font rasterization scale.</param>
    /// <returns>A complete default skin, independent of active controls.</returns>
    public static SkinState BuildDefault(IReadOnlyDictionary<string, Texture2D> textures,
        IReadOnlyDictionary<string, Image> images, IReadOnlyDictionary<SkinCursorRole, SkinCursor> cursors,
        string[] systemFonts, Font fallbackFont, int zoom)
    {
        var style = PlaylistSkinStyle.Default;
        return new SkinState(textures, SkinBitmapFont.CreateText(images["TEXT"]),
            SkinBitmapFont.CreateNumbers(images["NUMBERS"], false), style,
            CreatePlaylistFont(style, systemFonts, fallbackFont, zoom), VisualizationPalette.Default,
            new GenericTitleFont(images["GEN"]), SkinRegions.Default, cursors, true);
    }

    /// <summary>Resolves missing archive assets against built-in resources.</summary>
    /// <param name="archive">Decoded, validated archive assets.</param>
    /// <param name="systemFonts">Available font families supplied by the caller.</param>
    /// <param name="fallbackFont">Engine fallback font.</param>
    /// <param name="zoom">Requested font rasterization scale.</param>
    /// <returns>Complete prepared resources ready for transactional application.</returns>
    public SkinState Build(SkinArchive archive, string[] systemFonts, Font fallbackFont, int zoom)
    {
        var textures = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, original) in _defaults.Textures)
        {
            Image fallback = _images[name];
            if (name.Equals("BALANCE", StringComparison.OrdinalIgnoreCase) && !archive.Images.ContainsKey(name))
                fallback = _images["VOLUME"];
            Image resolved = archive.ResolveImage(name, fallback);
            textures[name] = ReferenceEquals(resolved, _images[name])
                ? original : ImageTexture.CreateFromImage(resolved);
        }
        Image text = archive.Images.GetValueOrDefault("TEXT", _images["TEXT"]);
        bool extended = archive.Images.TryGetValue("NUMS_EX", out Image? numbers);
        numbers ??= archive.Images.GetValueOrDefault("NUMBERS", _images["NUMBERS"]);
        Image genericArtwork = textures["GEN"].GetImage();
        var cursors = new Dictionary<SkinCursorRole, SkinCursor>(_defaults.Cursors);
        foreach (var (role, cursor) in archive.Cursors)
            cursors[role] = cursor;
        return new SkinState(textures, SkinBitmapFont.CreateText(text),
            SkinBitmapFont.CreateNumbers(numbers, extended), archive.PlaylistStyle,
            CreatePlaylistFont(archive.PlaylistStyle, systemFonts, fallbackFont, zoom),
            archive.Palette, new GenericTitleFont(genericArtwork), archive.Regions, cursors,
            archive.Images.ContainsKey("EQ_EX") || !archive.Images.ContainsKey("EQMAIN"));
    }

    private static Font CreatePlaylistFont(PlaylistSkinStyle style, string[] systemFonts, Font fallbackFont, int zoom)
    {
        string name = style.ResolveFontName(systemFonts);
        return name.Length == 0 ? fallbackFont : new SystemFont
        {
            FontNames = [name],
            Antialiasing = TextServer.FontAntialiasing.Gray,
            Oversampling = Math.Max(1, zoom),
            AllowSystemFallback = true
        };
    }
}
