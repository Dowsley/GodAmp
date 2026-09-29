using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using GodAmp.Audio.Playlists;
using GodAmp.Utils;
using Godot;

namespace GodAmp.Audio.Importing;

/// <summary>Expands mixed selections into ordered audio occurrences without following directory links.</summary>
internal static class ImportDiscovery
{
    private const int MaximumPlaylistDepth = 64;

    /// <summary>Expands folders and playlists, collecting per-source failures while retaining readable siblings.</summary>
    /// <param name="request">Selected files, folders or playlists.</param>
    /// <param name="issues">Destination for source discovery failures.</param>
    /// <param name="cancellation">Cancels traversal between directory and playlist entries.</param>
    /// <returns>Ordered occurrences and whether a readable empty playlist was explicitly selected.</returns>
    public static DiscoveryResult Discover(ImportRequest request, List<AudioIssue> issues, CancellationToken cancellation)
    {
        List<PlaylistEntry> entries = [];
        HashSet<string> ancestors = new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        bool hasPlaylist = false;
        bool hasFolder = false;
        bool emptyPlaylist = false;

        void Expand(PlaylistEntry entry, int depth)
        {
            cancellation.ThrowIfCancellationRequested();
            string path = entry.Path;
            try
            {
                if (path.Contains("://", StringComparison.Ordinal) && !path.StartsWith("res://", StringComparison.Ordinal)
                    && !path.StartsWith("user://", StringComparison.Ordinal))
                {
                    issues.Add(new AudioIssue(path, AudioIssueKind.UnsupportedFormat, "Network playback is not supported."));
                    return;
                }
                if (DirAccess.DirExistsAbsolute(path))
                {
                    hasFolder = true;
                    using var directory = DirAccess.Open(path) ?? throw new IOException("Cannot open the audio folder.");
                    foreach (string name in directory.GetFiles().Order(StringComparer.Ordinal))
                    {
                        cancellation.ThrowIfCancellationRequested();
                        if (AudioFormats.Supports(name))
                            entries.Add(new PlaylistEntry(path.TrimEnd('/', '\\') + "/" + name));
                    }
                    foreach (string name in directory.GetDirectories().Order(StringComparer.Ordinal))
                    {
                        cancellation.ThrowIfCancellationRequested();
                        if (!directory.IsLink(name))
                            Expand(new PlaylistEntry(path.TrimEnd('/', '\\') + "/" + name), depth);
                    }
                }
                else if (PlaylistFile.Supports(path))
                {
                    hasPlaylist = true;
                    string canonical = Path.GetFullPath(path);
                    if (depth >= MaximumPlaylistDepth || !ancestors.Add(canonical))
                        throw new InvalidDataException("Playlist nesting contains a cycle or exceeds the supported depth.");
                    try
                    {
                        PlaylistEntry[] nested = PlaylistFile.Read(path, cancellation);
                        emptyPlaylist |= nested.Length == 0;
                        foreach (PlaylistEntry child in nested)
                            Expand(child, depth + 1);
                    }
                    finally { ancestors.Remove(canonical); }
                }
                else
                    entries.Add(entry);
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
            {
                issues.Add(new AudioIssue(path, AudioIssueKind.Discovery, exception.Message));
            }
        }

        foreach (string path in request.Paths)
        {
            cancellation.ThrowIfCancellationRequested();
            if (request.Source == ImportSource.Folder && !DirAccess.DirExistsAbsolute(path))
                issues.Add(new AudioIssue(path, AudioIssueKind.Discovery, "Cannot open the audio folder."));
            else
                Expand(new PlaylistEntry(path), 0);
        }
        return new DiscoveryResult(entries, hasPlaylist || hasFolder,
            emptyPlaylist && entries.Count == 0 && issues.Count == 0 && !hasFolder);
    }
}

internal sealed record DiscoveryResult(IReadOnlyList<PlaylistEntry> Entries, bool PreserveOrder, bool EmptyPlaylist);
