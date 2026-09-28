using GodAmp.Audio.Playback;
using Godot;

namespace GodAmp.Controls.Playlist.ButtonDropdowns;

/// <summary>Exposes playlist ordering commands from the scene-authored menu.</summary>
public partial class MiscButtonDropdown : ButtonDropdown
{
    [Signal] public delegate void OrderRequestedEventHandler(PlaylistOrder order);
    [Export] private PopupMenu _sortMenu = null!;

    private void OnSortListButtonPressed() => OpenMenu(_sortMenu);

    private void OnSortItemPressed(long id) => EmitSignal(SignalName.OrderRequested, id);
}
