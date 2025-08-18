using GodAmp.Utils;
using Godot;

namespace GodAmp.Components;

public partial class DraggablePanel : Control
{
	private bool _following = false;

	private Vector2I _mouseOffset;

	public override void _Ready()
	{
		GuiInput += OnGuiInput;
	}

	private void OnGuiInput(InputEvent inputEvent)
	{
		if (inputEvent is not InputEventMouseButton { ButtonIndex: MouseButton.Left }) return;
		_following = !_following;
		MouseDefaultCursorShape = _following ? CursorShape.Move : CursorShape.Arrow;
		_mouseOffset = GetTree().Root.GetMousePosition().ToVector2I();
	}

	public override void _Process(double delta)
	{
		if (!_following) return;

		GetTree().Root.Position = DisplayServer.MouseGetPosition() - _mouseOffset;
	}
}