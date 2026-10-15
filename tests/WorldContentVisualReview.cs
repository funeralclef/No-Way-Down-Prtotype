using Godot;

public partial class WorldContentVisualReview : Node3D
{
    public override void _Ready()
    {
        GetNode<Camera3D>("Camera3D")
            .LookAtFromPosition(
                new Vector3(
                    58.0f,
                    62.0f,
                    48.0f),
                new Vector3(
                    0.0f,
                    0.0f,
                    -42.0f),
                Vector3.Up);
    }
}
