using GodAmp.Components;
using Godot;

namespace GodAmp.Visualizer;

public partial class Visualizer : WindowPanelContainer
{
	private AudioVisualizer _audioVisualizer;
	
	public override void _Ready()
	{
		_audioVisualizer = GetNode<AudioVisualizer>("%AudioVisualizer");
		Pause();
	}

	public void Pause()
	{
		_audioVisualizer.ProcessMode = Node.ProcessModeEnum.Disabled;
	}
	
	public void Unpause()
	{
		_audioVisualizer.ProcessMode = Node.ProcessModeEnum.Inherit;
	}
}