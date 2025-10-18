using System.Text;
using Godot;

namespace GodAmp.Controls.MasterPanel;

public partial class MarqueeLabel : Label
{
	[Export] public int MaxLength = 30;
	
	private Timer _timer;
	private string _value = null;
	private int _offset = 0;
	private bool _rotate = false;

	public override void _Ready()
	{
		_timer = GetNode<Timer>("Timer");
	}
	
	public void SetValue(string value)
	{
		if (value.Length > MaxLength)
		{
			_rotate = false;
		}
		var newValue = value.ToUpper() + new string(' ', int.Max(0, MaxLength - value.Length));
		if (_value != newValue)
			_offset = 0;
		_value = newValue;
		RenderText();
	}

	public void OnTimerTimeout()
	{
		if (!string.IsNullOrEmpty(_value))
		{
			RenderText();
			if (_rotate)
				_offset += 1;
		}
		_timer.Start();
	}

	private void RenderText()
	{
		var sb = new StringBuilder();
		for (var i = 0; i < MaxLength; i++)
		{
			sb.Append(_value[(i + _offset) % MaxLength]);
		}
		Text = sb.ToString();
	}
}