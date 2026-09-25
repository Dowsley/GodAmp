using System.Collections.Generic;
using Godot;

namespace GodAmp.Data;

/// <summary>Variable-width title glyphs delimited by the marker rows in GEN.BMP.</summary>
public sealed class GenericTitleFont
{
    private readonly Dictionary<char, Rect2> _glyphs = [];
    /// <summary>Advance used for spaces and unavailable glyphs in the classic title font.</summary>
    public const int SpaceWidth = 5;

    /// <summary>Reads bounded alphabet and numeral marker runs from the supplied generic sheet.</summary>
    /// <param name="image">Decoded GEN.BMP, including the title glyph rows.</param>
    public GenericTitleFont(Image image)
    {
        ReadRow(image, "ABCDEFGHIJKLMNOPQRSTUVWXYZ", 90, 88);
        ReadRow(image, "0123456789-:", 74, 72);
    }

    /// <summary>Extracts marker runs without reading beyond the sheet on malformed input.</summary>
    /// <param name="image">Generic artwork.</param>
    /// <param name="characters">Character order in this row.</param>
    /// <param name="markerY">Row whose background separates glyphs.</param>
    /// <param name="glyphY">Top of the active seven-pixel glyphs.</param>
    private void ReadRow(Image image, string characters, int markerY, int glyphY)
    {
        Color delimiter = image.GetPixel(0, markerY);
        int x = 0;
        foreach (char character in characters)
        {
            while (x < image.GetWidth() && image.GetPixel(x, markerY) == delimiter)
                x++;
            int start = x;
            while (x < image.GetWidth() && image.GetPixel(x, markerY) != delimiter)
                x++;
            if (x == image.GetWidth())
                break;
            if (x > start)
                _glyphs[character] = new Rect2(start, glyphY, x - start, 7);
        }
    }

    /// <summary>Gets an active or inactive source rectangle; unsupported characters have an empty rectangle.</summary>
    /// <param name="character">Letter, digit, dash, colon, or spacing character.</param>
    /// <param name="active">Whether the owning native window is focused.</param>
    /// <returns>Skin source coordinates for the selected glyph state.</returns>
    public Rect2 GetGlyph(char character, bool active)
    {
        if (!_glyphs.TryGetValue(char.ToUpperInvariant(character), out Rect2 glyph))
            return default;
        if (!active)
            glyph.Position += new Vector2(0, 8);
        return glyph;
    }

    /// <summary>Measures a title using skin glyph advances and classic spacing.</summary>
    /// <param name="text">Window title.</param>
    /// <returns>Width in unscaled skin pixels.</returns>
    public int Measure(string text)
    {
        int width = 0;
        foreach (char character in text)
        {
            Rect2 glyph = GetGlyph(character, true);
            width += glyph.HasArea() ? (int)glyph.Size.X : SpaceWidth;
        }
        return width;
    }
}
