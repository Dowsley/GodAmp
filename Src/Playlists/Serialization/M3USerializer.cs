using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace GodAmp.Playlists.Serialization;

/// <summary>Parses and writes M3U entries and their EXTINF metadata without filesystem access.</summary>
internal sealed class M3USerializer : IPlaylistSerializer
{
    private const string ExtendedInfo = "#EXTINF:";

    /// <summary>Parses playlist lines, associating EXTINF metadata with the next source entry.</summary>
    /// <param name="text">Decoded M3U or M3U8 contents.</param>
    /// <param name="cancellation">Cancels between lines.</param>
    /// <returns>Ordered entries with unresolved paths and optional cached metadata.</returns>
    public PlaylistEntry[] Parse(string text, CancellationToken cancellation)
    {
        List<PlaylistEntry> entries = [];
        string? title = null;
        float? duration = null;
        using var reader = new StringReader(text);
        while (reader.ReadLine() is { } raw)
        {
            cancellation.ThrowIfCancellationRequested();
            string line = raw.Trim();
            if (line.StartsWith(ExtendedInfo, StringComparison.OrdinalIgnoreCase))
            {
                int comma = line.IndexOf(',');
                title = comma >= 0 ? line[(comma + 1)..].Trim() : null;
                duration = comma >= 0 ? PlaylistText.ParseDuration(line[ExtendedInfo.Length..comma]) : null;
            }
            else if (line.Length > 0 && !line.StartsWith('#'))
            {
                entries.Add(new PlaylistEntry(line, title, duration));
                title = null;
                duration = null;
            }
        }
        return [.. entries];
    }

    /// <summary>Writes extended M3U text in occurrence order.</summary>
    /// <param name="writer">Destination owned by the caller.</param>
    /// <param name="entries">Entries with paths prepared for the destination directory.</param>
    public void Write(TextWriter writer, IEnumerable<PlaylistEntry> entries)
    {
        writer.WriteLine("#EXTM3U");
        foreach (PlaylistEntry entry in entries)
        {
            writer.WriteLine($"{ExtendedInfo}{PlaylistText.FormatDuration(entry.Duration)},{PlaylistText.FormatTitle(entry.Title)}");
            writer.WriteLine(entry.Path.StartsWith('#') ? "./" + entry.Path : entry.Path);
        }
    }
}
