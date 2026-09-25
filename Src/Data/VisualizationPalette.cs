using System;
using System.Globalization;
using Godot;

namespace GodAmp.Data;

/// <summary>Immutable classic visualization colors, in VISCOLOR.TXT slot order.</summary>
public sealed class VisualizationPalette
{
    private readonly Color[] _colors;

    /* Classic format: background, dots, sixteen spectrum levels, five scope levels, peak. */
    private static readonly uint[] DefaultRgb =
    [
        0x000000, 0x181829, 0xef3110, 0xce2910, 0xd65a00, 0xd66600,
        0xd67300, 0xc67b08, 0xdea518, 0xd6b521, 0xbdde29, 0x94de21,
        0x29ce10, 0x32be10, 0x39b510, 0x319c08, 0x299400, 0x188408,
        0xffffff, 0xd6d6de, 0xb5bdbd, 0xa0aaaf, 0x949ca5, 0x969696
    ];

    /// <summary>Built-in Winamp palette for absent or incomplete metadata.</summary>
    public static VisualizationPalette Default { get; } = Parse("");

    private VisualizationPalette(Color[] colors) => _colors = colors;

    /// <summary>Gets a color by its classic palette index.</summary>
    /// <param name="index">Zero-based slot from 0 through 23.</param>
    public Color this[int index] => _colors[index];

    /// <summary>Reads ordered decimal RGB lines, retaining defaults for invalid or omitted slots.</summary>
    /// <param name="text">Contents of VISCOLOR.TXT, optionally containing trailing comments.</param>
    /// <returns>A complete palette independent of the previously selected skin.</returns>
    public static VisualizationPalette Parse(string text)
    {
        var colors = new Color[DefaultRgb.Length];
        for (int i = 0; i < colors.Length; i++)
            colors[i] = new Color((DefaultRgb[i] << 8) | 0xff);
        string[] lines = text.TrimStart('\uFEFF').Split('\n');
        for (int i = 0; i < Math.Min(lines.Length, colors.Length); i++)
        {
            string[] fields = lines[i].Split([',', ' ', '\t', '\r'], StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length >= 3 && byte.TryParse(fields[0], NumberStyles.None, CultureInfo.InvariantCulture, out byte red) &&
                byte.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out byte green) &&
                byte.TryParse(fields[2], NumberStyles.None, CultureInfo.InvariantCulture, out byte blue))
                colors[i] = Color.Color8(red, green, blue);
        }
        return new VisualizationPalette(colors);
    }
}
