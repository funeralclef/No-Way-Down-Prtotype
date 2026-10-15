using Godot;

/// <summary>Short world-space effect. Cosmetic lifetime never changes the source action.</summary>
public partial class RunSkillPulse : Node3D
{
    public float Radius {get;init;}=6;
    public Color Tint {get;init;}=new Color("#a6ffe3");
    private float _age;
    private StandardMaterial3D _material;
    public override void _Ready()
    {
        _material=new StandardMaterial3D {ShadingMode=BaseMaterial3D.ShadingModeEnum.Unshaded,Transparency=BaseMaterial3D.TransparencyEnum.Alpha,AlbedoColor=Tint,EmissionEnabled=true,Emission=Tint,EmissionEnergyMultiplier=.6f};
        AddChild(new MeshInstance3D {Mesh=new TorusMesh {InnerRadius=.9f,OuterRadius=1,Rings=32,RingSegments=8},MaterialOverride=_material,CastShadow=GeometryInstance3D.ShadowCastingSetting.Off});
    }
    public override void _Process(double delta)
    {_age+=(float)delta;float t=_age/.55f;if(t>=1){QueueFree();return;}float s=Mathf.Lerp(.4f,Radius,t);Scale=new Vector3(s,.1f,s);_material.AlbedoColor=new Color(Tint,1-t);}
}
