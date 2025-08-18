using Godot;

namespace GodAmp.Components;

public partial class WindowPanelContainer : PanelContainer
{
	[Signal] public delegate void CloseButtonClickedEventHandler();
	
	[Export] public Control Contents;

	private bool _minimized = false;
	private bool _closed = false;
	
	public virtual void OnCloseButtonPressed()
	{
		_closed = !_closed;
		Visible = !Visible;
		EmitSignal(SignalName.CloseButtonClicked);
	}

	public virtual void OnMinimizeButtonPressed()
	{
		_minimized = !_minimized;
		Contents.Visible = !Contents.Visible;
	}
}