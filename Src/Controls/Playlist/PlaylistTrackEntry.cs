using GodAmp.Autoload;
using GodAmp.Utils;
using Godot;

namespace GodAmp.Controls.Playlist;

public partial class PlaylistTrackEntry : PanelContainer
{
    private const int PlaylistFontSize = 10;
    [Signal] public delegate void SelectedEventHandler(int index);

    [Export] private Label _trackTitleLabel = null!;
    [Export] private Label _durationLabel = null!;
    [Export] private ColorRect _selectedBg = null!;

    public int Index;

    private bool _isSelected = false;
    private bool _isPointerDown = false;
    private bool _dragStarted = false;
    private bool _isCurrentTrack;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            _isSelected = value;
            _selectedBg.Visible = _isSelected;
        }
    }

    /// <inheritdoc />
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        SignalBus.Instance.SkinChanged += ApplySkin;
        ApplySkin();
    }

    /// <inheritdoc />
    public override void _ExitTree() => SignalBus.Instance.SkinChanged -= ApplySkin;

    /// <summary>Populates the row and applies its selection and playback styling.</summary>
    /// <param name="title">Track title as displayed, preserving its letter case.</param>
    /// <param name="duration">Track length in seconds.</param>
    /// <param name="index">Zero-based position in the playlist.</param>
    /// <param name="selected">Whether the row is selected.</param>
    /// <param name="current">Whether the row represents the playing track.</param>
    public void Setup(string title, float duration, int index, bool selected, bool current = false)
    {
        IsSelected = selected;

        Index = index;
        _trackTitleLabel.Text = title;
        _durationLabel.Text = TimeUtils.FormatAsTrackTime(duration);
        _isCurrentTrack = current;
        ApplySkin();
    }

    /// <summary>Updates both labels and the selection background from the active playlist style.</summary>
    private void ApplySkin()
    {
        var skin = SkinLoader.Instance;
        _selectedBg.Color = skin.PlaylistStyle.SelectedBackground;
        foreach (var label in new[] { _trackTitleLabel, _durationLabel })
        {
            label.Material = null;
            label.TextureFilter = TextureFilterEnum.Linear;
            label.AddThemeFontOverride("font", skin.PlaylistFont);
            label.AddThemeFontSizeOverride("font_size", PlaylistFontSize);
            label.AddThemeColorOverride("font_color", _isCurrentTrack ? skin.PlaylistStyle.Current : skin.PlaylistStyle.Normal);
            label.AddThemeColorOverride("font_shadow_color", Colors.Transparent);
        }
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

        Node? ancestor = GetParent();
        Playlist? playlist = null;
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
