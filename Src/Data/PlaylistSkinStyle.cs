using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;

namespace GodAmp.Data;

/// <summary>Playlist colors and preferred font from a classic skin's PLEDIT.TXT file.</summary>
/// <param name="Normal">Text color for tracks that are not playing.</param>
/// <param name="Current">Text color for the playing track.</param>
/// <param name="Background">Playlist background color.</param>
/// <param name="SelectedBackground">Selected-row background color.</param>
/// <param name="FontName">Preferred system font family.</param>
public sealed record PlaylistSkinStyle(Color Normal, Color Current, Color Background, Color SelectedBackground, string FontName)
{
    /// <summary>Gets classic Winamp's default playlist styling.</summary>
    public static PlaylistSkinStyle Default { get; } = new(
        new Color("00ff00"), Colors.White, Colors.Black, new Color("0000c6"), "Arial");

    /// <summary>Reads the Text section, substituting defaults independently for invalid or missing fields.</summary>
    /// <param name="text">Decoded PLEDIT.TXT contents.</param>
    /// <returns>Playlist styling with case-insensitive setting names and validated RGB colors.</returns>
    public static PlaylistSkinStyle Parse(string text)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        bool inTextSection = false;
        foreach (string raw in text.Split('\n'))
        {
            string line = raw.Trim().TrimStart('\uFEFF');
            if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#'))
                continue;
            if (line.StartsWith('['))
            {
                inTextSection = line.Equals("[Text]", StringComparison.OrdinalIgnoreCase);
                continue;
            }
            int separator = line.IndexOf('=');
            if (inTextSection && separator > 0)
                values[line[..separator].Trim()] = line[(separator + 1)..].Split(';')[0].Trim();
        }

        Color ReadColor(string name, Color fallback)
        {
            if (!values.TryGetValue(name, out string? value))
                return fallback;
            value = value.TrimStart('#');
            if (value.Length != 6 || !uint.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint rgb))
                return fallback;
            return Color.FromHtml($"#{rgb:x6}");
        }

        string font = values.GetValueOrDefault("Font", Default.FontName).Trim('"');
        return new PlaylistSkinStyle(
            ReadColor("Normal", Default.Normal), ReadColor("Current", Default.Current),
            ReadColor("NormalBG", Default.Background), ReadColor("SelectedBG", Default.SelectedBackground),
            string.IsNullOrWhiteSpace(font) ? Default.FontName : font);
    }

    /// <summary>Selects the requested installed family, then Arial or Helvetica.</summary>
    /// <param name="availableFonts">Installed font family names.</param>
    /// <returns>The installed family name, or an empty string to request Godot's fallback font.</returns>
    public string ResolveFontName(IEnumerable<string> availableFonts)
    {
        string[] names = [.. availableFonts];
        foreach (string candidate in new[] { FontName, Default.FontName, "Helvetica" })
        {
            string? match = names.FirstOrDefault(name => name.Equals(candidate, StringComparison.OrdinalIgnoreCase));
            if (match != null)
                return match;
        }
        return "";
    }
}
