using GodAmp.Desktop;
using GodAmp.Audio.Processing;
using Godot;

namespace GodAmp.Settings;

/// <summary>Initializes application preferences and publishes their changes through Godot signals.</summary>
public partial class SettingsManager : Node
{
    public const int MinimumZoom = SettingsStore.MinimumZoom;
    public const int MaximumZoom = SettingsStore.MaximumZoom;
    private const string ApplicationDirectoryName = "GodAmp";
    private const string DataDirectoryEnvironmentVariable = "GODAMP_DATA_DIR";

    [Signal] public delegate void SettingChangedEventHandler(string key, Variant value);
    [Signal] public delegate void LastPlaylistPathChangedEventHandler(string path);
    [Signal] public delegate void ZoomModeChangedEventHandler(int zoomMode);
    [Signal] public delegate void VolumeChangedEventHandler(float volume);

    private SettingsStore _store = null!;
    public static SettingsManager Instance { get; private set; } = null!;

    /// <summary>Absolute application data directory, honoring the optional GODAMP_DATA_DIR override.</summary>
    public static string DataDirectory
    {
        get
        {
            string configured = OS.GetEnvironment(DataDirectoryEnvironmentVariable);
            return string.IsNullOrWhiteSpace(configured)
                ? System.IO.Path.Combine(OS.GetDataDir(), ApplicationDirectoryName)
                : System.IO.Path.GetFullPath(configured);
        }
    }

    /// <summary>Loads preferences before dependent autoloads and scenes enter the tree.</summary>
    public override void _EnterTree()
    {
        Instance = this;
        _store = new SettingsStore(DataDirectory);
        _store.Changed += Publish;
        if (_store.LoadError != Error.Ok)
            GD.PrintErr($"Failed to load settings file: {_store.LoadError}");
    }

    /// <summary>Disconnects preference forwarding and releases the store after dependent nodes exit.</summary>
    public override void _ExitTree()
    {
        _store.Changed -= Publish;
        _store.Dispose();
        if (Instance == this) Instance = null!;
    }

    private void Publish(string key, Variant value)
    {
        EmitSignal(SignalName.SettingChanged, key, value);
        switch (key)
        {
            case SettingsStore.LastPlaylistPathKey:
                EmitSignal(SignalName.LastPlaylistPathChanged, value.AsString());
                break;
            case SettingsStore.ZoomModeKey:
                EmitSignal(SignalName.ZoomModeChanged, value.AsInt32());
                break;
            case SettingsStore.VolumeKey:
                EmitSignal(SignalName.VolumeChanged, value.AsSingle());
                break;
        }
    }

    /// <inheritdoc cref="SettingsStore.GetAudioSettings"/>
    public AudioSettings GetAudioSettings() => _store.GetAudioSettings();
    /// <inheritdoc cref="SettingsStore.SetAudioSettings"/>
    public void SetAudioSettings(AudioSettings settings) => _store.SetAudioSettings(settings);
    /// <inheritdoc cref="SettingsStore.GetLastPlaylistPath"/>
    public string GetLastPlaylistPath() => _store.GetLastPlaylistPath();
    /// <inheritdoc cref="SettingsStore.SetLastPlaylistPath"/>
    public void SetLastPlaylistPath(string path) => _store.SetLastPlaylistPath(path);
    /// <inheritdoc cref="SettingsStore.GetZoomMode"/>
    public int GetZoomMode() => _store.GetZoomMode();
    /// <inheritdoc cref="SettingsStore.SetZoomMode"/>
    public void SetZoomMode(int mode) => _store.SetZoomMode(mode);
    /// <inheritdoc cref="SettingsStore.GetVolume"/>
    public float GetVolume() => _store.GetVolume();
    /// <inheritdoc cref="SettingsStore.SetVolume"/>
    public void SetVolume(float volume) => _store.SetVolume(volume);
    /// <inheritdoc cref="SettingsStore.GetActiveSkin"/>
    public string GetActiveSkin() => _store.GetActiveSkin();
    /// <inheritdoc cref="SettingsStore.SetActiveSkin"/>
    public void SetActiveSkin(string skinFileName) => _store.SetActiveSkin(skinFileName);
    /// <inheritdoc cref="SettingsStore.GetWindowPosition"/>
    public Vector2I GetWindowPosition(PlayerWindow window, Vector2I defaultPos) => _store.GetWindowPosition(window, defaultPos);
    /// <inheritdoc cref="SettingsStore.SetWindowPosition"/>
    public void SetWindowPosition(PlayerWindow window, Vector2I position) => _store.SetWindowPosition(window, position);
    /// <inheritdoc cref="SettingsStore.GetWindowSize"/>
    public Vector2I GetWindowSize(PlayerWindow window, Vector2I defaultSize) => _store.GetWindowSize(window, defaultSize);
    /// <inheritdoc cref="SettingsStore.SetWindowSize"/>
    public void SetWindowSize(PlayerWindow window, Vector2I size) => _store.SetWindowSize(window, size);
    /// <inheritdoc cref="SettingsStore.GetWindowVisible"/>
    public bool GetWindowVisible(PlayerWindow window, bool defaultVisible) => _store.GetWindowVisible(window, defaultVisible);
    /// <inheritdoc cref="SettingsStore.SetWindowVisible"/>
    public void SetWindowVisible(PlayerWindow window, bool visible) => _store.SetWindowVisible(window, visible);
    /// <inheritdoc cref="SettingsStore.GetWindowShaded"/>
    public bool GetWindowShaded(PlayerWindow window) => _store.GetWindowShaded(window);
    /// <inheritdoc cref="SettingsStore.SetWindowShaded"/>
    public void SetWindowShaded(PlayerWindow window, bool shaded) => _store.SetWindowShaded(window, shaded);

    /// <summary>Persists preferences at the explicit save boundary and reports failures.</summary>
    public void SaveAllSettings()
    {
        Error error = _store.Save();
        if (error != Error.Ok) GD.PrintErr($"Failed to save settings file: {error}");
    }
}
