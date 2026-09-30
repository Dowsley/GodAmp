using GodAmp.Playback;
using Godot;

namespace GodAmp.Presentation.MainPanel;

/// <summary>Selects skin artwork for the current transport state and source channels.</summary>
public partial class PlaybackIndicators : Control
{
    /// <summary>Scene-assigned owner supplying transport and source-channel information.</summary>
    [Export] public PlaybackController? Playback { get; set; }
    [ExportGroup("Controls")]
    [Export] private TextureRect _transport = null!;
    [Export] private TextureRect _activity = null!;
    [Export] private TextureRect _mono = null!;
    [Export] private TextureRect _stereo = null!;

    [ExportGroup("Artwork")]
    [Export] private Texture2D _playing = null!;
    [Export] private Texture2D _paused = null!;
    [Export] private Texture2D _stopped = null!;
    [Export] private Texture2D _empty = null!;
    [Export] private Texture2D _activityActive = null!;
    [Export] private Texture2D _activityInactive = null!;
    [Export] private Texture2D _monoActive = null!;
    [Export] private Texture2D _monoInactive = null!;
    [Export] private Texture2D _stereoActive = null!;
    [Export] private Texture2D _stereoInactive = null!;

    /// <inheritdoc />
    public override void _Ready() => Refresh();

    /// <summary>Refreshes transport artwork when the selected occurrence or playback state changes.</summary>
    public void Refresh()
    {
        bool hasEntry = Playback?.CurrentEntry != null;
        bool playing = hasEntry && Playback!.State == PlaybackState.Playing;
        _transport.Texture = !hasEntry ? _empty : Playback!.State switch
        {
            PlaybackState.Playing => _playing,
            PlaybackState.Paused => _paused,
            _ => _stopped
        };
        int channels = Playback?.CurrentEntry?.Track.Channels ?? 0;
        _activity.Texture = playing ? _activityActive : _activityInactive;
        _mono.Texture = channels == 1 ? _monoActive : _monoInactive;
        _stereo.Texture = channels >= 2 ? _stereoActive : _stereoInactive;
    }
}
