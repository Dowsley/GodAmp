using GodAmp.Autoload;
using Godot;

namespace GodAmp.Components;

public partial class BitmapLabel : Label
{
	public override void _Ready()
	{
		SignalBus.Instance.SkinChanged += QueueRedraw;
	}
}