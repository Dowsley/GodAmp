using System;
using System.Collections.Generic;
using Godot;

namespace GodAmp.Presentation.Components;

/// <summary>Defines skin artwork and the shared geometry used to draw and interact with a slider.</summary>
[Tool, GlobalClass]
public partial class SkinSliderStyle : Resource
{
    /// <summary>Pixel rounding and frame selection rules for normalized slider values.</summary>
    public enum ValueMapping { Linear, Centered, QuantizedInverted }

    /// <summary>First track frame; omitted for sliders whose background belongs to the panel.</summary>
    [ExportGroup("Track")]
    [Export] public AtlasTexture? Track { get => _track; set => SetField(ref _track, value); }
    /// <summary>Total number of track animation frames in the sheet.</summary>
    [Export(PropertyHint.Range, "1,256")] public int TrackFrameCount { get => _trackFrameCount; set => SetField(ref _trackFrameCount, value); }
    /// <summary>Number of frames in each row of the track animation.</summary>
    [Export(PropertyHint.Range, "1,256")] public int TrackColumns { get => _trackColumns; set => SetField(ref _trackColumns, value); }
    /// <summary>Horizontal column spacing and vertical row spacing in source pixels.</summary>
    [Export] public Vector2 TrackFrameStride { get => _trackFrameStride; set => SetField(ref _trackFrameStride, value); }

    /// <summary>First normal thumb frame; its region size defines the pointer hitbox.</summary>
    [ExportGroup("Thumb")]
    [Export] public AtlasTexture Thumb { get => _thumb; set => SetField(ref _thumb, value); }
    /// <summary>Optional pressed artwork using the same frame arrangement as the normal thumb.</summary>
    [Export] public AtlasTexture? PressedThumb { get => _pressedThumb; set => SetField(ref _pressedThumb, value); }
    /// <summary>Number of thumb variants selected by value or position thresholds.</summary>
    [Export(PropertyHint.Range, "1,256")] public int ThumbFrameCount { get => _thumbFrameCount; set => SetField(ref _thumbFrameCount, value); }
    /// <summary>Source-pixel offset between successive thumb variants.</summary>
    [Export] public Vector2 ThumbFrameStride { get => _thumbFrameStride; set => SetField(ref _thumbFrameStride, value); }
    /// <summary>Optional local-axis positions at which the thumb changes to the next frame.</summary>
    [Export] public float[] ThumbPositionThresholds { get => _thumbPositionThresholds; set => SetField(ref _thumbPositionThresholds, value); }
    /// <summary>Hides the thumb while seeking is unavailable.</summary>
    [Export] public bool HideThumbWhenDisabled { get => _hideThumbWhenDisabled; set => SetField(ref _hideThumbWhenDisabled, value); }

    /// <summary>Local position of the thumb at the beginning of its travel.</summary>
    [ExportGroup("Geometry")]
    [Export] public Vector2 ThumbOrigin { get => _thumbOrigin; set => SetField(ref _thumbOrigin, value); }
    /// <summary>Distance between the thumb's endpoint positions in logical pixels.</summary>
    [Export(PropertyHint.Range, "1,1024")] public int Travel { get => _travel; set => SetField(ref _travel, value); }
    /// <summary>Uses the Y axis for thumb movement and pointer input.</summary>
    [Export] public bool Vertical { get => _vertical; set => SetField(ref _vertical, value); }
    /// <summary>Controls animation selection and thumb-position rounding.</summary>
    [Export] public ValueMapping Mapping { get => _mapping; set => SetField(ref _mapping, value); }
    /// <summary>Number of discrete artwork positions for quantized inverted mapping.</summary>
    [Export(PropertyHint.Range, "2,256")] public int PositionCount { get => _positionCount; set => SetField(ref _positionCount, value); }

    private AtlasTexture? _track;
    private int _trackFrameCount = 1;
    private int _trackColumns = 1;
    private Vector2 _trackFrameStride;
    private AtlasTexture _thumb = null!;
    private AtlasTexture? _pressedThumb;
    private int _thumbFrameCount = 1;
    private Vector2 _thumbFrameStride;
    private float[] _thumbPositionThresholds = [];
    private bool _hideThumbWhenDisabled;
    private Vector2 _thumbOrigin;
    private int _travel = 1;
    private bool _vertical;
    private ValueMapping _mapping;
    private int _positionCount = 2;

    /// <summary>Notifies resource consumers when an authored property changes.</summary>
    /// <param name="field">Backing storage for the exported property.</param>
    /// <param name="value">Replacement property value.</param>
    private void SetField<T>(ref T field, T value)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;
        field = value;
        EmitChanged();
    }

    /// <summary>Gets the thumb's local rectangle, including format-specific pixel rounding.</summary>
    /// <param name="ratio">Normalized slider value from zero to one.</param>
    /// <returns>The rectangle shared by drawing and pointer hit testing.</returns>
    public Rect2 GetThumbRect(double ratio)
    {
        int offset = Mapping switch
        {
            ValueMapping.Centered => Travel / 2 + (int)((ratio * 2 - 1) * (Travel / 2)),
            ValueMapping.QuantizedInverted => Travel - (PositionCount - 1 - QuantizedPosition(ratio)) * (Travel + 1) / PositionCount,
            _ => Mathf.FloorToInt((float)ratio * Travel)
        };
        Vector2 position = ThumbOrigin + (Vertical ? new Vector2(0, offset) : new Vector2(offset, 0));
        return new Rect2(position, Thumb.Region.Size);
    }

    /// <summary>Selects the track animation frame from the normalized value.</summary>
    /// <param name="ratio">Normalized slider value from zero to one.</param>
    /// <returns>A source rectangle in the track's backing sheet, or an empty rectangle without a track.</returns>
    public Rect2 GetTrackRegion(double ratio)
    {
        if (Track == null)
            return default;
        int lastFrame = TrackFrameCount - 1;
        int frame = Mapping switch
        {
            ValueMapping.QuantizedInverted => lastFrame - QuantizedPosition(ratio) * TrackFrameCount / PositionCount,
            ValueMapping.Centered => (int)(Math.Abs(ratio * 2 - 1) * lastFrame),
            _ => (int)(ratio * lastFrame)
        };
        frame = Math.Clamp(frame, 0, lastFrame);
        return new Rect2(Track.Region.Position + new Vector2(frame % TrackColumns, frame / TrackColumns) * TrackFrameStride,
            Track.Region.Size);
    }

    /// <summary>Selects the thumb atlas for its pressed state.</summary>
    /// <param name="pressed">Whether the pointer interaction is active.</param>
    /// <returns>The pressed artwork when provided, otherwise the normal artwork.</returns>
    public AtlasTexture GetThumbTexture(bool pressed) => pressed ? PressedThumb ?? Thumb : Thumb;

    /// <summary>Selects a thumb sprite by normalized value or authored position thresholds.</summary>
    /// <param name="ratio">Normalized slider value from zero to one.</param>
    /// <param name="pressed">Whether the pointer interaction is active.</param>
    /// <returns>A source rectangle in the selected thumb's backing sheet.</returns>
    public Rect2 GetThumbRegion(double ratio, bool pressed)
    {
        int frame;
        if (ThumbPositionThresholds.Length == 0)
            frame = Math.Clamp((int)(ratio * ThumbFrameCount), 0, ThumbFrameCount - 1);
        else
        {
            Vector2 position = GetThumbRect(ratio).Position;
            float axis = Vertical ? position.Y : position.X;
            frame = 0;
            foreach (float threshold in ThumbPositionThresholds)
            {
                if (axis < threshold)
                    break;
                frame++;
            }
            frame = Math.Min(frame, ThumbFrameCount - 1);
        }
        Rect2 region = GetThumbTexture(pressed).Region;
        return new Rect2(region.Position + frame * ThumbFrameStride, region.Size);
    }

    /// <summary>Maps a pointer position to the same axis, origin and travel used by the thumb.</summary>
    /// <param name="position">Pointer coordinates in local logical pixels.</param>
    /// <param name="grabOffset">Distance from the thumb's leading edge to the pointer.</param>
    /// <returns>A normalized slider value clamped to its endpoints.</returns>
    public double GetRatioFromPointer(Vector2 position, float grabOffset)
    {
        Vector2 relative = position - ThumbOrigin;
        float ratio = ((Vertical ? relative.Y : relative.X) - grabOffset) / Travel;
        return Mathf.Clamp(Mapping == ValueMapping.QuantizedInverted ? 1 - ratio : ratio, 0, 1);
    }

    /// <summary>Maps the value onto the discrete inverted positions used by EQ artwork.</summary>
    /// <param name="ratio">Normalized slider value from zero to one.</param>
    /// <returns>The nearest position index, with zero at the maximum value.</returns>
    private int QuantizedPosition(double ratio) => Mathf.RoundToInt((1 - (float)ratio) * (PositionCount - 1));
}
