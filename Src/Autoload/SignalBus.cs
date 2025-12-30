using Godot;

namespace GodAmp.Autoload;

public partial class SignalBus : Node
{
    [Signal] public delegate void NextTrackRequestedEventHandler();
    [Signal] public delegate void PreviousTrackRequestedEventHandler();
    [Signal] public delegate void ShuffleModeRequestedEventHandler();
    [Signal] public delegate void RepeatModeRequestedEventHandler();
    [Signal] public delegate void ChangeToTrackRequestedEventHandler(int index);
    [Signal] public delegate void LoadTracksRequestedEventHandler(bool overridePlaylist = false);
    [Signal] public delegate void LoadTracksFromDirRequestedEventHandler(bool overridePlaylist = false);
    [Signal] public delegate void RemoveSelectedTracksFromPlaylistRequestedEventHandler();
    [Signal] public delegate void RemoveAllTracksFromPlaylistRequestedEventHandler();
    [Signal] public delegate void CropPlaylistRequestedEventHandler();
    [Signal] public delegate void InverseSelectionRequestedEventHandler();
    [Signal] public delegate void SelectZeroRequestedEventHandler();
    [Signal] public delegate void SelectAllRequestedEventHandler();
    [Signal] public delegate void LoadPlaylistRequestedEventHandler();
    [Signal] public delegate void SavePlaylistRequestedEventHandler();
    [Signal] public delegate void ZoomModeRequestedEventHandler(int multiplier);
    [Signal] public delegate void SkinChangedEventHandler();
    [Signal] public delegate void ToggleEqualizerRequestedEventHandler();
    [Signal] public delegate void TogglePlaylistRequestedEventHandler();
    [Signal] public delegate void ToggleVisualizerRequestedEventHandler();

    // For Master Label
    [Signal] public delegate void LockMasterLabelEventHandler(bool byPositionSeeker = false);
    [Signal] public delegate void UnlockMasterLabelEventHandler();
    [Signal] public delegate void VolumeChangedEventHandler(float volume);
    [Signal] public delegate void PannerBalanceChangedEventHandler(float value);
    [Signal] public delegate void PositionSeekerChangedEventHandler(float value);

    public static SignalBus Instance { get; private set; }

    public override void _EnterTree()
    {
        if (Instance != null)
            QueueFree();
        Instance = this;
    }
}