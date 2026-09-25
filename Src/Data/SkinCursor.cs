using Godot;

namespace GodAmp.Data;

/// <summary>Classic cursor basenames, including mappings reserved for collapsed windows.</summary>
public enum SkinCursorRole
{
    Normal, VolBal, Posbar, WinBut, Min, Close, MainMenu, TitleBar, SongName,
    WSPosbar, MMenu, WSNormal, PWinBut, PClose, PTBar, PVScroll, PSize, PNormal,
    PWSSize, PWSNorm, EQSlid, EQClose, EQTitle, EQNormal
}

/// <summary>A decoded static cursor with an image-relative hotspot.</summary>
/// <param name="Image">RGBA pixels, preserving the original cursor dimensions.</param>
/// <param name="Hotspot">The pixel identifying the pointer's input position.</param>
public sealed record SkinCursor(Image Image, Vector2I Hotspot);
