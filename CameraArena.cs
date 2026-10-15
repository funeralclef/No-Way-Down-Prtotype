using Godot;

public partial class CameraArena : Area3D
{
    [Export(PropertyHint.Range, "-20.0,20.0,0.5")]
    public float CameraOffsetZ { get; set; }

    [Export(PropertyHint.Range, "0.0,60.0,1.0")]
    public float CameraViewSize { get; set; }

    public ArenaLayout Layout { get; set; }

    public float CameraLockZ => GlobalPosition.Z + CameraOffsetZ;

    public Vector3 ClampCameraTarget(
        Vector3 desiredGlobalPosition,
        float horizontalViewMargin,
        float longitudinalViewMargin)
    {
        if (Layout != null)
        {
            Vector3 center = GlobalPosition; center.Y = desiredGlobalPosition.Y;
            float margin = Mathf.Min(Mathf.Max(horizontalViewMargin, longitudinalViewMargin), Mathf.Min(Layout.Extents.X, Layout.Extents.Y) * .52f);
            return Layout.ClampWorld(desiredGlobalPosition, center, margin);
        }
        CollisionShape3D collision =
            GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
        if (collision == null)
        {
            return desiredGlobalPosition;
        }

        Vector3 localPosition =
            collision.ToLocal(desiredGlobalPosition);
        if (collision.Shape is BoxShape3D boxShape)
        {
            Vector3 halfSize = boxShape.Size * 0.5f;
            float horizontalRange = Mathf.Max(
                0.0f,
                halfSize.X - Mathf.Max(0.0f, horizontalViewMargin));
            float longitudinalRange = Mathf.Max(
                0.0f,
                halfSize.Z - Mathf.Max(0.0f, longitudinalViewMargin));
            localPosition.X = Mathf.Clamp(
                localPosition.X,
                -horizontalRange,
                horizontalRange);
            localPosition.Z = Mathf.Clamp(
                localPosition.Z,
                -longitudinalRange,
                longitudinalRange);
        }
        else if (collision.Shape is CylinderShape3D cylinderShape)
        {
            float cameraFootprint = Mathf.Max(
                Mathf.Max(0.0f, horizontalViewMargin),
                Mathf.Max(0.0f, longitudinalViewMargin));
            // A circle always reveals some sky in the corners. Reserving the
            // full rectangular footprint would pin the camera near its
            // centre, so cap the reservation and preserve meaningful follow.
            cameraFootprint = Mathf.Min(
                cameraFootprint,
                cylinderShape.Radius * 0.52f);
            float trackingRadius = Mathf.Max(
                0.0f,
                cylinderShape.Radius - cameraFootprint);
            Vector2 horizontalPosition =
                new(localPosition.X, localPosition.Z);
            if (horizontalPosition.LengthSquared() >
                trackingRadius * trackingRadius)
            {
                horizontalPosition =
                    horizontalPosition.Normalized() * trackingRadius;
                localPosition.X = horizontalPosition.X;
                localPosition.Z = horizontalPosition.Y;
            }
        }
        else
        {
            return desiredGlobalPosition;
        }

        Vector3 clampedPosition = collision.ToGlobal(localPosition);
        clampedPosition.Y = desiredGlobalPosition.Y;
        return clampedPosition;
    }

    public bool ContainsPlayer(Player player)
    {
        if (!IsInstanceValid(player))
        {
            return false;
        }

        if (Layout != null)
        {
            Vector3 p = player.GlobalPosition - GlobalPosition;
            return Layout.Contains(new Vector2(p.X, p.Z));
        }
        CollisionShape3D collision = GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
        if (collision?.Shape is BoxShape3D boxShape)
        {
            // Arena membership is horizontal. A jump must not temporarily
            // switch the camera back to the travelling mode.
            Vector3 localPosition = collision.ToLocal(player.GlobalPosition);
            Vector3 halfSize = boxShape.Size * 0.5f;
            return Mathf.Abs(localPosition.X) <= halfSize.X &&
                Mathf.Abs(localPosition.Z) <= halfSize.Z;
        }

        if (collision?.Shape is CylinderShape3D cylinderShape)
        {
            Vector3 localPosition = collision.ToLocal(player.GlobalPosition);
            Vector2 horizontalPosition =
                new(localPosition.X, localPosition.Z);
            return horizontalPosition.LengthSquared() <=
                cylinderShape.Radius * cylinderShape.Radius;
        }

        return OverlapsBody(player);
    }
}
