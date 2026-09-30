using Godot;

namespace GodAmp.Presentation.Playlist;

/// <summary>Positions a playlist artwork dropdown and opens its scene-authored command menus.</summary>
public partial class ButtonDropdown : Node2D
{
    [Export] private FocusManagedVBoxContainer _container = null!;

    /// <summary>Shows the dropdown aligned to its invoking button's left and bottom edges.</summary>
    /// <param name="buttonRect">Button bounds in the owning canvas.</param>
    public void Activate(Rect2 buttonRect)
    {
        var buttonBottomY = buttonRect.Position.Y + buttonRect.Size.Y;
        var containerRect = _container.GetGlobalRect();
        var dropdownTopY = buttonBottomY - containerRect.Size.Y;

        Vector2 dropdownPos = new(buttonRect.Position.X, dropdownTopY);

        GlobalPosition = dropdownPos;
        Show();
    }

    /// <summary>Hides the artwork dropdown after dismissal or a command.</summary>
    public void Disable()
    {
        Hide();
    }

    /// <summary>Opens a command menu at the pointer within the owning window, then closes the artwork dropdown.</summary>
    /// <param name="menu">Scene-authored popup containing the available commands.</param>
    protected void OpenMenu(PopupMenu menu)
    {
        Viewport viewport = GetViewport();
        Vector2 position = viewport.GetFinalTransform() * viewport.GetMousePosition();
        menu.PopupOnParent(new Rect2I((Vector2I)position, Vector2I.Zero));
        Disable();
    }
}
