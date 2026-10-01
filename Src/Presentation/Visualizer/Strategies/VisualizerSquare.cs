using Godot;

namespace GodAmp.Presentation.Visualizer.Strategies;

/// <summary>Keeps a visualizer square's artwork and collision dimensions aligned.</summary>
public partial class VisualizerSquare : RigidBody2D
{
    [Export] private ColorRect _artwork = null!;
    [Export] private CollisionShape2D _collision = null!;

    /// <summary>Centers the square's artwork and collision shape at the body's origin.</summary>
    /// <param name="size">Side length in visualization pixels.</param>
    /// <param name="color">Audio-reactive artwork color.</param>
    public void SetAppearance(float size, Color color)
    {
        Vector2 dimensions = Vector2.One * size;
        _artwork.Size = dimensions;
        _artwork.Position = -dimensions / 2;
        _artwork.Color = color;
        ((RectangleShape2D)_collision.Shape).Size = dimensions;
    }
}
