using GodAmp.Presentation.Formatting;
using Godot;

namespace GodAmp.Presentation.Playlist;

/// <summary>Exposes playlist transport requests and selected/total duration.</summary>
public partial class PlaylistFooter : Control
{
    [Signal] public delegate void PreviousRequestedEventHandler();
    [Signal] public delegate void PlayRequestedEventHandler();
    [Signal] public delegate void PauseRequestedEventHandler();
    [Signal] public delegate void StopRequestedEventHandler();
    [Signal] public delegate void NextRequestedEventHandler();
    [Signal] public delegate void FilesRequestedEventHandler();
    [Export] private Label _duration = null!;

    /// <summary>Displays aggregate duration as selected time followed by total queue time.</summary>
    /// <param name="selected">Selected duration in seconds, including duplicate occurrences.</param>
    /// <param name="total">Duration of the entire queue in seconds.</param>
    public void UpdateDuration(float selected, float total)
    {
        _duration.Text = $"{TrackTime.Format(selected)}/{TrackTime.Format(total)}";
        _duration.TooltipText = $"Selected: {TrackTime.Format(selected)}\nTotal: {TrackTime.Format(total)}";
    }

    private void OnPreviousPressed() => EmitSignal(SignalName.PreviousRequested);
    private void OnPlayPressed() => EmitSignal(SignalName.PlayRequested);
    private void OnPausePressed() => EmitSignal(SignalName.PauseRequested);
    private void OnStopPressed() => EmitSignal(SignalName.StopRequested);
    private void OnNextPressed() => EmitSignal(SignalName.NextRequested);
    private void OnFilesPressed() => EmitSignal(SignalName.FilesRequested);
}
