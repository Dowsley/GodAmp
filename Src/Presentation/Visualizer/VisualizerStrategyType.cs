using Godot;

namespace GodAmp.Presentation.Visualizer;

[GlobalClass]
public partial class VisualizerStrategyType : Resource
{
    [Export] public StringName Id = "";
    [Export] public string DisplayName = "";
    [Export] public PackedScene Scene = null!;
}
