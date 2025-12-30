using Godot;

namespace GodAmp.Controls.Playlist;

public partial class ButtonDropdown : Node2D
{
    [Export] private FocusManagedVBoxContainer _container = null!;

    public override void _Ready()
    {
        _container.FocusReleased += Disable;
    }

    public void Activate(Rect2 buttonRect)
    {
        var buttonBottomY = buttonRect.Position.Y + buttonRect.Size.Y;
        var containerRect = _container.GetGlobalRect();
        var dropdownTopY = buttonBottomY - containerRect.Size.Y;

        Vector2 dropdownPos = new(buttonRect.Position.X, dropdownTopY);

        GlobalPosition = dropdownPos;
        Show();
    }

    public void Disable()
    {
        Hide();
    }
}