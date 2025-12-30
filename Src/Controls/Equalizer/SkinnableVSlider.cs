using System;
using Godot;

namespace GodAmp.Controls.Equalizer;

public partial class SkinnableVSlider : VSlider
{
    [Export] protected int TextureX = 13;
    [Export] protected int TextureY1 = 164;
    [Export] protected int TextureY2 = 229;
    [Export] protected int TextureStep = 15;
    [Export] protected int FrameCount = 27;
    [Export] protected int FramesPerRow = 14;
    [Export] protected int TextureWidth = 14;
    [Export] protected int TextureHeight = 63;

    private AtlasTexture _atlasTexture = null!;

    public override void _Ready()
    {
        var styleBox = GetThemeStylebox("slider") as StyleBoxTexture;
        _atlasTexture = styleBox?.Texture as AtlasTexture ?? throw new Exception("StyleBox shouldn't be null");

        ValueChanged += OnValueChanged;
        OnValueChanged(Value);
    }

    private void OnValueChanged(double value)
    {
        float normalized = (float)((value - MinValue) / (MaxValue - MinValue));
        int frame = Mathf.FloorToInt(normalized * FrameCount);

        int x, y;
        if (frame < FramesPerRow)
        {
            x = TextureX + frame * TextureStep;
            y = TextureY1;
        }
        else
        {
            x = TextureX + (frame - FramesPerRow) * TextureStep;
            y = TextureY2;
        }

        _atlasTexture.Region = new Rect2(x, y, TextureWidth, TextureHeight);
    }
}