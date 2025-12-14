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
    private bool _isPointerDown = false;
    private bool _dragStarted = false;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            _isSelected = value;
            _selectedBg.Visible = _isSelected;
        }
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        SignalBus.Instance.SkinChanged += _trackTitleLabel.QueueRedraw;
        SignalBus.Instance.SkinChanged += _durationLabel.QueueRedraw;
    }

    public void Setup(string title, float duration, int index, bool selected, bool current = false)
    {
        IsSelected = selected;

        Index = index;
        _trackTitleLabel.Text = title.ToUpper();
        _durationLabel.Text = TimeUtils.FormatAsTrackTime(duration);
        if (!current)
            return;

        _trackTitleLabel.AddThemeColorOverride("font_color", Colors.White);
        _durationLabel.AddThemeColorOverride("font_color", Colors.White);
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton eventMouseButton)
        {
            if (eventMouseButton.ButtonIndex != MouseButton.Left)
                return;

            if (eventMouseButton.Pressed)
            {
                _isPointerDown = true;
                _dragStarted = false;

                if (eventMouseButton.DoubleClick)
                    SignalBus.Instance.EmitSignal(SignalBus.SignalName.ChangeToTrackRequested, Index);
            }
            else
            {
                // Mouse released
                if (_isPointerDown && !_dragStarted)
                    EmitSignal(SignalName.Selected, Index); // plain click -> single select

                _isPointerDown = false;
                _dragStarted = false;
            }
        }
    }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        _dragStarted = true;
        // Build selection indices from siblings' visual state
        var parent = GetParent();
        var selectedIndices = new Godot.Collections.Array<int>();
        foreach (var child in parent.GetChildren())
        {
            if (child is PlaylistTrackEntry { IsSelected: true } entry)
                selectedIndices.Add(entry.Index);
        }

        if (selectedIndices.Count == 0)
            selectedIndices.Add(Index);

        var data = new Godot.Collections.Dictionary
        {
            { "type", "playlist-reorder" },
            { "indices", selectedIndices }
        };

        var preview = new Label { Text = $"Move {selectedIndices.Count} track(s)" };
        SetDragPreview(preview);
        return data;
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        if (data.VariantType != Variant.Type.Dictionary)
            return false;
        var dict = (Godot.Collections.Dictionary)data;
        return dict.ContainsKey("type") && (string)dict["type"] == "playlist-reorder";
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        var dict = (Godot.Collections.Dictionary)data;
        var arr = (Godot.Collections.Array<int>)dict["indices"];
        var indices = new System.Collections.Generic.List<int>(arr);
        bool insertAfter = atPosition.Y > Size.Y * 0.5f;

        // Find ancestor Playlist
        Node ancestor = GetParent();
        Playlist playlist = null;
        while (ancestor != null)
        {
            if (ancestor is Playlist p)
            {
                playlist = p;
                break;
            }
            ancestor = ancestor.GetParent();
        }

        playlist?.ReorderSelectedTracks(indices, Index, insertAfter);
    }
}