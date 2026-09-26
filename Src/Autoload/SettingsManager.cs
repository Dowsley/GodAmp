using Godot;
using GodAmp.Data;

namespace GodAmp.Autoload;

public partial class SettingsManager : Node
{
    /// <summary>Smallest supported integer UI zoom.</summary>
    public const int MinimumZoom = 1;
    /// <summary>Largest supported integer UI zoom.</summary>
    public const int MaximumZoom = 4;
    private const int CurrentVersion = 1;
    private const string SettingsFileName = "godamp.ini";
    private const string SettingsDir = "GodAmp";

    private const string MetaSection = "meta";
    private const string VersionKey = "version";

    private const string LastPlaylistPathKey = "last_playlist_path";
    private const string ZoomModeKey = "zoom_mode";
    private const string VolumeKey = "volume";
    private const string ActiveSkinKey = "active_skin";
    private const string WindowPositionKeyFormat = "window_{0}_position";
    private const string WindowVisibleKeyFormat = "window_{0}_visible";
    private const string WindowSizeKeyFormat = "window_{0}_size";

    [Signal] public delegate void SettingChangedEventHandler(string key, Variant value);
    [Signal] public delegate void LastPlaylistPathChangedEventHandler(string path);
    [Signal] public delegate void ZoomModeChangedEventHandler(int zoomMode);
    [Signal] public delegate void VolumeChangedEventHandler(float volume);

    private ConfigFile _configFile = null!;
    private string _settingsFilePath = null!;

    public static SettingsManager Instance { get; private set; } = null!;

    /// <summary>Gets the absolute settings and skin directory, optionally overridden by GODAMP_DATA_DIR.</summary>
    public static string DataDirectory
    {
        get
        {
            string overrideDirectory = OS.GetEnvironment("GODAMP_DATA_DIR");
            return string.IsNullOrWhiteSpace(overrideDirectory)
                ? System.IO.Path.Combine(OS.GetDataDir(), SettingsDir)
                : System.IO.Path.GetFullPath(overrideDirectory);
        }
    }

    public override void _EnterTree()
    {
        Instance = this;
        InitializeSettings();
    }

    private void InitializeSettings()
    {
        string godampDir = DataDirectory;
        _settingsFilePath = System.IO.Path.Combine(godampDir, SettingsFileName);
        if (!System.IO.Directory.Exists(godampDir))
        {
            System.IO.Directory.CreateDirectory(godampDir);
        }

        _configFile = new ConfigFile();

        if (System.IO.File.Exists(_settingsFilePath))
        {
            Error error = _configFile.Load(_settingsFilePath);
            if (error != Error.Ok)
            {
                GD.PrintErr($"Failed to load settings file: {error}");
            }
        }

        int fileVersion = GetVersion();
        if (fileVersion < CurrentVersion)
        {
            SetVersion(CurrentVersion);
        }
    }

    private int GetVersion()
    {
        if (_configFile.HasSectionKey(MetaSection, VersionKey))
            return (int)_configFile.GetValue(MetaSection, VersionKey);
        return 0;
    }

    private void SetVersion(int version)
    {
        _configFile.SetValue(MetaSection, VersionKey, version);
    }

    /// <summary>
    /// Gets a setting value by key. Returns null if not found.
    /// </summary>
    private Variant GetSetting(string key, Variant defaultValue = default)
    {
        if (_configFile.HasSectionKey("settings", key))
        {
            return _configFile.GetValue("settings", key);
        }
        return defaultValue;
    }

    /// <summary>
    /// Sets a setting in memory (does not save to disk immediately).
    /// </summary>
    private void SetSetting(string key, Variant value)
    {
        _configFile.SetValue("settings", key, value);
        EmitSignal(SignalName.SettingChanged, key, value);

        switch (key)
        {
            case LastPlaylistPathKey:
                EmitSignal(SignalName.LastPlaylistPathChanged, (string)value);
                break;
            case ZoomModeKey:
                EmitSignal(SignalName.ZoomModeChanged, (int)value);
                break;
            case VolumeKey:
                EmitSignal(SignalName.VolumeChanged, (float)value);
                break;
        }
    }

    /// <summary>
    /// Gets the last played playlist path.
    /// </summary>
    public string GetLastPlaylistPath()
    {
        return (string)GetSetting(LastPlaylistPathKey, "");
    }

    /// <summary>
    /// Sets the last played playlist path.
    /// </summary>
    public void SetLastPlaylistPath(string path)
    {
        SetSetting(LastPlaylistPathKey, path);
    }

    /// <summary>Gets the saved integer UI zoom within the supported range.</summary>
    /// <returns>The clamped zoom, defaulting to 2x when absent.</returns>
    public int GetZoomMode()
    {
        return Mathf.Clamp((int)GetSetting(ZoomModeKey, 2), MinimumZoom, MaximumZoom);
    }

    /// <summary>Stores and broadcasts an integer UI zoom within the supported range.</summary>
    /// <param name="mode">Requested multiplier, clamped before storage and notification.</param>
    public void SetZoomMode(int mode)
    {
        SetSetting(ZoomModeKey, Mathf.Clamp(mode, MinimumZoom, MaximumZoom));
    }

    /// <summary>
    /// Gets the volume level (0.0 to 1.0).
    /// </summary>
    public float GetVolume()
    {
        return (float)GetSetting(VolumeKey, 0.8f);
    }

    /// <summary>
    /// Sets the volume level (0.0 to 1.0).
    /// </summary>
    public void SetVolume(float volume)
    {
        SetSetting(VolumeKey, volume);
    }

    /// <summary>
    /// Gets the active skin filename.
    /// </summary>
    public string GetActiveSkin()
    {
        return (string)GetSetting(ActiveSkinKey, "");
    }

    /// <summary>
    /// Sets the active skin filename.
    /// </summary>
    public void SetActiveSkin(string skinFileName)
    {
        SetSetting(ActiveSkinKey, skinFileName);
    }

    /// <summary>Reads a window's saved desktop position.</summary>
    /// <param name="window">Player window whose position is requested.</param>
    /// <param name="defaultPos">Desktop position used when no setting exists.</param>
    /// <returns>Saved desktop coordinates or the supplied default.</returns>
    public Vector2I GetWindowPosition(PlayerWindow window, Vector2I defaultPos)
    {
        string key = GetWindowKey(window, WindowPositionKeyFormat);
        string value = (string)GetSetting(key, "");
        if (string.IsNullOrEmpty(value))
            return defaultPos;

        var parts = value.Split(',');
        return new Vector2I(int.Parse(parts[0]), int.Parse(parts[1]));
    }

    /// <summary>Stores a window's desktop position.</summary>
    /// <param name="window">Player window to update.</param>
    /// <param name="position">Desktop coordinates in native window units.</param>
    public void SetWindowPosition(PlayerWindow window, Vector2I position)
    {
        string key = GetWindowKey(window, WindowPositionKeyFormat);
        SetSetting(key, $"{position.X},{position.Y}");
    }

    /// <summary>Reads positive logical window dimensions, falling back for missing or malformed settings.</summary>
    /// <param name="window">Player window whose size is requested.</param>
    /// <param name="defaultSize">Scene-defined logical dimensions.</param>
    /// <returns>Saved dimensions in skin pixels or the supplied default.</returns>
    public Vector2I GetWindowSize(PlayerWindow window, Vector2I defaultSize)
    {
        Variant value = GetSetting(GetWindowKey(window, WindowSizeKeyFormat));
        if (value.VariantType != Variant.Type.Vector2I)
            return defaultSize;
        Vector2I size = value.AsVector2I();
        return size.X > 0 && size.Y > 0 && size.X <= int.MaxValue / MaximumZoom && size.Y <= int.MaxValue / MaximumZoom
            ? size : defaultSize;
    }

    /// <summary>Stores a window's unscaled size for restoration independent of zoom.</summary>
    /// <param name="window">Player window to update.</param>
    /// <param name="size">Validated logical dimensions.</param>
    public void SetWindowSize(PlayerWindow window, Vector2I size) =>
        SetSetting(GetWindowKey(window, WindowSizeKeyFormat), size);

    /// <summary>Reads a window's saved visibility.</summary>
    /// <param name="window">Player window whose visibility is requested.</param>
    /// <param name="defaultVisible">Visibility used when no setting exists.</param>
    /// <returns>Saved visibility or the supplied default.</returns>
    public bool GetWindowVisible(PlayerWindow window, bool defaultVisible)
    {
        string key = GetWindowKey(window, WindowVisibleKeyFormat);
        return (bool)GetSetting(key, defaultVisible);
    }

    /// <summary>Stores whether a player window is visible.</summary>
    /// <param name="window">Player window to update.</param>
    /// <param name="visible">True when the window is shown.</param>
    public void SetWindowVisible(PlayerWindow window, bool visible)
    {
        string key = GetWindowKey(window, WindowVisibleKeyFormat);
        SetSetting(key, visible);
    }

    /// <summary>Maps typed window identities to stable names in the settings file.</summary>
    /// <param name="window">Player window represented by the key.</param>
    /// <param name="format">Settings key format containing the window-name placeholder.</param>
    /// <returns>A key using the persisted spelling independently of enum member names.</returns>
    /// <exception cref="System.ArgumentOutOfRangeException">The window identity is undefined.</exception>
    private static string GetWindowKey(PlayerWindow window, string format)
    {
        string name = window switch
        {
            PlayerWindow.MasterPanel => "masterPanel",
            PlayerWindow.Equalizer => "equalizer",
            PlayerWindow.Playlist => "playlist",
            PlayerWindow.Visualizer => "visualizer",
            _ => throw new System.ArgumentOutOfRangeException(nameof(window), window, "Unknown player window.")
        };
        return string.Format(format, name);
    }

    /// <summary>
    /// Saves all settings to disk.
    /// </summary>
    public void SaveAllSettings()
    {
        Error error = _configFile.Save(_settingsFilePath);
        if (error != Error.Ok)
        {
            GD.PrintErr($"Failed to save settings file: {error}");
        }
    }
}
