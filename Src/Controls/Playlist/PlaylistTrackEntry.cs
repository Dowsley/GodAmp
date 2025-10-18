using GodAmp.Autoload;
using GodAmp.Utils;
using Godot;

namespace GodAmp.Controls.Playlist;

public partial class PlaylistTrackEntry : HBoxContainer
{
    private Label _trackTitleLabel;
    private Label _durationLabel;
    private int _index;

    public override void _Ready()
    {
        _trackTitleLabel = GetNode<Label>("TrackTitleLabel");
        _durationLabel = GetNode<Label>("DurationLabel");
        MouseFilter = MouseFilterEnum.Stop;
    }
    
    public void Setup(string title, float duration, int index, bool current = false)
    {
        _index = index;
        _trackTitleLabel.Text = title;
        _durationLabel.Text = TimeUtils.FormatAsTrackTime(duration);
        if (!current)
            return;
    
        _trackTitleLabel.AddThemeColorOverride("font_color", Colors.White);
        _durationLabel.AddThemeColorOverride("font_color", Colors.White);
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton eventMouseButton)
            return;

        if (eventMouseButton.Pressed &&
            eventMouseButton.ButtonIndex == MouseButton.Left &&
            eventMouseButton.DoubleClick)
        {
            SignalBus.Instance.EmitSignal(SignalBus.SignalName.ChangeToTrackRequested, _index);
        }
    }
}