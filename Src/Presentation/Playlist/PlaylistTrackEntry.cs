using System.Linq;
using GodAmp.Skins;
using GodAmp.Presentation.Formatting;
using Godot;

namespace GodAmp.Presentation.Playlist;

/// <summary>Displays one queue occurrence and forwards row selection, activation and drag requests.</summary>
public partial class PlaylistTrackEntry : PanelContainer
{
    [Signal] public delegate void SelectionRequestedEventHandler(long entryId, bool range, bool toggle);
    [Signal] public delegate void ContextRequestedEventHandler(long entryId);
    [Signal] public delegate void KeyboardRequestedEventHandler(long entryId, InputEventKey key);
    [Signal] public delegate void ActivatedEventHandler(long entryId);
    [Signal] public delegate void MoveRequestedEventHandler(long[] entryIds, long targetId, bool insertAfter);

    [Export] private Label _trackTitleLabel = null!;
    [Export] private Label _durationLabel = null!;
    [Export] private ColorRect _selectedBg = null!;

    public long EntryId { get; private set; }

    private bool _isSelected = false;
    private bool _isPointerDown = false;
    private bool _dragStarted = false;
    private bool _isCurrentTrack;
    private float? _duration;
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
        SkinLoader.Instance.SkinChanged += ApplySkin;
        ApplySkin();
    }

    /// <inheritdoc />
    public override void _ExitTree() => SkinLoader.Instance.SkinChanged -= ApplySkin;

    /// <summary>Populates the row and applies its selection and playback styling.</summary>
    /// <param name="title">Track title as displayed, preserving its letter case.</param>
    /// <param name="duration">Track length in seconds.</param>
    /// <param name="entryId">Identity of this queue occurrence.</param>
    /// <param name="selected">Whether the row is selected.</param>
    /// <param name="current">Whether the row represents the playing track.</param>
    public void Setup(string title, float duration, long entryId, bool selected, bool current = false)
    {
        IsSelected = selected;

        EntryId = entryId;
        UpdateMetadata(title, duration);
        _isCurrentTrack = current;
        ApplySkin();
    }

    /// <summary>Changes display metadata without resetting row interaction or skin state.</summary>
    /// <param name="title">Formatted title in queue order.</param>
    /// <param name="duration">Track length in seconds.</param>
    public void UpdateMetadata(string title, float duration)
    {
        if (_trackTitleLabel.Text != title)
            _trackTitleLabel.Text = title;
        if (_duration == duration)
            return;
        _duration = duration;
        _durationLabel.Text = TrackTime.Format(duration);
    }

    /// <summary>Changes playback highlighting independently of row selection.</summary>
    /// <param name="current">Whether this occurrence is selected for playback.</param>
    public void SetCurrent(bool current)
    {
        if (_isCurrentTrack == current)
            return;
        _isCurrentTrack = current;
        ApplyPlaybackColor();
    }

    /// <summary>Updates both labels and the selection background from the active playlist style.</summary>
    private void ApplySkin()
    {
        var skin = SkinLoader.Instance;
        _selectedBg.Color = skin.PlaylistStyle.SelectedBackground;
        foreach (var label in new[] { _trackTitleLabel, _durationLabel })
        {
            label.AddThemeFontOverride("font", skin.PlaylistFont);
        }
        ApplyPlaybackColor();
    }

    private void ApplyPlaybackColor()
    {
        var style = SkinLoader.Instance.PlaylistStyle;
        Color color = _isCurrentTrack ? style.Current : style.Normal;
        _trackTitleLabel.AddThemeColorOverride("font_color", color);
        _durationLabel.AddThemeColorOverride("font_color", color);
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true } key && IsPlaylistKey(key))
        {
            EmitSignal(SignalName.KeyboardRequested, EntryId, key);
            AcceptEvent();
            return;
        }
        if (@event is InputEventMouseButton eventMouseButton)
        {
            if (eventMouseButton.ButtonIndex == MouseButton.Right && eventMouseButton.Pressed)
            {
                GrabFocus();
                EmitSignal(SignalName.ContextRequested, EntryId);
                AcceptEvent();
                return;
            }
            if (eventMouseButton.ButtonIndex != MouseButton.Left)
                return;

            if (eventMouseButton.Pressed)
            {
                GrabFocus();
                _isPointerDown = true;
                _dragStarted = false;

                if (eventMouseButton.DoubleClick)
                {
                    EmitSignal(SignalName.SelectionRequested, EntryId, false, false);
                    EmitSignal(SignalName.Activated, EntryId);
                }
            }
            else
            {
                if (_isPointerDown && !_dragStarted)
                    EmitSignal(SignalName.SelectionRequested, EntryId, eventMouseButton.ShiftPressed, eventMouseButton.IsCommandOrControlPressed());

                _isPointerDown = false;
                _dragStarted = false;
            }
        }
    }

    private static bool IsPlaylistKey(InputEventKey key) => key.Keycode is
        Key.Up or Key.Down or Key.Home or Key.End or Key.Pageup or Key.Pagedown or
        Key.Enter or Key.KpEnter or Key.Delete or Key.Space or Key.Menu or Key.F5 ||
        key.Keycode == Key.F10 && key.ShiftPressed || key.Keycode == Key.Key3 && key.AltPressed ||
        key.IsCommandOrControlPressed() && key.Keycode is Key.A or Key.I;

    public override Variant _GetDragData(Vector2 atPosition)
    {
        _dragStarted = true;
        var parent = GetParent();
        long[] selectedIds = IsSelected
            ? [.. parent.GetChildren().OfType<PlaylistTrackEntry>().Where(row => row.IsSelected).Select(row => row.EntryId)]
            : [EntryId];
        var data = new PlaylistDragData
        {
            ContainerId = parent.GetInstanceId(),
            EntryIds = selectedIds
        };

        var preview = new Label { Text = $"Move {selectedIds.Length} track(s)" };
        SetDragPreview(preview);
        return data;
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        return data.VariantType == Variant.Type.Object && data.AsGodotObject() is PlaylistDragData drag &&
            drag.ContainerId == GetParent().GetInstanceId();
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (!_CanDropData(atPosition, data))
            return;
        var drag = (PlaylistDragData)data.AsGodotObject();
        bool insertAfter = atPosition.Y > Size.Y * 0.5f;
        EmitSignal(SignalName.MoveRequested, drag.EntryIds, EntryId, insertAfter);
    }
}
