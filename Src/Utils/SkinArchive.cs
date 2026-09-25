using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Godot;
using GodAmp.Data;

namespace GodAmp.Utils;

/// <summary>Reads and resolves supported classic Winamp skin assets without extracting files.</summary>
public sealed class SkinArchive
{
    private const int MaximumAssetBytes = 16 * 1024 * 1024;
    private readonly Dictionary<string, Image> _images = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<SkinCursorRole, SkinCursor> _cursors = [];

    /// <summary>Gets supported decoded cursors; omitted or invalid assets use system defaults.</summary>
    public IReadOnlyDictionary<SkinCursorRole, SkinCursor> Cursors => _cursors;

    /// <summary>Gets decoded sheets keyed by case-insensitive filenames without extensions.</summary>
    public IReadOnlyDictionary<string, Image> Images => _images;
    /// <summary>Gets playlist settings, with defaults for absent or malformed fields.</summary>
    public PlaylistSkinStyle PlaylistStyle { get; private set; } = PlaylistSkinStyle.Default;
    /// <summary>Gets classic analyzer colors, with defaults for absent or invalid slots.</summary>
    public VisualizationPalette Palette { get; private set; } = VisualizationPalette.Default;
    /// <summary>Gets classic window contours, with rectangular defaults for invalid or absent sections.</summary>
    public SkinRegions Regions { get; private set; } = SkinRegions.Default;

    /* Minimum dimensions are defined by the classic skin sprite layout. */
    private static readonly Dictionary<string, Vector2I> MinimumSizes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["MAIN"] = new(275, 116), ["CBUTTONS"] = new(136, 36),
        ["TITLEBAR"] = new(302, 29), ["SHUFREP"] = new(92, 85),
        ["VOLUME"] = new(68, 418), ["BALANCE"] = new(47, 418),
        ["POSBAR"] = new(307, 5), ["EQMAIN"] = new(275, 315),
        ["PLEDIT"] = new(280, 186), ["GEN"] = new(194, 109),
        ["TEXT"] = new(155, 18), ["NUMBERS"] = new(99, 13), ["NUMS_EX"] = new(108, 13),
        ["PLAYPAUS"] = new(42, 9), ["MONOSTER"] = new(57, 24)
    };

    /// <summary>Decodes recognized assets, preferring shallow paths and BMP files for duplicate names.</summary>
    /// <param name="path">Filesystem path to a ZIP-compatible skin archive.</param>
    /// <returns>A validated archive containing at least one supported image.</returns>
    /// <exception cref="InvalidDataException">The archive or a supported asset is invalid or oversized.</exception>
    /// <exception cref="IOException">The archive cannot be read.</exception>
    public static SkinArchive Read(string path)
    {
        using var zip = ZipFile.OpenRead(path);
        var skin = new SkinArchive();
        /* Resolve names in memory so nested Windows paths work on every platform. */
        var entries = zip.Entries.OrderBy(e => e.FullName.Count(c => c is '/' or '\\'))
            .ThenBy(e => Path.GetExtension(e.FullName).Equals(".bmp", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(e => e.FullName, StringComparer.Ordinal).ToArray();
        bool hasStyle = false;
        bool hasPalette = false;
        bool hasRegions = false;
        var seenCursors = new HashSet<SkinCursorRole>();
        foreach (var entry in entries)
        {
            string name = entry.FullName.Replace('\\', '/').Split('/')[^1];
            string stem = Path.GetFileNameWithoutExtension(name);
            string extension = Path.GetExtension(name).ToLowerInvariant();
            bool cursor = Enum.TryParse(stem, true, out SkinCursorRole role) &&
                Enum.GetName(role)?.Equals(stem, StringComparison.OrdinalIgnoreCase) == true && extension == ".cur";
            if (cursor && !seenCursors.Add(role))
                continue;
            bool playlistMetadata = name.Equals("pledit.txt", StringComparison.OrdinalIgnoreCase);
            bool paletteMetadata = name.Equals("viscolor.txt", StringComparison.OrdinalIgnoreCase);
            bool regionMetadata = name.Equals("region.txt", StringComparison.OrdinalIgnoreCase);
            bool metadata = playlistMetadata || paletteMetadata || regionMetadata;
            if ((playlistMetadata && hasStyle) || (paletteMetadata && hasPalette) || (regionMetadata && hasRegions))
                continue;
            if (!metadata && !cursor && (!MinimumSizes.ContainsKey(stem) || skin.Images.ContainsKey(stem) ||
                              extension is not (".bmp" or ".png" or ".jpg" or ".jpeg")))
                continue;
            if (entry.Length > MaximumAssetBytes)
                throw new InvalidDataException($"Skin asset is too large: {name}");
            using var input = entry.Open();
            using var buffer = new MemoryStream();
            input.CopyTo(buffer);
            byte[] bytes = buffer.ToArray();
            if (cursor)
            {
                SkinCursor? decoded = SkinCursorDecoder.Decode(bytes);
                if (decoded != null)
                    skin._cursors.Add(role, decoded);
                else
                    GD.PushWarning($"Using the system cursor for unsupported or invalid skin asset: {name}");
                continue;
            }
            if (metadata)
            {
                string text;
                try { text = new UTF8Encoding(false, true).GetString(bytes); }
                catch (DecoderFallbackException) { text = Encoding.Latin1.GetString(bytes); }
                if (playlistMetadata)
                {
                    skin.PlaylistStyle = PlaylistSkinStyle.Parse(text);
                    hasStyle = true;
                }
                else if (paletteMetadata)
                {
                    skin.Palette = VisualizationPalette.Parse(text);
                    hasPalette = true;
                }
                else
                {
                    skin.Regions = SkinRegions.Parse(text);
                    hasRegions = true;
                }
                continue;
            }

            var image = new Image();
            Error error = extension switch
            {
                ".bmp" => image.LoadBmpFromBuffer(bytes),
                ".png" => image.LoadPngFromBuffer(bytes),
                _ => image.LoadJpgFromBuffer(bytes)
            };
            var minimum = MinimumSizes[stem];
            if (error != Error.Ok || image.IsEmpty() || image.GetWidth() < minimum.X || image.GetHeight() < minimum.Y)
                throw new InvalidDataException($"Invalid skin image {name}; expected at least {minimum.X}x{minimum.Y} pixels.");
            image.Convert(Image.Format.Rgba8);
            skin._images.Add(stem, image);
        }
        if (skin.Images.Count == 0)
            throw new InvalidDataException("The archive contains no supported classic skin images.");
        return skin;
    }

    /// <summary>Resolves a sheet, using volume artwork for absent balance artwork and padding cropped sheets.</summary>
    /// <param name="name">Sheet name, optionally including its extension.</param>
    /// <param name="fallback">Default image and minimum output dimensions.</param>
    /// <returns>The matching image, a transparently padded copy, or the unchanged fallback.</returns>
    public Image ResolveImage(string name, Image fallback)
    {
        string key = Path.GetFileNameWithoutExtension(name);
        Image? image = Images.GetValueOrDefault(key);
        if (image == null && key.Equals("BALANCE", StringComparison.OrdinalIgnoreCase))
            image = Images.GetValueOrDefault("VOLUME");
        if (image == null)
            return fallback;
        if (image.GetWidth() >= fallback.GetWidth() && image.GetHeight() >= fallback.GetHeight())
            return image;

        /* Cropped sheets omit optional artwork. Transparent padding preserves the panel beneath it. */
        var resolved = Image.CreateEmpty(Math.Max(fallback.GetWidth(), image.GetWidth()),
            Math.Max(fallback.GetHeight(), image.GetHeight()), false, Image.Format.Rgba8);
        resolved.BlitRect(image, new Rect2I(Vector2I.Zero, image.GetSize()), Vector2I.Zero);
        return resolved;
    }
}
