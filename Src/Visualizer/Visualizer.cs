using System.Linq;
using Godot;
using GodAmp.Components;

namespace GodAmp.Visualizer;

public partial class Visualizer : WindowPanelContainer
{
    [ExportGroup("References")]
    [Export] private AudioVisualizer _audioVisualizer = null!;
    [Export] private VisualizerOptionsButton _vizOptsButton = null!;

    public override void _Ready()
    {
        base._Ready();
        Pause();
        _vizOptsButton.Initialize(_audioVisualizer.StrategyTypeMap.Values.ToList());
    }

    public void Pause()
    {
        _audioVisualizer.ProcessMode = ProcessModeEnum.Disabled;
    }

    public void Unpause()
    {
        _audioVisualizer.ProcessMode = ProcessModeEnum.Inherit;
    }
}