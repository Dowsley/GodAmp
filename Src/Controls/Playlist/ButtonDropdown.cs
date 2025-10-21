using Godot;

namespace GodAmp.Controls.Playlist;

public partial class ButtonDropdown : Node2D
{
   [Export] private FocusManagedVBoxContainer _container;

   public override void _Ready()
   {
       _container.FocusReleased += Disable;
   }

   public void Activate(Vector2 buttonPos, Vector2 buttonSize)
    {
        Vector2 pos = new(
            buttonPos.X,
            buttonPos.Y - _container.Size.Y + buttonSize.Y
        ); 
        GlobalPosition = pos;
        Show();
    }

    public void Disable()
    {
        Hide();
    }
}