using Godot;

namespace GodAmp.Utils;

public static class ExtensionMethods
{
    public static Vector2I ToVector2I(this Vector2 vector)
    {
        var v = vector.Floor();
        return new Vector2I((int)v.X, (int)v.Y);
    }
}