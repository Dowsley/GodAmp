using Godot;

namespace GodAmp.Presentation.Shortcuts;

/// <summary>Publishes player commands from unhandled keyboard input in its owning viewport.</summary>
public partial class PlayerShortcuts : Node
{
    [Signal] public delegate void JumpToTrackRequestedEventHandler();

    [Export] private Shortcut _jumpToTrack = null!;

    /// <summary>Requests track search for a matching key press after focused controls process input.</summary>
    /// <param name="input">Unhandled keyboard input from this player's viewport.</param>
    public override void _UnhandledKeyInput(InputEvent input)
    {
        if (input is not InputEventKey { Pressed: true, Echo: false } || !_jumpToTrack.MatchesEvent(input))
            return;

        GetViewport().SetInputAsHandled();
        EmitSignal(SignalName.JumpToTrackRequested);
    }
}
