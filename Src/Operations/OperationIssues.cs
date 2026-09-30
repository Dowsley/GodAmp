using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;

namespace GodAmp.Operations;

/// <summary>Owns operation diagnostics for the scene and publishes changes to status presentation.</summary>
public partial class OperationIssues : Node
{
    [Signal] public delegate void ChangedEventHandler();
    private readonly List<OperationIssue> _issues = [];
    private readonly ReadOnlyCollection<OperationIssue> _view;

    public OperationIssues() => _view = _issues.AsReadOnly();

    /// <summary>Gets reported issues in arrival order.</summary>
    public IReadOnlyList<OperationIssue> Issues => _view;

    /// <summary>Publishes a completed batch of operation diagnostics on the scene thread.</summary>
    /// <param name="issues">Issues to append in their original order.</param>
    public void Report(IEnumerable<OperationIssue> issues)
    {
        int count = _issues.Count;
        _issues.AddRange(issues);
        if (_issues.Count != count)
            EmitSignal(SignalName.Changed);
    }

    /// <summary>Receives a playback failure through the authored scene connection.</summary>
    /// <param name="path">Source that could not be played.</param>
    /// <param name="kind">Stage at which playback failed.</param>
    /// <param name="message">Reason reported by the playback operation.</param>
    public void ReportPlaybackFailure(string path, OperationIssueKind kind, string message) =>
        Report([new OperationIssue(path, kind, message, OperationKind.Playback)]);

    /// <summary>Dismisses collected issues without changing any active operation.</summary>
    public void Dismiss()
    {
        if (_issues.Count == 0)
            return;
        _issues.Clear();
        EmitSignal(SignalName.Changed);
    }
}
