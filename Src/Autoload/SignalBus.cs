using Godot;

namespace GodAmp.Autoload;

/* DO NOT ADD MORE SIGNALS HERE. Migrate existing signals to their owning components,
 * prefer scene-authored connections, and remove this bus once all callers are migrated. */
public partial class SignalBus : Node
{
    [Signal] public delegate void LoadTracksRequestedEventHandler(bool overridePlaylist = false);
    [Signal] public delegate void LoadTracksFromDirRequestedEventHandler(bool overridePlaylist = false);
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

    public static SignalBus Instance { get; private set; } = null!;

    public override void _EnterTree()
    {
        Instance = this;
    }
}
