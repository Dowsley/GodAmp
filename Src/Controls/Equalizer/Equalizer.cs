using GodAmp.Components;
using GodAmp.Utils;
using Godot;

namespace GodAmp.Controls.Equalizer;

public partial class Equalizer : WindowPanelContainer
{
	private VSlider _preampSlider;
	private TextureButton _equalizerToggleButton;

	public override void _Ready()
	{
		_equalizerToggleButton = GetNode<TextureButton>("%EqualizerToggleButton");
		_preampSlider = GetNode<VSlider>("%PreampSlider");
		UIUtils.SetSliderColor(
			_preampSlider, (float) _preampSlider.Value, -12.0f, 12.0f);
	}

	public void OnEqualizerToggleButtonPressed()
	{
		var busIndex = AudioServer.GetBusIndex("Master");
		var val = _equalizerToggleButton.ButtonPressed;
		AudioServer.SetBusEffectEnabled(busIndex, 1, val);
		AudioServer.SetBusEffectEnabled(busIndex, 2, val);
	}
	
	public void OnPreampSliderValueChanged(float value)
	{
		var busIndex = AudioServer.GetBusIndex("Master");
		if (AudioServer.GetBusEffect(busIndex, AudioUtils.AmplifyAudioEffectIndex) is AudioEffectAmplify effect)
		{
			effect.VolumeDb = value;
		}
		UIUtils.SetSliderColor(_preampSlider, value, -12.0f, 12.0f);
	}

	public void On60SliderValueChanged(float value)
	{
		SetEq10Index(0, value);
	}
	
	public void On170SliderValueChanged(float value)
	{
		SetEq10Index(1, value);
	}
	
	public void On310SliderValueChanged(float value)
	{
		SetEq10Index(2, value);
	}
	
	public void On600SliderValueChanged(float value)
	{
		SetEq10Index(3, value);
	}
	
	public void On1KSliderValueChanged(float value)
	{
		SetEq10Index(4, value);
	}
	
	public void On3KSliderValueChanged(float value)
	{
		SetEq10Index(5, value);
	}
	
	public void On6KSliderValueChanged(float value)
	{
		SetEq10Index(6, value);
	}
	
	public void On12KSliderValueChanged(float value)
	{
		SetEq10Index(7, value);
	}
	
	public void On14SliderValueChanged(float value)
	{
		SetEq10Index(8, value);
	}
	
	public void On16SliderValueChanged(float value)
	{
		SetEq10Index(9, value);
	}

	private void SetEq10Index(int index, float value)
	{
		var busIndex = AudioServer.GetBusIndex("Master");
		if (AudioServer.GetBusEffect(busIndex, AudioUtils.Eq10AudioEffectIndex) is AudioEffectEQ10 effect)
		{
			effect.SetBandGainDb(index, value);
		}
	}
}