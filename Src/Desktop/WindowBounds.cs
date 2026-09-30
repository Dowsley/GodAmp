using Godot;

namespace GodAmp.Desktop;

/// <summary>A native geometry snapshot without a reference to its scene window.</summary>
/// <param name="Identity">Stable application window identity.</param>
/// <param name="Bounds">Desktop position and dimensions in native pixels.</param>
/// <param name="Visible">Whether this window participates in desktop docking.</param>
internal readonly record struct WindowBounds(PlayerWindow Identity, Rect2I Bounds, bool Visible);
