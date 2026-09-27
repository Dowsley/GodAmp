namespace GodAmp.Audio.Importing;

public enum ImportSource { Files, Folder, Playlist }
public enum ImportMode { Append, Replace }

/// <summary>A batch to prepare before applying one queue mutation.</summary>
public sealed record ImportRequest(ImportSource Source, string[] Paths, ImportMode Mode);
