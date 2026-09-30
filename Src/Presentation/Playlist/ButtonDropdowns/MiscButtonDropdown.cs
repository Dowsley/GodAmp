using GodAmp.Playback.Queue;
using Godot;

namespace GodAmp.Presentation.Playlist.ButtonDropdowns;

/// <summary>Exposes playlist ordering commands from the scene-authored menu.</summary>
public partial class MiscButtonDropdown : ButtonDropdown
{
    [Signal] public delegate void OrderRequestedEventHandler(PlaylistOrder order);
    [Signal] public delegate void FileInfoRequestedEventHandler();
    [Signal] public delegate void RefreshMetadataRequestedEventHandler();
    [Signal] public delegate void JumpToTrackRequestedEventHandler();
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
    private enum OptionsCommand { RefreshMetadata = 0, JumpToTrack = 1 }

    private void OnOptionsItemPressed(long id)
    {
        switch ((OptionsCommand)id)
        {
            case OptionsCommand.RefreshMetadata: EmitSignal(SignalName.RefreshMetadataRequested); break;
            case OptionsCommand.JumpToTrack: EmitSignal(SignalName.JumpToTrackRequested); break;
        }
    }
}
