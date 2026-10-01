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

    /// <summary>Runs simulation and rendering only while the window is visible and playback is active.</summary>
    public void RefreshPlaybackState()
    {
        if (!IsNodeReady())
            return;
        _audioVisualizer.SetActive(WindowRef.Visible && IsVisibleInTree() && _playback.State == PlaybackState.Playing);
    }
}
