using Godot;

namespace GodAmp.Presentation.Commands;

/// <summary>Routes player commands to feature owners through scene-authored signal connections.</summary>
public partial class PlayerCommandRouter : Node
{
    [Signal] public delegate void JumpToTrackRequestedEventHandler();

    /// <summary>Publishes the feature request associated with a player command.</summary>
    /// <param name="command">Command requested by a player input source.</param>
    public void Execute(PlayerCommand command)
    {
        switch (command)
        {
            case PlayerCommand.JumpToTrack:
                EmitSignal(SignalName.JumpToTrackRequested);
                break;
            default:
                GD.PushError($"Unsupported player command: {command}.");
                break;
        }
    }
}
