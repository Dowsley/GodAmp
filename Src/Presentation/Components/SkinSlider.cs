using GodAmp.Skins;
using Godot;

namespace GodAmp.Presentation.Components;

/// <summary>Draws classic skin sliders with matching logical-pixel artwork and input geometry.</summary>
[Tool]
public partial class SkinSlider : Godot.Range
{
    /// <summary>Emitted before pointer or keyboard interaction changes the value.</summary>
    [Signal] public delegate void DragStartedEventHandler();
    /// <summary>Emitted on release with whether the interaction changed the value.</summary>
    [Signal] public delegate void DragEndedEventHandler(bool valueChanged);

    /// <summary>Inspector-assigned artwork, animation and interaction geometry.</summary>
    [Export] public SkinSliderStyle Style { get; set; } = null!;
    /// <summary>Controls whether mouse and keyboard input can change the value.</summary>
    [Export] public bool Editable
    {
        get => _editable;
        set
        {
            if (_editable == value)
                return;
            _editable = value;
            if (!value)
                FinishDrag();
            QueueRedraw();
        }
    }

    private bool _editable = true;
    private bool _dragging;
    private double _valueBeforeDrag;
    private float _grabOffset;

    /// <summary>Gets the thumb's logical rectangle, shared by rendering and pointer hit testing.</summary>
    public Rect2 ThumbRect => Style.GetThumbRect(Ratio);

    /// <summary>Gets the source track rectangle for the current value.</summary>
    public Rect2 TrackRegion => Style.GetTrackRegion(Ratio);

    /// <inheritdoc />
    public override void _Ready()
    {
        if (Engine.IsEditorHint())
            return;
        /* The hosting window and autoload live outside this reusable control scene. */
        GetWindow().FocusExited += FinishDrag;
        SkinLoader.Instance.SkinChanged += QueueRedraw;
    }

    /// <inheritdoc />
    public override void _ExitTree()
    {
        if (Engine.IsEditorHint())
            return;
        GetWindow().FocusExited -= FinishDrag;
        SkinLoader.Instance.SkinChanged -= QueueRedraw;
    }

    /// <summary>Invalidates artwork after a range value change.</summary>
    /// <param name="_">Updated value supplied by the scene's Range signal.</param>
    private void OnValueChanged(double _) => QueueRedraw();

    /// <inheritdoc />
    public override void _Draw()
    {
        if (Style?.Thumb?.Atlas == null)
            return;
        Rect2 track = TrackRegion;
        if (Style.Track != null)
            DrawTextureRectRegion(Style.Track.Atlas, new Rect2(Vector2.Zero, track.Size), track);
        if (!Editable && Style.HideThumbWhenDisabled)
            return;
        DrawTextureRectRegion(Style.GetThumbTexture(_dragging).Atlas, ThumbRect, Style.GetThumbRegion(Ratio, _dragging));
    }

    /// <inheritdoc />
    public override void _GuiInput(InputEvent input)
    {
        if (!Editable || Style == null)
            return;
        if (input is InputEventMouseButton button)
        {
            if (button.ButtonIndex == MouseButton.Left)
            {
                if (button.Pressed)
                {
                    GrabFocus();
                    BeginDrag();
                    Rect2 thumb = ThumbRect;
                    Vector2 offset = button.Position - thumb.Position;
                    _grabOffset = thumb.HasPoint(button.Position)
                        ? (Style.Vertical ? offset.Y : offset.X)
                        : (Style.Vertical ? thumb.Size.Y : thumb.Size.X) / 2;
                    if (!thumb.HasPoint(button.Position))
                        SetValueFromPointer(button.Position);
                }
                else
                    FinishDrag();
                AcceptEvent();
            }
            else if (button.Pressed && button.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
            {
                AdjustValue(button.ButtonIndex == MouseButton.WheelUp ? 1 : -1);
                AcceptEvent();
            }
        }
        else if (input is InputEventMouseMotion motion && _dragging)
        {
            SetValueFromPointer(motion.Position);
            AcceptEvent();
        }
        else if (input.IsActionPressed("ui_right") || input.IsActionPressed("ui_up"))
        {
            AdjustValue(1);
            AcceptEvent();
        }
        else if (input.IsActionPressed("ui_left") || input.IsActionPressed("ui_down"))
        {
            AdjustValue(-1);
            AcceptEvent();
        }
    }

    /// <summary>Maps a local pointer position onto the same travel used to draw the thumb.</summary>
    /// <param name="position">Pointer coordinates in logical control pixels.</param>
    private void SetValueFromPointer(Vector2 position)
    {
        Ratio = Style.GetRatioFromPointer(position, _grabOffset);
    }

    /// <summary>Applies one wheel or keyboard step and emits a complete interaction for seek handlers.</summary>
    /// <param name="direction">Positive to increase the value, negative to decrease it.</param>
    private void AdjustValue(int direction)
    {
        BeginDrag();
        Value += direction * (Step > 0 ? Step : (MaxValue - MinValue) / 100);
        FinishDrag();
    }

    /// <summary>Records the initial value and starts a single interaction.</summary>
    private void BeginDrag()
    {
        if (_dragging)
            return;
        _valueBeforeDrag = Value;
        _dragging = true;
        EmitSignal(SignalName.DragStarted);
        QueueRedraw();
    }

    /// <summary>Ends an interaction and restores the released thumb artwork.</summary>
    private void FinishDrag()
    {
        if (!_dragging)
            return;
        _dragging = false;
        EmitSignal(SignalName.DragEnded, !Mathf.IsEqualApprox(Value, _valueBeforeDrag));
        QueueRedraw();
    }
}
