using GodAmp.Autoload;
using Godot;

namespace GodAmp.Components;

/// <summary>Uses independent latched and held sprite states for classic skin toggle buttons.</summary>
public partial class SkinToggleButton : TextureButton
{
    /// <summary>Offset from a released sprite to its mouse-held sprite in the same sheet.</summary>
    [Export] public Vector2 HeldOffset { get; set; }
    private AtlasTexture _off = null!;
    private AtlasTexture _on = null!;
    private readonly AtlasTexture _display = new();
    private bool _held;

    /// <inheritdoc />
    public override void _Ready()
    {
        _off = (AtlasTexture)TextureNormal;
        _on = (AtlasTexture)TexturePressed;
        TextureNormal = _display;
        TexturePressed = _display;
        TextureHover = _display;
        TextureDisabled = _display;
        /* The hosting window and autoload live outside this reusable control scene. */
        GetWindow().FocusExited += OnButtonUp;
        SignalBus.Instance.SkinChanged += RefreshTexture;
        RefreshTexture();
    }

    /// <inheritdoc />
    public override void _ExitTree()
    {
        SignalBus.Instance.SkinChanged -= RefreshTexture;
        GetWindow().FocusExited -= OnButtonUp;
    }

    /// <inheritdoc />
    public override void _Process(double delta) => RefreshTexture();

    /// <summary>Records a press originating on this button.</summary>
    private void OnButtonDown() => _held = true;

    /// <summary>Releases the held artwork without changing the latched state.</summary>
    private void OnButtonUp() => _held = false;

    /// <summary>Updates the displayed atlas without conflating a latched toggle with a held press.</summary>
    private void RefreshTexture()
    {
        if (Disabled)
            _held = false;
        AtlasTexture source = ButtonPressed ? _on : _off;
        Rect2 region = source.Region;
        if (!Disabled && _held && IsHovered())
            region.Position += HeldOffset;
        if (_display.Atlas != source.Atlas)
            _display.Atlas = source.Atlas;
        if (_display.Region != region)
            _display.Region = region;
    }
}
