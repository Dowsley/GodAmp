using GodAmp.Autoload;
using GodAmp.Utils;
using Godot;

namespace GodAmp.Controls.Playlist;

public partial class PlaylistTrackEntry : PanelContainer
{
    [Signal] public delegate void SelectedEventHandler(int index);
    
    [Export] private Label _trackTitleLabel;
    [Export] private Label _durationLabel;
    [Export] private ColorRect _selectedBg;
    
    public int Index;

    private bool _isSelected = false;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            _isSelected = value;
            _selectedBg.Visible = _isSelected;
            if (_isSelected)
                EmitSignal(SignalName.Selected, Index);
        }
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
    }
    
    public void Setup(string title, float duration, int index, bool selected, bool current = false)
    {
        _isSelected = selected;
        _selectedBg.Visible = _isSelected;
        
        Index = index;
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

        if (eventMouseButton.Pressed && eventMouseButton.ButtonIndex == MouseButton.Left) 
        {
            if (eventMouseButton.DoubleClick)
                SignalBus.Instance.EmitSignal(SignalBus.SignalName.ChangeToTrackRequested, Index);
            else
                IsSelected = !IsSelected;
        }
    }
}