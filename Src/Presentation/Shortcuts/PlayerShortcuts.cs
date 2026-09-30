using GodAmp.Presentation.Commands;
using Godot;

namespace GodAmp.Presentation.Shortcuts;

/// <summary>Publishes player commands from unhandled keyboard input in its owning viewport.</summary>
public partial class PlayerShortcuts : Node
{
    [Signal] public delegate void CommandRequestedEventHandler(PlayerCommand command);

    [Export] private Godot.Collections.Array<PlayerShortcutBinding> _bindings = [];

    /// <summary>Emits the first matching binding's command after focused controls process input.</summary>
    /// <param name="input">Unhandled keyboard input from this player's viewport.</param>
    public override void _UnhandledKeyInput(InputEvent input)
    {
        if (input is not InputEventKey { Pressed: true, Echo: false })
            return;

        foreach (PlayerShortcutBinding binding in _bindings)
        {
            if (binding?.Shortcut == null || !binding.Shortcut.MatchesEvent(input))
                continue;

            GetViewport().SetInputAsHandled();
            EmitSignal(SignalName.CommandRequested, (int)binding.Command);
            return;
        }
    }
}
