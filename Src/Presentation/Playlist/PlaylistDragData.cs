using Godot;

namespace GodAmp.Presentation.Playlist;

/// <summary>Identifies the playlist owner and queue occurrences participating in a drag.</summary>
public partial class PlaylistDragData : RefCounted
{
    public ulong PlaylistId { get; init; }
    public long[] EntryIds { get; init; } = [];
}
