using GodAmp.Autoload;
using GodAmp.Data;
using Godot;

namespace GodAmp.Components;

/// <summary>Tiles the generic title background and renders its variable-width skin lettering.</summary>
public partial class GenericWindowTitle : Control
{
    private const int TileWidth = 25;
    private const int TitleHeight = 20;
    /// <summary>Title rendered with the generic sheet's supported uppercase glyphs.</summary>
    [Export] public string Title { get; set; } = "VISUALIZER";
    private Window _window = null!;

    /// <inheritdoc />
    public override void _Ready()
    {
        _window = GetWindow();
        _window.FocusEntered += QueueRedraw;
        _window.FocusExited += QueueRedraw;
        SignalBus.Instance.SkinChanged += RefreshSkin;
        RefreshSkin();
    }

    /// <inheritdoc />
    public override void _ExitTree()
    {
        _window.FocusEntered -= QueueRedraw;
        _window.FocusExited -= QueueRedraw;
        SignalBus.Instance.SkinChanged -= RefreshSkin;
    }

    /// <summary>Reserves complete title tiles for the active font's measured width.</summary>
    private void RefreshSkin()
    {
        int width = SkinLoader.Instance.TitleFont.Measure(Title);
        CustomMinimumSize = new Vector2(((width + TileWidth - 1) / TileWidth) * TileWidth, TitleHeight);
        QueueRedraw();
    }

    /// <inheritdoc />
    public override void _Draw()
    {
        Texture2D sheet = SkinLoader.Instance.GetSheet("GEN");
        GenericTitleFont font = SkinLoader.Instance.TitleFont;
        bool active = _window.HasFocus();
        for (int x = 0; x < (int)Size.X; x += TileWidth)
        {
            int width = Mathf.Min(TileWidth, (int)Size.X - x);
            DrawTextureRectRegion(sheet, new Rect2(x, 0, width, TitleHeight), new Rect2(52, active ? 0 : 21, width, TitleHeight));
        }
        int textWidth = font.Measure(Title);
        int position = Mathf.Max(0, ((int)Size.X - textWidth) / 2 + (Size.X == textWidth ? 0 : 1));
        foreach (char character in Title)
        {
            Rect2 source = font.GetGlyph(character, active);
            int width = source.HasArea() ? (int)source.Size.X : GenericTitleFont.SpaceWidth;
            if (position + width > Size.X)
                break;
            if (source.HasArea())
                DrawTextureRectRegion(sheet, new Rect2(new Vector2(position, 4), source.Size), source);
            position += width;
        }
    }
}
