using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace GodAmp.Audio.Playlists;

/// <summary>Selects a playlist parser and owns file encoding, path resolution and safe replacement.</summary>
public static class PlaylistFile
{
    private const int WindowsCodePage = 1252;
    private sealed record FormatDefinition(IPlaylistFormat Parser, string DisplayName, bool AcceptWindows1252);
    private static readonly IPlaylistFormat M3u = new M3UParser();
    private static readonly Dictionary<string, FormatDefinition> Formats = new(StringComparer.OrdinalIgnoreCase)
    {
        [".m3u"] = new(M3u, "M3U Playlist", AcceptWindows1252: true),
        [".m3u8"] = new(M3u, "M3U8 Playlist", AcceptWindows1252: false),
        [".pls"] = new(new PLSParser(), "PLS Playlist", AcceptWindows1252: true)
    };

    /// <summary>Native file-picker filters derived from the registered formats.</summary>
    public static string[] FileFilters => [.. Formats.Select(format => $"*{format.Key}; {format.Value.DisplayName}")];

    /// <summary>Recognizes supported playlist extensions without inspecting the file.</summary>
    /// <param name="path">Candidate playlist path.</param>
    /// <returns>Whether a format parser is available.</returns>
    public static bool Supports(string path) => Formats.ContainsKey(Path.GetExtension(path));

    /// <summary>Reads ordered occurrences, resolving relative paths against the playlist directory.</summary>
    /// <param name="path">Local playlist path.</param>
    /// <param name="cancellation">Cancels between parsed entries.</param>
    /// <returns>Ordered entries with optional titles and durations, retaining duplicates.</returns>
    public static PlaylistEntry[] Read(string path, CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();
        FormatDefinition format = RequireFormat(path);
        string text = ReadText(path, format);
        cancellation.ThrowIfCancellationRequested();
        PlaylistEntry[] entries = format.Parser.Parse(text, cancellation);
        return [.. entries.Select(entry =>
        {
            cancellation.ThrowIfCancellationRequested();
            return entry with { Path = ResolvePath(path, entry.Path) };
        })];
    }

    private static FormatDefinition RequireFormat(string path) => Formats.TryGetValue(Path.GetExtension(path), out var format)
        ? format : throw new InvalidDataException("Unsupported playlist extension: " + Path.GetExtension(path));

    private static string ReadText(string path, FormatDefinition format)
    {
        try
        {
            return File.ReadAllText(path, new UTF8Encoding(false, true));
        }
        catch (DecoderFallbackException) when (format.AcceptWindows1252)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return File.ReadAllText(path, Encoding.GetEncoding(WindowsCodePage));
        }
    }

    private static string ResolvePath(string playlist, string source)
    {
        if (source.StartsWith("file://", StringComparison.OrdinalIgnoreCase) && Uri.TryCreate(source, UriKind.Absolute, out var uri))
            return uri.LocalPath;
        if (source.Contains("://", StringComparison.Ordinal) || source.Length > 2 && char.IsAsciiLetter(source[0]) && source[1] == ':')
            return source;
        string normalized = source.Replace('\\', Path.DirectorySeparatorChar);
        return Path.GetFullPath(normalized, Path.GetDirectoryName(Path.GetFullPath(playlist))!);
    }

    /// <summary>Writes a UTF-8 playlist and replaces the destination only after writing succeeds.</summary>
    /// <param name="path">Destination whose extension selects the format.</param>
    /// <param name="entries">Occurrences to save, including duplicates and display metadata.</param>
    /// <param name="relativePaths">Whether local paths should be relative to the playlist directory.</param>
    public static void Write(string path, IEnumerable<PlaylistEntry> entries, bool relativePaths = true)
    {
        FormatDefinition format = RequireFormat(path);
        string directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        var prepared = entries.Select(entry => entry with
        {
            Path = PreparePath(directory, entry.Path, relativePaths)
        });
        string temporary = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var writer = new StreamWriter(temporary, false, new UTF8Encoding(false)))
            {
                format.Parser.Write(writer, prepared);
            }
            File.Move(temporary, path, true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static string PreparePath(string directory, string source, bool relative)
    {
        if (source.Contains('\n') || source.Contains('\r'))
            throw new InvalidDataException("Playlist paths cannot contain line breaks.");
        return relative && !source.Contains("://", StringComparison.Ordinal) ? Path.GetRelativePath(directory, source) : source;
    }
}
