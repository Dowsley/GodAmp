using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;

namespace GodAmp.Playlists.Serialization;

/// <summary>Parses and writes numbered PLS entries without filesystem access.</summary>
internal sealed class PLSSerializer : IPlaylistSerializer
{
    private const string PlaylistSection = "[playlist]";
    private const string EntryCountKey = "NumberOfEntries";
    private const string FileKey = "File";
    private const string TitleKey = "Title";
    private const string LengthKey = "Length";

    /// <summary>Parses the playlist section in numeric entry order, including sparse indexes.</summary>
    /// <param name="text">Decoded PLS contents.</param>
    /// <param name="cancellation">Cancels between lines.</param>
    /// <returns>Entries with unresolved paths; malformed or ambiguous empty lists throw InvalidDataException.</returns>
    public PlaylistEntry[] Parse(string text, CancellationToken cancellation)
    {
        var fields = new SortedDictionary<int, Dictionary<string, string>>();
        bool inPlaylist = false;
        bool foundSection = false;
        bool explicitlyEmpty = false;
        using var reader = new StringReader(text);
        while (reader.ReadLine() is { } raw)
        {
            cancellation.ThrowIfCancellationRequested();
            string line = raw.Trim();
            if (line.StartsWith('['))
            {
                inPlaylist = line.Equals(PlaylistSection, StringComparison.OrdinalIgnoreCase);
                foundSection |= inPlaylist;
                continue;
            }
            int separator = line.IndexOf('=');
            if (!inPlaylist || separator < 0 || line.StartsWith(';') || line.StartsWith('#')) continue;
            string key = line[..separator].Trim();
            string value = line[(separator + 1)..].Trim();
            if (key.Equals(EntryCountKey, StringComparison.OrdinalIgnoreCase)) explicitlyEmpty = value == "0";
            int suffix = key.Length;
            while (suffix > 0 && char.IsAsciiDigit(key[suffix - 1])) suffix--;
            if (!int.TryParse(key[suffix..], NumberStyles.None, CultureInfo.InvariantCulture, out int index) || index <= 0) continue;
            if (!fields.TryGetValue(index, out var entry))
                fields[index] = entry = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            entry[key[..suffix]] = value;
        }
        PlaylistEntry[] entries = [.. fields.Values.Where(entry => !string.IsNullOrWhiteSpace(entry.GetValueOrDefault(FileKey)))
            .Select(entry => new PlaylistEntry(entry[FileKey], entry.GetValueOrDefault(TitleKey), PlaylistText.ParseDuration(entry.GetValueOrDefault(LengthKey))))];
        if (!foundSection || entries.Length == 0 && !explicitlyEmpty)
            throw new InvalidDataException("PLS requires a [playlist] section and file entries, or NumberOfEntries=0.");
        return entries;
    }

    /// <summary>Writes numbered entries and the PLS version and entry count.</summary>
    /// <param name="writer">Destination owned by the caller.</param>
    /// <param name="entries">Entries with paths prepared for the destination directory.</param>
    public void Write(TextWriter writer, IEnumerable<PlaylistEntry> entries)
    {
        writer.WriteLine(PlaylistSection);
        int index = 0;
        foreach (PlaylistEntry entry in entries)
        {
            index++;
            writer.WriteLine($"{FileKey}{index}={entry.Path}");
            writer.WriteLine($"{TitleKey}{index}={PlaylistText.FormatTitle(entry.Title)}");
            writer.WriteLine($"{LengthKey}{index}={PlaylistText.FormatDuration(entry.Duration)}");
        }
        writer.WriteLine($"{EntryCountKey}={index}");
        writer.WriteLine("Version=2");
    }
}
