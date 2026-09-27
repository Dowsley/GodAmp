using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using GodAmp.Utils;
using Godot;

namespace GodAmp.Audio.Importing;

/// <summary>Prepares ordered metadata batches without touching the queue or active scene tree.</summary>
internal static class AudioImporter
{
    /// <summary>Discovers sources and reads metadata without decoding playback streams.</summary>
    /// <param name="request">Source selection whose order and duplicates must be preserved.</param>
    /// <param name="progress">Worker callback reporting completed sources; it must not access scene nodes.</param>
    /// <param name="cancellation">Cancels discovery and metadata reads before a batch can be applied.</param>
    /// <returns>Metadata, per-source issues, and whether an empty playlist was explicitly selected.</returns>
    public static ImportResult Import(ImportRequest request, Action<ImportProgress> progress, CancellationToken cancellation)
    {
        var tracks = new List<ImportedTrack>();
        var issues = new List<AudioIssue>();
        string[] paths;
        try
        {
            cancellation.ThrowIfCancellationRequested();
            paths = request.Source switch
            {
                ImportSource.Files => request.Paths,
                ImportSource.Folder => Discover(request.Paths[0], cancellation),
                ImportSource.Playlist => M3UParser.Parse(request.Paths[0], cancellation),
                _ => throw new ArgumentOutOfRangeException(nameof(request))
            };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            issues.Add(new AudioIssue(request.Paths.FirstOrDefault() ?? "", AudioIssueKind.Discovery, exception.Message));
            return new ImportResult(tracks, issues, false);
        }

        for (int index = 0; index < paths.Length; index++)
        {
            cancellation.ThrowIfCancellationRequested();
            string path = paths[index];
            progress(new ImportProgress(index, paths.Length, path));
            if (!AudioFormats.Supports(path))
            {
                issues.Add(new AudioIssue(path, AudioIssueKind.UnsupportedFormat, "Supported audio formats are MP3, WAV, and OGG."));
                continue;
            }
            try
            {
                tracks.Add(TrackMetadataReader.Read(path, cancellation, out AudioIssue? warning));
                if (warning != null)
                    issues.Add(warning);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
            {
                issues.Add(new AudioIssue(path, AudioIssueKind.Access, exception.Message));
            }
        }
        cancellation.ThrowIfCancellationRequested();
        progress(new ImportProgress(paths.Length, paths.Length, ""));
        if (request.Source != ImportSource.Playlist)
            tracks = [.. tracks.OrderBy(track => track.TrackNumber)];
        return new ImportResult(tracks, issues, request.Source == ImportSource.Playlist && paths.Length == 0);
    }

    private static string[] Discover(string directory, CancellationToken cancellation)
    {
        using var access = DirAccess.Open(directory) ?? throw new IOException("Cannot open the audio folder.");
        var paths = new List<string>();
        foreach (string name in access.GetFiles())
        {
            cancellation.ThrowIfCancellationRequested();
            if (AudioFormats.Supports(name))
                paths.Add(directory.TrimEnd('/', '\\') + "/" + name);
        }
        return [.. paths.Order(StringComparer.Ordinal)];
    }
}
