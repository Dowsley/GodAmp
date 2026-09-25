using GodAmp.Autoload;
using GodAmp.Utils;
using Godot;

namespace GodAmp.Components;

public partial class BitmapLabel : Label
{
    /// <summary>Selects the clock digit sheet instead of the main text sheet.</summary>
    [Export] public bool UseNumberFont { get; set; }

    /// <inheritdoc />
    public override void _Ready()
    {
        SignalBus.Instance.SkinChanged += ApplySkin;
        ApplySkin();
    }

    /// <inheritdoc />
    public override void _ExitTree() => SignalBus.Instance.SkinChanged -= ApplySkin;

    /// <summary>Applies the selected skin's colored bitmap font without shader or shadow tinting.</summary>
    private void ApplySkin()
    {
        Material = null;
        AddThemeFontOverride("font", UseNumberFont ? SkinLoader.Instance.NumberFont : SkinLoader.Instance.TextFont);
        AddThemeFontSizeOverride("font_size", UseNumberFont ? SkinBitmapFont.NumberHeight : SkinBitmapFont.TextHeight);
        AddThemeColorOverride("font_color", Colors.White);
        AddThemeColorOverride("font_shadow_color", Colors.Transparent);
        QueueRedraw();
    }
}
