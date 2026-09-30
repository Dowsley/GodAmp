using Godot;
using GodAmp.Settings;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using GodAmp.Skins.Fonts;
using GodAmp.Skins.Archives;
using GodAmp.Skins.Regions;
using GodAmp.Skins.Cursors;

namespace GodAmp.Skins;

/// <summary>Initializes skin resources, applies prepared states and publishes skin changes.</summary>
public partial class SkinLoader : Node
{
    /// <summary>Published after a complete skin is applied to its shared resources.</summary>
    [Signal] public delegate void SkinChangedEventHandler();
    /// <summary>Gets the skin autoload initialized before application controls enter the tree.</summary>
    public static SkinLoader Instance { get; private set; } = null!;

    private const string SkinResourcesPath = "res://Data/SkinResources/";
    private const string DefaultCursorsPath = "res://Data/Cursors/";
    private const string DefaultArtworkPath = "res://Assets/Winamp/Raw/";
    private const string BitmapFontPath = DefaultArtworkPath + "TEXT.png";
    private const string BitmapNumbersFontPath = DefaultArtworkPath + "NUMBERS.png";

    private sealed record AtlasBinding(AtlasTexture Atlas, string Name);
    private readonly List<AtlasBinding> _atlases = [];
    private readonly Dictionary<string, Image> _defaultImages = new(StringComparer.OrdinalIgnoreCase);
    private string _skinsDirectory = "";
    private string? _currentSkinName;
    private SkinState _defaultState = null!;
    private SkinState _activeState = null!;

    private SkinStateBuilder _builder = null!;

    /// <summary>Whether compact EQ artwork is supplied or the expanded EQ also uses built-in artwork.</summary>
    public bool EqualizerWindowshadeAvailable => _activeState.EqualizerWindowshadeAvailable;

    /// <summary>Gets the active main-display bitmap font.</summary>
    public FontFile TextFont => _activeState.TextFont;
    /// <summary>Gets the active clock bitmap font.</summary>
    public FontFile NumberFont => _activeState.NumberFont;
    /// <summary>Gets the active playlist colors and requested font family.</summary>
    public PlaylistSkinStyle PlaylistStyle => _activeState.PlaylistStyle;
    /// <summary>Gets the resolved playlist font with system fallback enabled.</summary>
    public Font PlaylistFont => _activeState.PlaylistFont;
    /// <summary>Gets the active classic analyzer palette.</summary>
    public VisualizationPalette Palette => _activeState.Palette;
    /// <summary>Gets the active variable-width generic title glyphs.</summary>
    public GenericTitleFont TitleFont => _activeState.TitleFont;
    /// <summary>Gets the active classic window contours in unscaled skin coordinates.</summary>
    public SkinRegions Regions => _activeState.Regions;
    /// <summary>Gets the selected skin cursor or its built-in fallback.</summary>
    /// <param name="role">Classic control or window cursor role.</param>
    /// <returns>The selected cursor asset without inheriting assets from another skin.</returns>
    public SkinCursor? GetCursor(SkinCursorRole role) => _activeState.Cursors.GetValueOrDefault(role);

    /// <summary>Gets a resolved sheet from the complete active skin state.</summary>
    /// <param name="name">Classic sheet basename without its extension.</param>
    /// <returns>The selected artwork or its built-in fallback.</returns>
    public Texture2D GetSheet(string name) => _activeState.Textures[name];
    /// <summary>Gets the most recent skin-load failure, or null after successful loading or restoration.</summary>
    public string? LastError { get; private set; }

    /// <inheritdoc />
    public override void _EnterTree()
    {
        Instance = this;
        _skinsDirectory = Path.Combine(SettingsManager.DataDirectory, "Skins");
        Directory.CreateDirectory(_skinsDirectory);
        InitializeDefaults();
    }

    /// <inheritdoc />
    public override void _Ready()
    {
        SettingsManager.Instance.ZoomModeChanged += OnZoomModeChanged;
        string activeSkin = SettingsManager.Instance.GetActiveSkin();
        if (!string.IsNullOrEmpty(activeSkin))
        {
            string skinPath = Path.Combine(_skinsDirectory, Path.GetFileName(activeSkin));
            if (!Load(skinPath))
                GD.PushWarning($"Using the default skin: {LastError}");
        }
    }

    /// <inheritdoc />
    public override void _ExitTree() => SettingsManager.Instance.ZoomModeChanged -= OnZoomModeChanged;

    /// <summary>Matches system-font rasterization density to the UI zoom.</summary>
    /// <param name="multiplier">Requested integer zoom, clamped to at least one for rasterization.</param>
    private void OnZoomModeChanged(int multiplier)
    {
        if (_defaultState.PlaylistFont is SystemFont defaultFont)
            defaultFont.Oversampling = Math.Max(1, multiplier);
        if (_activeState.PlaylistFont is SystemFont activeFont)
            activeFont.Oversampling = Math.Max(1, multiplier);
    }

    /// <summary>Retains the built-in atlas sources and constructs the initial fallback skin state.</summary>
    private void InitializeDefaults()
    {
        var textures = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
        foreach (string fileName in ResourceLoader.ListDirectory(SkinResourcesPath).Order(StringComparer.Ordinal))
        {
            if (!fileName.EndsWith(".tres", StringComparison.OrdinalIgnoreCase))
                continue;
            if (GD.Load<Resource>(SkinResourcesPath + fileName) is not AtlasTexture atlas || atlas.Atlas == null)
                continue;
            if (!atlas.Atlas.ResourcePath.StartsWith(DefaultArtworkPath, StringComparison.Ordinal))
                continue;
            string name = Path.GetFileNameWithoutExtension(atlas.Atlas.ResourcePath);
            if (string.IsNullOrEmpty(name))
                continue;
            _atlases.Add(new AtlasBinding(atlas, name));
            textures.TryAdd(name, atlas.Atlas);
            if (!_defaultImages.ContainsKey(name))
                _defaultImages[name] = atlas.Atlas.GetImage();
        }

        foreach (string name in new[] { "PLAYPAUS", "MONOSTER" })
        {
            Texture2D texture = GD.Load<Texture2D>(DefaultArtworkPath + name + ".png");
            textures[name] = texture;
            _defaultImages[name] = texture.GetImage();
        }
        Image text = GD.Load<FontFile>(BitmapFontPath).GetTextureImage(0, Vector2I.Zero, 0);
        Image numbers = GD.Load<FontFile>(BitmapNumbersFontPath).GetTextureImage(0, Vector2I.Zero, 0);
        _defaultImages["TEXT"] = text;
        _defaultImages["NUMBERS"] = numbers;
        _defaultState = SkinStateBuilder.BuildDefault(textures, _defaultImages, LoadDefaultCursors(),
            OS.GetSystemFonts(), ThemeDB.FallbackFont, SettingsManager.Instance.GetZoomMode());
        _builder = new SkinStateBuilder(_defaultState, _defaultImages);
        _activeState = _defaultState;
    }

    /// <summary>Loads Inspector-defined built-in cursor assets and their role mappings.</summary>
    /// <returns>Original-size cursors indexed by their classic skin roles.</returns>
    private static Dictionary<SkinCursorRole, SkinCursor> LoadDefaultCursors()
    {
        var cursors = new Dictionary<SkinCursorRole, SkinCursor>();
        foreach (string fileName in ResourceLoader.ListDirectory(DefaultCursorsPath).Order(StringComparer.Ordinal))
        {
            if (!fileName.EndsWith(".tres", StringComparison.OrdinalIgnoreCase))
                continue;
            var asset = GD.Load<SkinCursorAsset>(DefaultCursorsPath + fileName);
            SkinCursor cursor = asset.CreateCursor();
            foreach (SkinCursorRole role in asset.Roles)
                cursors.Add(role, cursor);
        }
        return cursors;
    }

    /// <summary>Validates and prepares a skin before applying it and saving its selection.</summary>
    /// <param name="filePath">Filesystem or Godot resource path to a classic skin archive.</param>
    /// <returns>True on success; false with <see cref="LastError"/> set if loading fails.</returns>
    public static bool Load(string filePath)
    {
        var loader = Instance;
        try
        {
            var archive = SkinArchive.Read(ProjectSettings.GlobalizePath(filePath));
            var state = loader._builder.Build(archive, OS.GetSystemFonts(), ThemeDB.FallbackFont,
                SettingsManager.Instance.GetZoomMode());
            loader.Apply(state, Path.GetFileName(filePath));
            loader.LastError = null;
            return true;
        }
        catch (Exception ex)
        {
            loader.LastError = $"Could not load skin '{Path.GetFileName(filePath)}': {ex.Message}";
            GD.PushWarning(loader.LastError);
            return false;
        }
    }

    /// <summary>Replaces shared artwork, records the selection, and notifies existing controls.</summary>
    /// <param name="state">Complete prepared textures, fonts, and playlist settings.</param>
    /// <param name="name">Archive filename, or null for the built-in skin.</param>
    private void Apply(SkinState state, string? name)
    {
        SkinState previous = _activeState;
        try
        {
            foreach (var binding in _atlases)
                binding.Atlas.Atlas = state.Textures[binding.Name];
            _activeState = state;
        }
        catch
        {
            foreach (var binding in _atlases)
                binding.Atlas.Atlas = previous.Textures[binding.Name];
            _activeState = previous;
            throw;
        }
        _currentSkinName = name;
        SettingsManager.Instance.SetActiveSkin(name ?? "");
        SettingsManager.Instance.SaveAllSettings();
        EmitSignal(SignalName.SkinChanged);
    }

    /// <summary>Restores built-in artwork, fonts, and playlist styling and clears the saved skin selection.</summary>
    public static void RestoreOriginalSkin()
    {
        Instance.Apply(Instance._defaultState, null);
        Instance.LastError = null;
    }

    /// <summary>Gets the selected archive filename.</summary>
    /// <returns>The filename, or null while the built-in skin is active.</returns>
    public static string? GetCurrentSkinName() => Instance._currentSkinName;
    /// <summary>Gets the skin installation directory beneath the configured data root.</summary>
    /// <returns>An absolute filesystem directory path.</returns>
    public static string GetSkinsDirectory() => Instance._skinsDirectory;

    /// <summary>Lists installed classic skins in case-insensitive filename order.</summary>
    /// <returns>Archive filenames without directory paths, or an empty array if none are installed.</returns>
    public static string[] GetAvailableSkins() => Directory.Exists(GetSkinsDirectory())
        ? [.. Directory.GetFiles(GetSkinsDirectory()).Where(path => Path.GetExtension(path).Equals(".wsz", StringComparison.OrdinalIgnoreCase))
            .Select(Path.GetFileName).OfType<string>().Order(StringComparer.OrdinalIgnoreCase)]
        : [];
}
