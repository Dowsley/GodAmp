using Godot;

namespace GodAmp.Controls.MasterPanel;

public partial class SkinnableHSlider : HSlider
{
    [Export] protected int TextureX = 0;
    [Export] protected int TextureStep = 15;
    [Export] protected int FrameCount = 27;
    [Export] protected int TextureWidth = 68;
    [Export] protected int TextureHeight = 13;
    
    private AtlasTexture _atlasTexture;

    public override void _Ready()
    {
        var styleBox = GetThemeStylebox("slider") as StyleBoxTexture;
        _atlasTexture = styleBox?.Texture as AtlasTexture;
        
        ValueChanged += OnValueChanged;
        OnValueChanged(Value);
    }

    private void OnValueChanged(double value)
    {
        if (_atlasTexture == null) return;
        
        float normalized = MinValue < 0 
            ? (float)(Mathf.Abs(value) / MaxValue)
            : (float)((value - MinValue) / (MaxValue - MinValue));
            
        int frame = Mathf.FloorToInt(normalized * FrameCount);
        _atlasTexture.Region = new Rect2(TextureX, frame * TextureStep, TextureWidth, TextureHeight);
    }
}