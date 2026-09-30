using Godot;
using GodAmp.Presentation.Components;
using GodAmp.Playback;

namespace GodAmp.Presentation.Visualizer;

public partial class Visualizer : WindowPanelContainer
{
    [ExportGroup("References")]
    [Export] private PlaybackController _playback = null!;
    [Export] private AudioVisualizer _audioVisualizer = null!;
    [Export] private VisualizerOptionsButton _vizOptsButton = null!;

    public override void _Ready()
    {
        base._Ready();
        RefreshPlaybackState();
        _vizOptsButton.Initialize(_audioVisualizer.StrategyTypeMap.Values);
    }

    /// <summary>Runs visualization rendering while the playback owner reports active playback.</summary>
    public void RefreshPlaybackState()
    {
        _audioVisualizer.ProcessMode = _playback.State == PlaybackState.Playing
            ? ProcessModeEnum.Inherit : ProcessModeEnum.Disabled;
    }
}
