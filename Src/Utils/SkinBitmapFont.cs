using Godot;

namespace GodAmp.Utils;

/// <summary>Builds fixed-cell fonts from classic Winamp text and clock sheets.</summary>
public static class SkinBitmapFont
{
    /// <summary>Width of a TEXT glyph in logical pixels.</summary>
    public const int TextWidth = 5;
    /// <summary>Height of a TEXT glyph in logical pixels.</summary>
    public const int TextHeight = 6;
    /// <summary>Width and advance of a clock digit in logical pixels.</summary>
    public const int NumberWidth = 9;
    /// <summary>Height of a clock digit in logical pixels.</summary>
    public const int NumberHeight = 13;

    /// <summary>Builds the classic text glyph map, including lowercase and punctuation aliases.</summary>
    /// <param name="image">Decoded TEXT sheet; glyph positions are independent of the image dimensions.</param>
    /// <returns>A pixel-aligned font preserving the sheet's colors without system-font fallback.</returns>
    public static FontFile CreateText(Image image)
    {
        var font = Create(image, TextHeight);
        string[] rows = ["ABCDEFGHIJKLMNOPQRSTUVWXYZ\"@\u0002\u0003 ", "0123456789….:()-'!_+\\/[]^&%,=$#", "ÅÖÄ?*"];
        for (int row = 0; row < rows.Length; row++)
            for (int column = 0; column < rows[row].Length; column++)
                AddGlyph(font, rows[row][column], new Rect2(column * TextWidth, row * TextHeight, TextWidth, TextHeight), TextHeight);
        for (char character = 'a'; character <= 'z'; character++)
            AddGlyph(font, character, new Rect2((character - 'a') * TextWidth, 0, TextWidth, TextHeight), TextHeight);
        foreach (var (character, source) in new[] { ('~', '^'), ('{', '['), ('<', '['), ('}', ']'), ('>', ']'), ('`', '\''), ('å', 'Å'), ('ö', 'Ö'), ('ä', 'Ä'), ('\u0001', '…') })
            AddGlyph(font, character, font.GetGlyphUVRect(0, new Vector2I(TextHeight, 0), source), TextHeight);
        return font;
    }

    /// <summary>Builds clock digits, a blank cell, and an extended or synthesized minus glyph.</summary>
    /// <param name="image">Decoded NUMBERS or NUMS_EX sheet.</param>
    /// <param name="extended">Whether the sheet includes the NUMS_EX minus cell after the blank cell.</param>
    /// <returns>A fixed-size clock font with nine-pixel advances.</returns>
    public static FontFile CreateNumbers(Image image, bool extended)
    {
        var font = Create(image, NumberHeight);
        for (int digit = 0; digit < 10; digit++)
            AddGlyph(font, '0' + digit, new Rect2(digit * NumberWidth, 0, NumberWidth, NumberHeight), NumberHeight);
        AddGlyph(font, ' ', new Rect2(10 * NumberWidth, 0, NumberWidth, NumberHeight), NumberHeight);
        if (extended)
            AddGlyph(font, '-', new Rect2(11 * NumberWidth, 0, NumberWidth, NumberHeight), NumberHeight);
        else
        {
            /* Classic number sheets provide the minus stroke within a digit. */
            AddGlyph(font, '-', new Rect2(20, 6, 5, 1), NumberHeight);
            font.SetGlyphOffset(0, new Vector2I(NumberHeight, 0), '-', new Vector2(2, -7));
            font.SetGlyphAdvance(0, NumberHeight, '-', new Vector2(NumberWidth, 0));
        }
        return font;
    }

    /// <summary>Initializes a single-texture bitmap font with a baseline at the bottom of each cell.</summary>
    /// <param name="image">Glyph atlas image.</param>
    /// <param name="height">Logical font size and ascent in pixels.</param>
    /// <returns>An empty glyph cache backed by the supplied image.</returns>
    private static FontFile Create(Image image, int height)
    {
        var font = new FontFile
        {
            FixedSize = height,
            FixedSizeScaleMode = TextServer.FixedSizeScaleMode.Disable,
            AllowSystemFallback = false,
            ModulateColorGlyphs = false
        };
        font.SetTextureImage(0, new Vector2I(height, 0), 0, image);
        font.SetCacheAscent(0, height, height);
        font.SetCacheDescent(0, height, 0);
        return font;
    }

    /// <summary>Registers a glyph region with a horizontal advance equal to its width.</summary>
    /// <param name="font">Destination font cache.</param>
    /// <param name="character">Unicode code point used as the glyph index.</param>
    /// <param name="region">Source rectangle in atlas pixels.</param>
    /// <param name="height">Logical font size and baseline offset.</param>
    private static void AddGlyph(FontFile font, int character, Rect2 region, int height)
    {
        var size = new Vector2I(height, 0);
        font.SetGlyphTextureIdx(0, size, character, 0);
        font.SetGlyphUVRect(0, size, character, region);
        font.SetGlyphSize(0, size, character, region.Size);
        font.SetGlyphOffset(0, size, character, new Vector2(0, -height));
        font.SetGlyphAdvance(0, height, character, new Vector2(region.Size.X, 0));
    }
}
