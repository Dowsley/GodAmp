using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using GodAmp.Utils;

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
        DiscoveryResult discovered = ImportDiscovery.Discover(request, issues, cancellation);

        for (int index = 0; index < discovered.Entries.Count; index++)
        {
            cancellation.ThrowIfCancellationRequested();
            var entry = discovered.Entries[index];
            string path = entry.Path;
            progress(new ImportProgress(index, discovered.Entries.Count, path));
            if (!AudioFormats.Supports(path))
            {
                issues.Add(new AudioIssue(path, AudioIssueKind.UnsupportedFormat, "Supported audio formats are MP3, WAV, and OGG."));
                continue;
            }
            try
            {
                ImportedTrack track = TrackMetadataReader.Read(path, cancellation, out AudioIssue? warning);
                tracks.Add(track with
                {
                    PlaylistTitle = entry.Title ?? "",
                    Duration = track.Duration > 0 ? track.Duration : entry.Duration ?? 0
                });
                if (warning != null)
                    issues.Add(warning);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
            {
                issues.Add(new AudioIssue(path, AudioIssueKind.Access, exception.Message));
            }
        }
        cancellation.ThrowIfCancellationRequested();
        progress(new ImportProgress(discovered.Entries.Count, discovered.Entries.Count, ""));
        if (!discovered.PreserveOrder)
            tracks = [.. tracks.OrderBy(track => track.TrackNumber)];
        return new ImportResult(tracks, issues, discovered.EmptyPlaylist);
    }
}
