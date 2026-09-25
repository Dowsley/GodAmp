using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;

namespace GodAmp.Data;

/// <summary>Classic window modes with REGION.TXT support in Winamp.</summary>
public enum SkinRegionMode
{
    /// <summary>The expanded main window.</summary>
    Normal,
    /// <summary>The collapsed main window.</summary>
    WindowShade,
    /// <summary>The expanded equalizer window.</summary>
    Equalizer,
    /// <summary>The collapsed equalizer window.</summary>
    EqualizerWS
}

/// <summary>Immutable classic window contours, retaining vertex order for nonzero winding fills.</summary>
public sealed class SkinRegions
{
    /* Winamp reads each region value into a 65,535-byte profile buffer. */
    private const int MaximumValueLength = 65534;
    private readonly Dictionary<SkinRegionMode, IReadOnlyList<IReadOnlyList<Vector2I>>> _regions;

    /// <summary>Gets rectangular-window defaults with no custom contours.</summary>
    public static SkinRegions Default { get; } = new([]);

    private SkinRegions(Dictionary<SkinRegionMode, IReadOnlyList<IReadOnlyList<Vector2I>>> regions)
        => _regions = regions;

    /// <summary>Gets implicitly closed contours in unscaled skin coordinates.</summary>
    /// <param name="mode">The main or equalizer window mode.</param>
    /// <returns>Ordered contours, or an empty list indicating a rectangular window.</returns>
    public IReadOnlyList<IReadOnlyList<Vector2I>> GetPolygons(SkinRegionMode mode)
        => _regions.GetValueOrDefault(mode) ?? [];

    /// <summary>Reads supported sections independently, discarding incomplete or malformed sections.</summary>
    /// <param name="text">REGION.TXT contents with case-insensitive section and key names.</param>
    /// <returns>Validated contours with rectangular defaults for absent or invalid sections.</returns>
    public static SkinRegions Parse(string text)
    {
        var sections = new Dictionary<SkinRegionMode, Dictionary<string, string>>();
        Dictionary<string, string>? section = null;
        foreach (string raw in text.TrimStart('\uFEFF').Split('\n'))
        {
            string line = raw.Split(';', 2)[0].Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;
            if (line.StartsWith('['))
            {
                section = null;
                if (line.EndsWith(']') && TryReadMode(line[1..^1].Trim(), out SkinRegionMode mode) &&
                    !sections.ContainsKey(mode))
                {
                    section = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    sections.Add(mode, section);
                }
                continue;
            }
            int separator = line.IndexOf('=');
            if (section != null && separator > 0)
            {
                string key = line[..separator].Trim();
                if (key.Equals("NumPoints", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("PointList", StringComparison.OrdinalIgnoreCase))
                    section.TryAdd(key, line[(separator + 1)..].Trim());
            }
        }

        var regions = new Dictionary<SkinRegionMode, IReadOnlyList<IReadOnlyList<Vector2I>>>();
        foreach (var (mode, fields) in sections)
        {
            int[]? counts = ReadNumbers(fields.GetValueOrDefault("NumPoints", ""));
            int[]? coordinates = ReadNumbers(fields.GetValueOrDefault("PointList", ""));
            if (counts == null || coordinates == null || coordinates.Length % 2 != 0)
                continue;
            long total = 0;
            foreach (int count in counts)
            {
                if (count < 3)
                {
                    total = -1;
                    break;
                }
                total += count;
            }
            if (total != coordinates.Length / 2)
                continue;

            var polygons = new List<IReadOnlyList<Vector2I>>(counts.Length);
            int offset = 0;
            foreach (int count in counts)
            {
                var points = new Vector2I[count];
                for (int i = 0; i < count; i++)
                {
                    points[i] = new Vector2I(coordinates[offset], coordinates[offset + 1]);
                    offset += 2;
                }
                polygons.Add(Array.AsReadOnly(points));
            }
            regions.Add(mode, polygons.AsReadOnly());
        }
        return new SkinRegions(regions);
    }

    /// <summary>Matches named sections without accepting numeric enum values.</summary>
    /// <param name="name">Trimmed section name.</param>
    /// <param name="mode">The matching mode, or its default value when unsupported.</param>
    /// <returns>True for a supported classic region section.</returns>
    private static bool TryReadMode(string name, out SkinRegionMode mode)
    {
        foreach (SkinRegionMode candidate in Enum.GetValues<SkinRegionMode>())
        {
            if (name.Equals(candidate.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                mode = candidate;
                return true;
            }
        }
        mode = default;
        return false;
    }

    /// <summary>Decodes a bounded list of signed decimal coordinates or polygon vertex counts.</summary>
    /// <param name="value">A comma, space, or tab-separated profile value.</param>
    /// <returns>Parsed integers, or null for an empty, oversized, or malformed value.</returns>
    private static int[]? ReadNumbers(string value)
    {
        if (value.Length == 0 || value.Length > MaximumValueLength)
            return null;
        string[] tokens = value.Split([',', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
            return null;
        var numbers = new int[tokens.Length];
        for (int i = 0; i < tokens.Length; i++)
        {
            if (!int.TryParse(tokens[i], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out numbers[i]))
                return null;
        }
        return numbers;
    }
}
