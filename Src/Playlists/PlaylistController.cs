using System;
using System.IO;
using System.Linq;
using GodAmp.Media.Importing;
using GodAmp.Diagnostics;
using GodAmp.Playback;
using GodAmp.Playlists.Serialization;
using GodAmp.Media;
using Godot;

namespace GodAmp.Playlists;

/// <summary>Coordinates playlist file operations against the scene's playback queue.</summary>
public partial class PlaylistController : Node
{
    private const string DefaultExtension = ".m3u8";
    [Export] private PlaybackController _playback = null!;
    [Export] private AudioImportController _imports = null!;
    [Export] private OperationIssues _issues = null!;

    /// <summary>Imports a playlist before replacing the queue, retaining playback on failure.</summary>
    /// <param name="path">Playlist source selected by the user.</param>
    public void Open(string path) =>
        _imports.Enqueue(new ImportRequest(ImportSource.Playlist, [path], ImportMode.Replace));

    /// <summary>Saves queue order, duplicates and display metadata using the destination's format.</summary>
    /// <param name="path">Destination path; a missing extension selects M3U8.</param>
    public void Save(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(Path.GetExtension(path)))
                path += DefaultExtension;
            var entries = _playback.Entries.Select(entry => new PlaylistEntry(entry.Track.SourcePath,
                TrackTitle.Format(entry.Track), entry.Track.Duration > 0 ? entry.Track.Duration : null));
            PlaylistFile.Write(path, entries);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
        {
            _issues.Report([new OperationIssue(path, OperationIssueKind.Access, exception.Message, OperationKind.PlaylistSave)]);
        }
    }
}
