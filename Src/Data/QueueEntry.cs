namespace GodAmp.Data;

/// <summary>Identifies one occurrence of a track independently of its queue position.</summary>
/// <param name="Id">Identity allocated by the owning playback controller.</param>
/// <param name="Track">Audio and metadata shared by this occurrence.</param>
public sealed record QueueEntry(long Id, Track Track);
