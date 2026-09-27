using Godot;

namespace GodAmp.Controls.Playlist;

/// <summary>Transfers queue occurrence IDs only between rows in the same playlist container.</summary>
public partial class PlaylistDragData : RefCounted
{
    public ulong ContainerId { get; init; }
    public long[] EntryIds { get; init; } = [];
}
