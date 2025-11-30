using GodAmp.Components;

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
		_audioVisualizer.ProcessMode = ProcessModeEnum.Disabled;
	}
	
	public void Unpause()
	{
		_audioVisualizer.ProcessMode = ProcessModeEnum.Inherit;
	}
}