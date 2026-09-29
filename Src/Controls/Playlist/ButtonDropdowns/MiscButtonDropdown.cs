using GodAmp.Audio.Playback;
using Godot;

namespace GodAmp.Controls.Playlist.ButtonDropdowns;

/// <summary>Exposes playlist ordering commands from the scene-authored menu.</summary>
public partial class MiscButtonDropdown : ButtonDropdown
{
    [Signal] public delegate void OrderRequestedEventHandler(PlaylistOrder order);
    [Signal] public delegate void FileInfoRequestedEventHandler();
    [Signal] public delegate void RefreshMetadataRequestedEventHandler();
    [Export] private PopupMenu _sortMenu = null!;
    [Export] private PopupMenu _optionsMenu = null!;

    private void OnSortListButtonPressed() => OpenMenu(_sortMenu);

    private void OnSortItemPressed(long id) => EmitSignal(SignalName.OrderRequested, id);
    private void OnFileInfoButtonPressed()
    {
        Disable();
        EmitSignal(SignalName.FileInfoRequested);
    }
    private void OnMiscOptsButtonPressed() => OpenMenu(_optionsMenu);
    private void OnOptionsItemPressed(long _) => EmitSignal(SignalName.RefreshMetadataRequested);
}
