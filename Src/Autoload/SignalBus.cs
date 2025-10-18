using Godot;

namespace GodAmp.Autoload;

public partial class SignalBus : Node
{
	[Signal] public delegate void NextTrackRequestedEventHandler();
	[Signal] public delegate void PreviousTrackRequestedEventHandler();
	[Signal] public delegate void ShuffleModeRequestedEventHandler();
	[Signal] public delegate void RepeatModeRequestedEventHandler();
	[Signal] public delegate void ChangeToTrackRequestedEventHandler(int index);
	[Signal] public delegate void LoadTracksRequestedEventHandler();
	
	// For Master Label
	[Signal] public delegate void LockMasterLabelEventHandler(bool byPositionSeeker=false);
	[Signal] public delegate void UnlockMasterLabelEventHandler();
	[Signal] public delegate void VolumeChangedEventHandler(float volume);
	[Signal] public delegate void PannerBalanceChangedEventHandler(float value);
	[Signal] public delegate void PositionSeekerChangedEventHandler(float value);

	public static SignalBus Instance { get; private set; }

	public override void _EnterTree(){
		if (Instance != null)
		{
			QueueFree();
		}
		Instance = this;
	}
}