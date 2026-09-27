using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GodAmp.Audio.Playback;
using GodAmp.Autoload;
using Godot;

namespace GodAmp.Audio.Importing;

/// <summary>Serializes import requests and commits complete, uncancelled batches on the scene thread.</summary>
public partial class AudioImportController : Node
{
    [Signal] public delegate void StatusChangedEventHandler();
    [Export] private PlaybackController _playbackController = null!;

    private readonly Queue<ImportRequest> _waiting = [];
    private readonly List<AudioIssue> _issues = [];
    private readonly ReadOnlyCollection<AudioIssue> _readOnlyIssues;
    private Task<ImportResult>? _task;
    private CancellationTokenSource? _cancellation;
    private ImportRequest? _active;
    private ImportProgress? _progress;
    private ImportProgress? _publishedProgress;
    private string? _startupFallback;
    private bool _startup;

    public AudioImportController() => _readOnlyIssues = _issues.AsReadOnly();

    public bool IsBusy => _waiting.Count > 0 || _task != null && _cancellation?.IsCancellationRequested == false;
    public int WaitingCount => _waiting.Count;
    public ImportProgress? Progress => _cancellation?.IsCancellationRequested == true ? null : Volatile.Read(ref _progress);
    public IReadOnlyList<AudioIssue> Issues => _readOnlyIssues;

    /// <summary>Queues a user request in submission order; supersedes automatic startup restoration.</summary>
    /// <param name="request">Source selection and the queue operation to apply after preparation.</param>
    public void Enqueue(ImportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Paths.Length == 0)
            return;
        if (_startup)
            CancelImports();
        _waiting.Enqueue(request with { Paths = (string[])request.Paths.Clone() });
        EmitSignal(SignalName.StatusChanged);
    }

    /// <summary>Restores the saved playlist, falling back to packaged samples if restoration fails.</summary>
    /// <param name="playlistPath">Saved filesystem playlist path; empty selects the sample folder.</param>
    /// <param name="sampleDirectory">Packaged samples used when no saved playlist can be restored.</param>
    public void Restore(string playlistPath, string sampleDirectory)
    {
        _startup = true;
        _startupFallback = sampleDirectory;
        _waiting.Enqueue(string.IsNullOrWhiteSpace(playlistPath)
            ? new ImportRequest(ImportSource.Folder, [sampleDirectory], ImportMode.Replace)
            : new ImportRequest(ImportSource.Playlist, [playlistPath], ImportMode.Replace));
    }

    /// <summary>Discards all unfinished batches; completed queue mutations are retained.</summary>
    public void CancelImports()
    {
        _waiting.Clear();
        _cancellation?.Cancel();
        _startup = false;
        _startupFallback = null;
        EmitSignal(SignalName.StatusChanged);
    }

    public void DismissIssues()
    {
        _issues.Clear();
        EmitSignal(SignalName.StatusChanged);
    }

    /// <summary>Collects playback failures in the same scene-owned diagnostic presentation.</summary>
    /// <param name="path">Source that could not be played.</param>
    /// <param name="kind">Stage at which the source failed.</param>
    /// <param name="message">Readable reason for the failure.</param>
    public void ReportPlaybackFailure(string path, AudioIssueKind kind, string message)
    {
        _issues.Add(new AudioIssue(path, kind, message));
        EmitSignal(SignalName.StatusChanged);
    }

    public override void _Process(double delta)
    {
        if (_task == null && _waiting.TryDequeue(out ImportRequest? request))
        {
            _active = request;
            _cancellation = new CancellationTokenSource();
            CancellationToken token = _cancellation.Token;
            _progress = new ImportProgress(0, 0, "");
            _task = Task.Run(() => AudioImporter.Import(request, value => Volatile.Write(ref _progress, value), token), token);
            EmitSignal(SignalName.StatusChanged);
        }
        if (Progress != _publishedProgress)
        {
            _publishedProgress = Progress;
            EmitSignal(SignalName.StatusChanged);
        }
        if (_task is not { IsCompleted: true })
            return;
        try
        {
            if (!_cancellation!.IsCancellationRequested)
                Apply(_task.GetAwaiter().GetResult(), _active!);
            else
                _ = _task.Exception;
        }
        catch (OperationCanceledException) { }
        catch (Exception exception)
        {
            _issues.Add(new AudioIssue(_active!.Paths[0], AudioIssueKind.Discovery, exception.Message));
            GD.PushError(exception.ToString());
        }
        finally
        {
            _task = null;
            _active = null;
            _cancellation!.Dispose();
            _cancellation = null;
            _progress = null;
            EmitSignal(SignalName.StatusChanged);
        }
    }

    private void Apply(ImportResult result, ImportRequest request)
    {
        _issues.AddRange(result.Issues);
        bool applicable = result.Tracks.Count > 0 || result.EmptyPlaylist;
        if (applicable)
        {
            var tracks = result.Tracks.Select(track => track.CreateTrack()).ToArray();
            if (request.Mode == ImportMode.Replace)
                _playbackController.Replace(tracks);
            else
                _playbackController.Append(tracks);
            if (request.Source == ImportSource.Playlist)
                SettingsManager.Instance.SetLastPlaylistPath(request.Paths[0]);
        }
        if (_startup && !applicable && request.Source == ImportSource.Playlist && _startupFallback != null)
            _waiting.Enqueue(new ImportRequest(ImportSource.Folder, [_startupFallback], ImportMode.Replace));
        else
        {
            _startup = false;
            _startupFallback = null;
        }
    }

    public override void _ExitTree()
    {
        _cancellation?.Cancel();
        _waiting.Clear();
        /* Observe and release worker bookkeeping without calling a node after teardown. */
        Task<ImportResult>? task = _task;
        CancellationTokenSource? cancellation = _cancellation;
        if (task != null)
            _ = task.ContinueWith(completed => { _ = completed.Exception; cancellation?.Dispose(); }, TaskScheduler.Default);
        else
            cancellation?.Dispose();
    }
}
