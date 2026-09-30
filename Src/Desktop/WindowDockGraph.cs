using System.Collections.Generic;

namespace GodAmp.Desktop;

/// <summary>Owns docking relationships between window identities from a captured desktop layout.</summary>
internal sealed class WindowDockGraph
{
    private readonly Dictionary<PlayerWindow, HashSet<PlayerWindow>> _contacts = [];

    /// <summary>Rebuilds contacts from visible rectangles with positive edge overlap.</summary>
    /// <param name="windows">Desktop snapshots in stable application order.</param>
    /// <param name="tolerance">Maximum gap between contacting edges, in native pixels.</param>
    public void Rebuild(IReadOnlyList<WindowBounds> windows, int tolerance)
    {
        _contacts.Clear();
        foreach (WindowBounds window in windows)
            _contacts.Add(window.Identity, []);
        for (int first = 0; first < windows.Count; first++)
        {
            if (!windows[first].Visible)
                continue;
            for (int second = first + 1; second < windows.Count; second++)
            {
                if (!windows[second].Visible || !WindowDockLayout.TryGetContact(windows[first].Bounds, windows[second].Bounds, tolerance, out _))
                    continue;
                _contacts[windows[first].Identity].Add(windows[second].Identity);
                _contacts[windows[second].Identity].Add(windows[first].Identity);
            }
        }
    }

    /// <summary>Detaches one window from all neighbors before an independent drag.</summary>
    /// <param name="window">Identity of the directly dragged secondary window.</param>
    public void Detach(PlayerWindow window)
    {
        foreach (PlayerWindow neighbor in _contacts[window])
            _contacts[neighbor].Remove(window);
        _contacts[window].Clear();
    }

    /// <summary>Traverses direct and transitive contacts from an anchored window.</summary>
    /// <param name="window">Identity that anchors the group.</param>
    /// <returns>A detached set containing the anchor and every connected neighbor.</returns>
    public IReadOnlySet<PlayerWindow> GetGroup(PlayerWindow window)
    {
        var group = new HashSet<PlayerWindow> { window };
        var pending = new Queue<PlayerWindow>();
        pending.Enqueue(window);
        while (pending.TryDequeue(out PlayerWindow current))
            foreach (PlayerWindow neighbor in _contacts[current])
                if (group.Add(neighbor)) pending.Enqueue(neighbor);
        return group;
    }
}
