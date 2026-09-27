using Godot;

namespace GodAmp.Autoload;

/* DO NOT ADD MORE SIGNALS HERE. Migrate existing signals to their owning components,
 * prefer scene-authored connections, and remove this bus once all callers are migrated. */
public partial class SignalBus : Node
{
    /// <summary>Application-wide skin replacement observed by controls in independently instantiated scenes.</summary>
    [Signal] public delegate void SkinChangedEventHandler();

    public static SignalBus Instance { get; private set; } = null!;

    public override void _EnterTree()
    {
        Instance = this;
    }
}
