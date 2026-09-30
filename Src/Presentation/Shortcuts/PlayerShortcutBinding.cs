using GodAmp.Presentation.Commands;
using Godot;

namespace GodAmp.Presentation.Shortcuts;

/// <summary>Associates an Inspector-authored shortcut with a player command.</summary>
[GlobalClass]
public partial class PlayerShortcutBinding : Resource
{
    [Export] public Shortcut Shortcut { get; set; } = new();
    [Export] public PlayerCommand Command { get; set; }
}
