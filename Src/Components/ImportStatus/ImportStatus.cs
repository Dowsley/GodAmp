using System.Linq;
using GodAmp.Audio;
using GodAmp.Audio.Importing;
using Godot;

namespace GodAmp.Components.ImportStatus;

/// <summary>Presents scene-owned import progress and consolidated source diagnostics.</summary>
public partial class ImportStatus : Window
{
    [Export] private AudioImportController _imports = null!;
    [Export] private Label _status = null!;
    [Export] private ProgressBar _progress = null!;
    [Export] private TextEdit _issues = null!;
    [Export] private Button _cancel = null!;
    [Export] private Button _dismiss = null!;
    [Export] private Timer _showDelay = null!;
    private bool _wasBusy;

    /// <summary>Reflects import state without interrupting the user for batches that finish before the display delay.</summary>
    public void Refresh()
    {
        if (_imports.IsBusy != _wasBusy)
        {
            if (_imports.IsBusy)
                _showDelay.Start();
            else
                _showDelay.Stop();
            _wasBusy = _imports.IsBusy;
        }
        ImportProgress? progress = _imports.Progress;
        _status.Text = _imports.IsBusy
            ? $"Importing {progress?.Completed ?? 0}/{progress?.Total ?? 0} • {_imports.WaitingCount} waiting\n{progress?.Path}"
            : "Audio issues";
        _progress.MaxValue = System.Math.Max(1, progress?.Total ?? 0);
        _progress.Value = progress?.Completed ?? 0;
        _progress.Visible = _imports.IsBusy;
        _cancel.Visible = _imports.IsBusy;
        _dismiss.Visible = !_imports.IsBusy;
        string issues = string.Join("\n\n", _imports.Issues.Select(issue => $"{Describe(issue.Kind)}: {issue.Path}\n{issue.Message}"));
        if (_issues.Text != issues)
            _issues.Text = issues;
        _issues.Visible = _imports.Issues.Count > 0;
        bool show = (_imports.IsBusy && _showDelay.IsStopped()) || _imports.Issues.Count > 0;
        if (show && !Visible)
            PopupCentered();
        else if (!show)
            Hide();
    }

    public void Cancel() => _imports.CancelImports();
    public void Dismiss() => _imports.DismissIssues();

    private static string Describe(AudioIssueKind kind) => kind switch
    {
        AudioIssueKind.Discovery => "Cannot read selection",
        AudioIssueKind.Access => "Cannot read file",
        AudioIssueKind.UnsupportedFormat => "Unsupported format",
        AudioIssueKind.Metadata => "Metadata unavailable",
        AudioIssueKind.Decoding => "Cannot decode audio",
        _ => "Audio problem"
    };

    public void OnCloseRequested()
    {
        _imports.CancelImports();
        _imports.DismissIssues();
        Hide();
    }
}
