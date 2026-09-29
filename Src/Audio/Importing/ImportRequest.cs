namespace GodAmp.Audio.Importing;

public enum ImportSource { Files, Folder, Playlist }
public enum ImportMode { Append, Replace, RefreshMetadata }

/// <summary>A batch to prepare before applying one queue mutation.</summary>
public sealed record ImportRequest(ImportSource Source, string[] Paths, ImportMode Mode)
{
    /// <summary>Occurrence identities eligible for a metadata refresh; stale identities are ignored.</summary>
    public long[] EntryIds { get; init; } = [];
}
