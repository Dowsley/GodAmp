using Godot;
using GodAmp.Desktop;
using GodAmp.Audio.Processing;
using System.Globalization;
using System;

namespace GodAmp.Settings;

/// <summary>Stores typed preferences in ConfigFile, preserving persisted keys and decoding fallbacks.</summary>
public sealed class SettingsStore : IDisposable
{
    /// <summary>Smallest supported integer UI zoom.</summary>
    public const int MinimumZoom = 1;
    /// <summary>Largest supported integer UI zoom.</summary>
    public const int MaximumZoom = 4;
    private const int DefaultZoom = 2;
    private const int CurrentVersion = 1;
    private const string SettingsFileName = "godamp.ini";
    private const string SettingsSection = "settings";

    private const string MetaSection = "meta";
    private const string VersionKey = "version";

    internal const string LastPlaylistPathKey = "last_playlist_path";
    internal const string ZoomModeKey = "zoom_mode";
    internal const string VolumeKey = "volume";
    private const string BalanceKey = "balance";
    private const string PreampKey = "preamp_db";
    private const string EqualizerEnabledKey = "equalizer_enabled";
    private const string EqualizerGainsKey = "equalizer_gains";
    private const string ActiveSkinKey = "active_skin";
    private const string WindowPositionKeyFormat = "window_{0}_position";
    private const string WindowVisibleKeyFormat = "window_{0}_visible";
    private const string WindowSizeKeyFormat = "window_{0}_size";
    private const string WindowShadedKeyFormat = "window_{0}_shaded";

    /// <summary>Raised synchronously after a preference is stored in memory.</summary>
    public event Action<string, Variant>? Changed;
    private readonly ConfigFile _configFile;
    private readonly string _settingsFilePath;

    /// <summary>Result of loading an existing file; missing files use defaults without error.</summary>
    public Error LoadError { get; }

    /// <summary>Opens preferences and initializes the schema version in memory.</summary>
    /// <param name="directory">Resolved application data directory; created when absent.</param>
    public SettingsStore(string directory)
    {
        _settingsFilePath = System.IO.Path.Combine(directory, SettingsFileName);
        System.IO.Directory.CreateDirectory(directory);
        _configFile = new ConfigFile();
        if (System.IO.File.Exists(_settingsFilePath))
            LoadError = _configFile.Load(_settingsFilePath);

        int fileVersion = GetVersion();
        if (fileVersion < CurrentVersion)
        {
            SetVersion(CurrentVersion);
        }
    }

    private int GetVersion()
    {
        Variant value = _configFile.GetValue(MetaSection, VersionKey, 0);
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : 0;
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
        if (_configFile.HasSectionKey(SettingsSection, key))
        {
            return _configFile.GetValue(SettingsSection, key);
        }
        return defaultValue;
    }

    /// <summary>
    /// Sets a setting in memory (does not save to disk immediately).
    /// </summary>
    private void SetSetting(string key, Variant value)
    {
        _configFile.SetValue(SettingsSection, key, value);
        Changed?.Invoke(key, value);
    }

    /// <summary>Reads audio preferences with typed fallbacks for missing or malformed values.</summary>
    /// <returns>A detached snapshot; the audio owner validates gain and volume ranges.</returns>
    public AudioSettings GetAudioSettings()
    {
        Variant enabled = GetSetting(EqualizerEnabledKey);
        Variant gains = GetSetting(EqualizerGainsKey);
        return new AudioSettings(ReadAudioNumber(VolumeKey, AudioSettings.DefaultVolume), ReadAudioNumber(BalanceKey, 0), ReadAudioNumber(PreampKey, 0),
            enabled.VariantType == Variant.Type.Bool && enabled.AsBool(),
            gains.VariantType == Variant.Type.PackedFloat32Array ? gains.AsFloat32Array() : []);
    }

    /// <summary>Stores the audio owner's validated preferences for the normal settings-save boundary.</summary>
    /// <param name="settings">Snapshot whose band gains use native ten-band order.</param>
    public void SetAudioSettings(AudioSettings settings)
    {
        SetVolume(settings.Volume);
        SetSetting(BalanceKey, settings.Balance);
        SetSetting(PreampKey, settings.PreampDb);
        SetSetting(EqualizerEnabledKey, settings.EqualizerEnabled);
        SetSetting(EqualizerGainsKey, settings.BandGains);
    }

    private float ReadAudioNumber(string key, float fallback)
    {
        Variant value = GetSetting(key);
        return value.VariantType is Variant.Type.Float or Variant.Type.Int ? value.AsSingle() : fallback;
    }

    /// <summary>Reads the last played playlist path with a typed fallback.</summary>
    /// <returns>The stored path, or an empty string when missing or malformed.</returns>
    public string GetLastPlaylistPath()
    {
        return ReadText(LastPlaylistPathKey);
    }

    /// <summary>Stores the last played playlist path in memory.</summary>
    /// <param name="path">Playlist source used for startup restoration.</param>
    public void SetLastPlaylistPath(string path)
    {
        SetSetting(LastPlaylistPathKey, path);
    }

    /// <summary>Gets the saved integer UI zoom within the supported range.</summary>
    /// <returns>The clamped zoom, defaulting to 2x when absent.</returns>
    public int GetZoomMode()
    {
        Variant value = GetSetting(ZoomModeKey);
        return value.VariantType == Variant.Type.Int
            ? (int)Math.Clamp(value.AsInt64(), MinimumZoom, MaximumZoom) : DefaultZoom;
    }

    /// <summary>Stores and broadcasts an integer UI zoom within the supported range.</summary>
    /// <param name="mode">Requested multiplier, clamped before storage and notification.</param>
    public void SetZoomMode(int mode)
    {
        SetSetting(ZoomModeKey, Mathf.Clamp(mode, MinimumZoom, MaximumZoom));
    }

    /// <summary>Reads stored volume without applying audio-domain range validation.</summary>
    /// <returns>The numeric preference, or the default volume for a missing or malformed value.</returns>
    public float GetVolume()
    {
        return ReadAudioNumber(VolumeKey, AudioSettings.DefaultVolume);
    }

    /// <summary>Stores the audio owner's normalized volume slider position in memory.</summary>
    /// <param name="volume">Validated slider position.</param>
    public void SetVolume(float volume)
    {
        SetSetting(VolumeKey, volume);
    }

    /// <summary>Reads the active skin filename with a typed fallback.</summary>
    /// <returns>The saved filename, or an empty string selecting the default skin.</returns>
    public string GetActiveSkin()
    {
        return ReadText(ActiveSkinKey);
    }

    /// <summary>Stores the active skin selection in memory.</summary>
    /// <param name="skinFileName">Skin archive filename, or empty for the default skin.</param>
    public void SetActiveSkin(string skinFileName)
    {
        SetSetting(ActiveSkinKey, skinFileName);
    }

    /// <summary>Reads a window's saved desktop position.</summary>
    /// <param name="window">Player window whose position is requested.</param>
    /// <param name="defaultPos">Desktop position used when the setting is missing or malformed.</param>
    /// <returns>Saved desktop coordinates or the supplied default.</returns>
    public Vector2I GetWindowPosition(PlayerWindow window, Vector2I defaultPos)
    {
        string key = GetWindowKey(window, WindowPositionKeyFormat);
        Variant value = GetSetting(key);
        if (value.VariantType != Variant.Type.String)
            return defaultPos;
        string[] parts = value.AsString().Split(',');
        return parts.Length == 2 &&
            int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int x) &&
            int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int y)
            ? new Vector2I(x, y) : defaultPos;
    }

    /// <summary>Stores a window's desktop position.</summary>
    /// <param name="window">Player window to update.</param>
    /// <param name="position">Desktop coordinates in native window units.</param>
    public void SetWindowPosition(PlayerWindow window, Vector2I position)
    {
        string key = GetWindowKey(window, WindowPositionKeyFormat);
        SetSetting(key, string.Create(CultureInfo.InvariantCulture, $"{position.X},{position.Y}"));
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
        Variant value = GetSetting(key);
        return value.VariantType == Variant.Type.Bool ? value.AsBool() : defaultVisible;
    }

    /// <summary>Stores whether a player window is visible.</summary>
    /// <param name="window">Player window to update.</param>
    /// <param name="visible">True when the window is shown.</param>
    public void SetWindowVisible(PlayerWindow window, bool visible)
    {
        string key = GetWindowKey(window, WindowVisibleKeyFormat);
        SetSetting(key, visible);
    }

    /// <summary>Reads a saved compact presentation, using expanded mode for missing or malformed values.</summary>
    /// <param name="window">Player window whose presentation is requested.</param>
    /// <returns>True only when a valid saved boolean selects compact mode.</returns>
    public bool GetWindowShaded(PlayerWindow window)
    {
        Variant value = GetSetting(GetWindowKey(window, WindowShadedKeyFormat));
        return value.VariantType == Variant.Type.Bool && value.AsBool();
    }

    /// <summary>Stores a window's compact presentation independently of its expanded dimensions.</summary>
    /// <param name="window">Player window to update.</param>
    /// <param name="shaded">Whether the compact presentation is active.</param>
    public void SetWindowShaded(PlayerWindow window, bool shaded) =>
        SetSetting(GetWindowKey(window, WindowShadedKeyFormat), shaded);

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

    private string ReadText(string key)
    {
        Variant value = GetSetting(key);
        return value.VariantType == Variant.Type.String ? value.AsString() : "";
    }

    /// <summary>Writes preferences at the caller's chosen save boundary.</summary>
    /// <returns>The ConfigFile save result for the application owner to report.</returns>
    public Error Save() => _configFile.Save(_settingsFilePath);

    /// <summary>Releases the owned ConfigFile without implicitly saving changes.</summary>
    public void Dispose() => _configFile.Dispose();
}
