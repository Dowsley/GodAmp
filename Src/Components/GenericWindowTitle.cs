using GodAmp.Autoload;
using GodAmp.Data;
using Godot;

namespace GodAmp.Components;

/// <summary>Tiles the generic title background and renders its variable-width skin lettering.</summary>
[Tool]
public partial class GenericWindowTitle : Control
{
    private const int TileWidth = 25;
    private const int TitleHeight = 20;
    /// <summary>Title rendered with the generic sheet's supported uppercase glyphs.</summary>
    [Export] public string Title
    {
        get => _title;
        set
        {
            _title = value;
            if (IsInsideTree())
                RefreshSkin();
        }
    }

    /// <summary>Generic artwork used for the editor preview without loading runtime skin state.</summary>
    [Export] public Texture2D? DefaultSheet
    {
        get => _defaultSheet;
        set
        {
            _defaultSheet = value;
            _previewFont = null;
            if (IsInsideTree())
                RefreshSkin();
        }
    }

    private string _title = "";
    private Texture2D? _defaultSheet;
    private GenericTitleFont? _previewFont;
    private Texture2D? _sheet;
    private GenericTitleFont? _font;
    private Window? _window;

    /// <inheritdoc />
    public override void _Ready()
    {
        if (!Engine.IsEditorHint())
        {
            _window = GetWindow();
            _window.FocusEntered += QueueRedraw;
            _window.FocusExited += QueueRedraw;
            SignalBus.Instance.SkinChanged += RefreshSkin;
        }
        RefreshSkin();
    }

    /// <inheritdoc />
    public override void _ExitTree()
    {
        if (_window != null)
        {
            _window.FocusEntered -= QueueRedraw;
            _window.FocusExited -= QueueRedraw;
            SignalBus.Instance.SkinChanged -= RefreshSkin;
            _window = null;
        }
    }

    /// <summary>Reserves complete title tiles for the active font's measured width.</summary>
    private void RefreshSkin()
    {
        if (Engine.IsEditorHint())
        {
            _sheet = DefaultSheet;
            if (_previewFont == null && _sheet != null)
                _previewFont = new GenericTitleFont(_sheet.GetImage());
            _font = _previewFont;
        }
        else
        {
            _sheet = SkinLoader.Instance.GetSheet("GEN");
            _font = SkinLoader.Instance.TitleFont;
        }
        int width = _font?.Measure(Title) ?? 0;
        CustomMinimumSize = new Vector2(((width + TileWidth - 1) / TileWidth) * TileWidth, TitleHeight);
        QueueRedraw();
    }

    /// <inheritdoc />
    public override void _Draw()
    {
        if (_sheet == null || _font == null)
            return;
        bool active = Engine.IsEditorHint() || (_window?.HasFocus() ?? false);
        for (int x = 0; x < (int)Size.X; x += TileWidth)
        {
            int width = Mathf.Min(TileWidth, (int)Size.X - x);
            DrawTextureRectRegion(_sheet, new Rect2(x, 0, width, TitleHeight), new Rect2(52, active ? 0 : 21, width, TitleHeight));
        }
        int textWidth = _font.Measure(Title);
        int position = Mathf.Max(0, ((int)Size.X - textWidth) / 2 + (Size.X == textWidth ? 0 : 1));
        foreach (char character in Title)
        {
            Rect2 source = _font.GetGlyph(character, active);
            int width = source.HasArea() ? (int)source.Size.X : GenericTitleFont.SpaceWidth;
            if (position + width > Size.X)
                break;
            if (source.HasArea())
                DrawTextureRectRegion(_sheet, new Rect2(new Vector2(position, 4), source.Size), source);
            position += width;
        }
    }
}
