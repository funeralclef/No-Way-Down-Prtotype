using Godot;

/// <summary>One ground attack, using identical geometry for its warning, impact and hit test.</summary>
public partial class BossHazard : Node3D
{
    public enum ShapeKind { Ring, Lane, Disc }
    public ShapeKind Kind { get; set; }
    public float Radius { get; set; } = 3;
    public float Width { get; set; } = 1.35f;
    public float Length { get; set; } = 20;
    public float WarningTime { get; set; } = 1.15f;
    public float ActiveTime { get; set; } = .45f;
    public float ExpansionSpeed { get; set; }
    public Player Target { get; set; }
    public Color Tint { get; set; } = new Color("#ff6d79");
    public bool Active => _age >= WarningTime && _age < WarningTime + ActiveTime;
    public float CurrentRadius => Radius + Mathf.Max(0, _age - WarningTime) * ExpansionSpeed;
    private float _age;
    private MeshInstance3D _mesh;
    private StandardMaterial3D _material;
    private TorusMesh _ring;
    private bool _hit;
    public override void _Ready()
    {
        AddToGroup("hostile_telegraph");
        _material = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha, AlbedoColor = new Color(Tint, .24f),
            EmissionEnabled = true, Emission = Tint, EmissionEnergyMultiplier = .55f,
            NoDepthTest = false, CullMode = BaseMaterial3D.CullModeEnum.Disabled };
        Mesh geometry;
        if (Kind == ShapeKind.Ring) {
            _ring = new TorusMesh { InnerRadius = Mathf.Max(.1f, Radius - Width / 2), OuterRadius = Radius + Width / 2,
                Rings = 64, RingSegments = 8 }; geometry = _ring;
        } else if (Kind == ShapeKind.Disc) geometry = new CylinderMesh { TopRadius = Radius, BottomRadius = Radius, Height = .035f, RadialSegments = 40 };
        else geometry = new BoxMesh { Size = new Vector3(Width, .06f, Length) };
        _mesh = new MeshInstance3D { Mesh = geometry, MaterialOverride = _material,
            Position = new Vector3(0, .08f, 0), Scale = new Vector3(1, Kind == ShapeKind.Ring ? .12f : 1, 1),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(_mesh);
    }
    public override void _PhysicsProcess(double delta)
    {
        _age += (float)delta;
        if (_age > WarningTime + ActiveTime) { QueueFree(); return; }
        float opacity = Active ? .85f : .18f + .25f * Mathf.Clamp(_age / WarningTime, 0, 1);
        _material.AlbedoColor = new Color(Active ? Tint : Tint.Lightened(.3f), opacity);
        if (_ring != null) {
            _ring.InnerRadius = Mathf.Max(.1f, CurrentRadius - Width / 2);
            _ring.OuterRadius = CurrentRadius + Width / 2;
        }
        if (!Active || _hit || !IsInstanceValid(Target) || Target.Health <= 0) return;
        Vector3 local = ToLocal(Target.GlobalPosition);
        if (Hits(local)) { Target.TakeDamage(this); _hit = true; }
    }
    public bool InterruptWarning()
    {
        if (Active || IsQueuedForDeletion()) return false;
        QueueFree(); return true;
    }
    public bool OverlapsDisc(Vector3 center,float radius)
    {
        Vector3 p=ToLocal(center);Vector2 point=new(p.X,p.Z);
        if(Kind==ShapeKind.Disc)return point.Length()<=Radius+radius;
        if(Kind==ShapeKind.Ring)return Mathf.Abs(point.Length()-CurrentRadius)<=Width/2+radius;
        Vector2 nearest=new(Mathf.Clamp(point.X,-Width/2,Width/2),Mathf.Clamp(point.Y,-Length/2,Length/2));
        return point.DistanceTo(nearest)<=radius;
    }
    public bool Hits(Vector3 local) => local.Y < 1.45f && (Kind == ShapeKind.Disc
        ? new Vector2(local.X, local.Z).Length() < Radius + .45f
        : Kind == ShapeKind.Ring
        ? Mathf.Abs(new Vector2(local.X, local.Z).Length() - CurrentRadius) < Width / 2 + .45f
        : Mathf.Abs(local.X) < Width / 2 + .45f && Mathf.Abs(local.Z) < Length / 2 + .45f);
}
