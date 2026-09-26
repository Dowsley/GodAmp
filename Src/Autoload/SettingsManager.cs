using Godot;

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

    public Vector2I GetWindowPosition(string windowName, Vector2I defaultPos)
    {
        string key = string.Format(WindowPositionKeyFormat, windowName);
        string value = (string)GetSetting(key, "");
        if (string.IsNullOrEmpty(value))
            return defaultPos;

        var parts = value.Split(',');
        return new Vector2I(int.Parse(parts[0]), int.Parse(parts[1]));
    }

    public void SetWindowPosition(string windowName, Vector2I position)
    {
        string key = string.Format(WindowPositionKeyFormat, windowName);
        SetSetting(key, $"{position.X},{position.Y}");
    }

    public bool GetWindowVisible(string windowName, bool defaultVisible)
    {
        string key = string.Format(WindowVisibleKeyFormat, windowName);
        return (bool)GetSetting(key, defaultVisible);
    }

    public void SetWindowVisible(string windowName, bool visible)
    {
        string key = string.Format(WindowVisibleKeyFormat, windowName);
        SetSetting(key, visible);
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
