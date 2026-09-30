using GodAmp.Skins;
using Godot;

namespace GodAmp.Presentation.Components;

public partial class BitmapLabel : Label
{
    /// <summary>Selects the clock digit sheet instead of the main text sheet.</summary>
    [Export] public bool UseNumberFont { get; set; }

    /// <inheritdoc />
    public override void _Ready()
    {
        SkinLoader.Instance.SkinChanged += ApplySkin;
        ApplySkin();
    }

    /// <inheritdoc />
    public override void _ExitTree() => SkinLoader.Instance.SkinChanged -= ApplySkin;

    /// <summary>Updates skin-dependent glyph artwork; scene themes define font size and colors.</summary>
    private void ApplySkin()
    {
        AddThemeFontOverride("font", UseNumberFont ? SkinLoader.Instance.NumberFont : SkinLoader.Instance.TextFont);
        QueueRedraw();
    }
}
