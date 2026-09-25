using System;
using GodAmp.Autoload;
using Godot;

namespace GodAmp.Components;

/// <summary>Draws classic skin sliders with matching logical-pixel artwork and input geometry.</summary>
public partial class SkinSlider : Godot.Range
{
    /// <summary>Classic sprite layouts supported by this control.</summary>
    public enum SliderLayout { Volume, Balance, Seek, Equalizer }

    private const int LastFrame = 27;
    private const int FrameStride = 15;
    private const int EqualizerPositions = 64;
    private const int EqualizerFramesPerRow = 14;
    private const int SeekTravel = 219;
    private const int BalanceHalfTravel = 12;
    private const int VolumeTravel = 51;
    private const int EqualizerTravel = 51;

    /// <summary>Emitted before pointer or keyboard interaction changes the value.</summary>
    [Signal] public delegate void DragStartedEventHandler();
    /// <summary>Emitted on release with whether the interaction changed the value.</summary>
    [Signal] public delegate void DragEndedEventHandler(bool valueChanged);

    /// <summary>Selects the sheet layout and logical geometry.</summary>
    [Export] public SliderLayout Layout { get; set; }
    /// <summary>References a shared skin atlas whose backing sheet follows skin changes.</summary>
    [Export] public AtlasTexture Sheet { get; set; } = null!;
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
    public Rect2 ThumbRect => Layout switch
    {
        SliderLayout.Equalizer => new Rect2(1, EqualizerTravel - (EqualizerPositions - 1 - EqualizerPosition) * (EqualizerTravel + 1) / EqualizerPositions, 11, 11),
        SliderLayout.Seek => new Rect2(Mathf.FloorToInt((float)Ratio * SeekTravel), 0, 29, 10),
        SliderLayout.Balance => new Rect2(BalanceHalfTravel + (int)((Ratio * 2 - 1) * BalanceHalfTravel), 1, 14, 11),
        _ => new Rect2(Mathf.FloorToInt((float)Ratio * VolumeTravel), 1, 14, 11)
    };

    private int EqualizerPosition => Mathf.RoundToInt((1 - (float)Ratio) * (EqualizerPositions - 1));

    /// <summary>Gets the source track rectangle for the current value.</summary>
    public Rect2 TrackRegion
    {
        get
        {
            if (Layout == SliderLayout.Seek)
                return new Rect2(0, 0, 248, 10);
            if (Layout == SliderLayout.Equalizer)
            {
                int frame = LastFrame - EqualizerPosition * (LastFrame + 1) / EqualizerPositions;
                return new Rect2(13 + frame % EqualizerFramesPerRow * FrameStride,
                    frame < EqualizerFramesPerRow ? 164 : 229, 14, 63);
            }
            double amount = Layout == SliderLayout.Balance ? Math.Abs(Ratio * 2 - 1) : Ratio;
            int row = Math.Clamp((int)(amount * LastFrame), 0, LastFrame);
            return new Rect2(Layout == SliderLayout.Balance ? 9 : 0, row * FrameStride,
                Layout == SliderLayout.Balance ? 38 : 68, 13);
        }
    }

    /// <inheritdoc />
    public override void _Ready()
    {
        /* The hosting window and autoload live outside this reusable control scene. */
        GetWindow().FocusExited += FinishDrag;
        SignalBus.Instance.SkinChanged += QueueRedraw;
    }

    /// <inheritdoc />
    public override void _ExitTree()
    {
        GetWindow().FocusExited -= FinishDrag;
        SignalBus.Instance.SkinChanged -= QueueRedraw;
    }

    /// <summary>Invalidates artwork after a range value change.</summary>
    /// <param name="_">Updated value supplied by the scene's Range signal.</param>
    private void OnValueChanged(double _) => QueueRedraw();

    /// <inheritdoc />
    public override void _Draw()
    {
        Rect2 track = TrackRegion;
        DrawTextureRectRegion(Sheet.Atlas, new Rect2(Vector2.Zero, track.Size), track);
        if (!Editable && Layout == SliderLayout.Seek)
            return;
        Rect2 source = Layout switch
        {
            SliderLayout.Equalizer => new Rect2(0, _dragging ? 176 : 164, 11, 11),
            SliderLayout.Seek => new Rect2(_dragging ? 278 : 248, 0, 29, 10),
            _ => new Rect2(_dragging ? 0 : 15, 422, 14, 11)
        };
        DrawTextureRectRegion(Sheet.Atlas, ThumbRect, source);
    }

    /// <inheritdoc />
    public override void _GuiInput(InputEvent input)
    {
        if (!Editable)
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
                        ? (Layout == SliderLayout.Equalizer ? offset.Y : offset.X)
                        : (Layout == SliderLayout.Equalizer ? thumb.Size.Y : thumb.Size.X) / 2;
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
        float travel = Layout switch
        {
            SliderLayout.Seek => SeekTravel,
            SliderLayout.Balance => BalanceHalfTravel * 2,
            SliderLayout.Equalizer => EqualizerTravel,
            _ => VolumeTravel
        };
        float ratio = ((Layout == SliderLayout.Equalizer ? position.Y : position.X) - _grabOffset) / travel;
        Ratio = Mathf.Clamp(Layout == SliderLayout.Equalizer ? 1 - ratio : ratio, 0, 1);
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
