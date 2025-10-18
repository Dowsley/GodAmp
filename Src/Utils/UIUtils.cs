using Godot;

namespace GodAmp.Utils;

public static class UIUtils
{
	/*
	 * Util to set color for slider based on value.
	 * Used for volume and preamp slider.
	 */
	public static void SetSliderColor(Slider slider, float value, float min, float max)
	{
		double t = (value - min) / (max - min); // Normalize 0..1
		float hue = Mathf.Lerp(0.33f, 0.0f, (float)t); // Green to Red
		var styleBox = (StyleBoxTexture)slider.GetThemeStylebox("slider");
		styleBox.ModulateColor = Color.FromHsv(hue, 1.0f, 1.0f);
	}
}