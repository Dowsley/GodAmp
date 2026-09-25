using System;
using System.Text;
using Godot;
using GodAmp.Components;

namespace GodAmp.Controls.MasterPanel;

public partial class MarqueeLabel : BitmapLabel
{
    [Export] public int MaxLength = 30;

    private Timer _timer = null!;
    private string _value = "";
    private int _offset = 0;
    private bool _rotate = false;

    /// <inheritdoc />
    public override void _Ready()
    {
        base._Ready();
        _timer = GetNode<Timer>("Timer");
    }

    public void SetValue(string value)
    {
        _rotate = value.Length > MaxLength;
        var upperValue = value.ToUpper();

        var newValue = _rotate
            ? upperValue + "   ***   " // Separator for scrolling
            : upperValue + new string(' ', Math.Max(0, MaxLength - upperValue.Length));

        if (_value != newValue)
            _offset = 0;
        _value = newValue;
        RenderText();
    }

    private void OnTimerTimeout()
    {
        if (!string.IsNullOrEmpty(_value))
        {
            RenderText();
            if (_rotate)
                _offset += 1;
        }
        _timer.Start();
    }

    /// <summary>Displays a fixed-width slice of the scrolling value, or clears an uninitialized value.</summary>
    private void RenderText()
    {
        if (_value.Length == 0)
        {
            Text = "";
            return;
        }
        var sb = new StringBuilder();
        for (var i = 0; i < MaxLength; i++)
        {
            sb.Append(_value[(i + _offset) % _value.Length]);
        }
        Text = sb.ToString();
    }
}
