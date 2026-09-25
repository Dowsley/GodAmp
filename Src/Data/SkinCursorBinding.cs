using Godot;

namespace GodAmp.Data;

/// <summary>An Inspector-defined cursor role for an existing control's visible hit rectangle.</summary>
[GlobalClass]
public partial class SkinCursorBinding : Resource
{
    /// <summary>Control paths relative to the owning cursor controller, sharing this cursor role.</summary>
    [Export] public Godot.Collections.Array<NodePath> ControlPaths { get; set; } = [];
    /// <summary>Classic cursor asset selected while the pointer occupies the control.</summary>
    [Export] public SkinCursorRole Role { get; set; }
    /// <summary>Targets the Godot-owned vertical scrollbar when a mapped control is a ScrollContainer.</summary>
    [Export] public bool UseVerticalScrollBar { get; set; }
}
