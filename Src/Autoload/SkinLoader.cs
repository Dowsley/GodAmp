using Godot;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using GodAmp.Data;
using GodAmp.Utils;

namespace GodAmp.Autoload;

/// <summary>Prepares complete skin states and applies them to shared atlases and live controls.</summary>
public partial class SkinLoader : Node
{
    /// <summary>Gets the skin autoload initialized before application controls enter the tree.</summary>
    public static SkinLoader Instance { get; private set; } = null!;

    private const string SkinResourcesPath = "res://Data/SkinResources/";
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

    private sealed record SkinState(Dictionary<string, Texture2D> Textures, FontFile TextFont,
        FontFile NumberFont, PlaylistSkinStyle PlaylistStyle, Font PlaylistFont);

    /// <summary>Gets the active main-display bitmap font.</summary>
    public FontFile TextFont => _activeState.TextFont;
    /// <summary>Gets the active clock bitmap font.</summary>
    public FontFile NumberFont => _activeState.NumberFont;
    /// <summary>Gets the active playlist colors and requested font family.</summary>
    public PlaylistSkinStyle PlaylistStyle => _activeState.PlaylistStyle;
    /// <summary>Gets the resolved playlist font with system fallback enabled.</summary>
    public Font PlaylistFont => _activeState.PlaylistFont;
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

        Image text = GD.Load<FontFile>(BitmapFontPath).GetTextureImage(0, Vector2I.Zero, 0);
        Image numbers = GD.Load<FontFile>(BitmapNumbersFontPath).GetTextureImage(0, Vector2I.Zero, 0);
        _defaultImages["TEXT"] = text;
        _defaultImages["NUMBERS"] = numbers;
        var style = PlaylistSkinStyle.Default;
        _defaultState = new SkinState(textures, SkinBitmapFont.CreateText(text),
            SkinBitmapFont.CreateNumbers(numbers, false), style, CreatePlaylistFont(style));
        _activeState = _defaultState;
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
            var state = loader.PrepareState(archive);
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

    /// <summary>Resolves missing artwork and fonts against the built-in skin without changing active controls.</summary>
    /// <param name="archive">Decoded, validated skin assets.</param>
    /// <returns>A complete skin state ready to apply.</returns>
    private SkinState PrepareState(SkinArchive archive)
    {
        var textures = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, original) in _defaultState.Textures)
        {
            Image fallback = _defaultImages[name];
            if (name.Equals("BALANCE", StringComparison.OrdinalIgnoreCase) && !archive.Images.ContainsKey(name))
                fallback = _defaultImages["VOLUME"];
            Image resolved = archive.ResolveImage(name, fallback);
            textures[name] = ReferenceEquals(resolved, _defaultImages[name])
                ? original : ImageTexture.CreateFromImage(resolved);
        }
        Image text = archive.Images.GetValueOrDefault("TEXT", _defaultImages["TEXT"]);
        bool extended = archive.Images.TryGetValue("NUMS_EX", out Image? numbers);
        numbers ??= archive.Images.GetValueOrDefault("NUMBERS", _defaultImages["NUMBERS"]);
        return new SkinState(textures, SkinBitmapFont.CreateText(text),
            SkinBitmapFont.CreateNumbers(numbers, extended), archive.PlaylistStyle, CreatePlaylistFont(archive.PlaylistStyle));
    }

    /// <summary>Resolves an installed playlist font at the current UI rasterization scale.</summary>
    /// <param name="style">Skin settings containing the requested font family.</param>
    /// <returns>A system font, or Godot's fallback when no preferred family is installed.</returns>
    private static Font CreatePlaylistFont(PlaylistSkinStyle style)
    {
        string name = style.ResolveFontName(OS.GetSystemFonts());
        return name.Length == 0 ? ThemeDB.FallbackFont : new SystemFont
        {
            FontNames = [name],
            Antialiasing = TextServer.FontAntialiasing.Gray,
            Oversampling = Math.Max(1, SettingsManager.Instance.GetZoomMode()),
            AllowSystemFallback = true
        };
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
        SignalBus.Instance.EmitSignal(SignalBus.SignalName.SkinChanged);
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
