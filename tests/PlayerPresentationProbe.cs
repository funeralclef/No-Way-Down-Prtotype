using Godot;
using System.Collections.Generic;

/// <summary>
/// Regression coverage for authored player visuals and per-instance resources.
/// </summary>
public partial class PlayerPresentationProbe : Node3D
{
    private bool _failed;

    public override async void _Ready()
    {
        PackedScene playerScene =
            GD.Load<PackedScene>(
                "res://player.tscn");
        Player first = playerScene.Instantiate<Player>();
        Player second = playerScene.Instantiate<Player>();
        first.SetPhysicsProcess(false);
        second.SetPhysicsProcess(false);
        AddChild(first);
        AddChild(second);
        await ToSignal(
            GetTree(),
            SceneTree.SignalName.ProcessFrame);

        PlayerPresentation firstPresentation =
            first.GetNode<PlayerPresentation>(
                "Pivot");
        PlayerPresentation secondPresentation =
            second.GetNode<PlayerPresentation>(
                "Pivot");
        Node3D firstSkin = first.GetNode<Node3D>(
            "Pivot/SkinSilhouette");
        int skinChildCount =
            firstSkin.GetChildCount();
        int pivotChildCount =
            firstPresentation.GetChildCount();
        ulong coreMaterialId = GetMaterial(
            first,
            "CoreShell")?.GetInstanceId() ?? 0;

        for (int pass = 0; pass < 12; pass++)
        {
            first.ApplySkin(
                pass % 4,
                new Color(0.9f, 0.2f, 0.2f),
                new Color(0.2f, 0.9f, 0.3f),
                new Color(0.15f, 0.55f, 1.0f));
        }

        Check(
            skinChildCount == 3 &&
                firstSkin.GetChildCount() ==
                    skinChildCount &&
                firstPresentation.GetChildCount() ==
                    pivotChildCount &&
                GetMaterial(
                    first,
                    "CoreShell")
                    ?.GetInstanceId() ==
                    coreMaterialId,
            "repeated ApplySkin reuses three authored nodes and materials");

        GpuParticles3D trail =
            first.GetNode<GpuParticles3D>(
                "MomentumTrail");
        OmniLight3D glow =
            first.GetNode<OmniLight3D>(
                "RushGlow");
        int[] expectedAmounts = { 1, 10, 18, 28 };
        for (int quality = 0; quality <= 3; quality++)
        {
            first.SetEffectsQuality(quality);
            Check(
                first.EffectsQuality == quality &&
                    firstPresentation.EffectsQuality ==
                        quality &&
                    trail.Amount ==
                        expectedAmounts[quality] &&
                    trail.Visible ==
                        (quality > 0) &&
                    glow.Visible ==
                        (quality >= 2),
                $"effects quality {quality} applies the authored budget");
        }

        Color firstBody =
            new(0.12f, 0.72f, 0.32f);
        Color firstAccent =
            new(0.16f, 0.48f, 1.0f);
        Color secondBody =
            new(0.92f, 0.34f, 0.18f);
        Color secondAccent =
            new(1.0f, 0.72f, 0.08f);
        first.ApplySkin(
            0,
            Colors.White,
            firstBody,
            firstAccent);
        second.ApplySkin(
            1,
            Colors.White,
            secondBody,
            secondAccent);

        StandardMaterial3D firstCore =
            GetMaterial(
                first,
                "CoreShell");
        StandardMaterial3D secondCore =
            GetMaterial(
                second,
                "CoreShell");
        StandardMaterial3D firstVisor =
            GetMaterial(
                first,
                "ForwardVisor");
        StandardMaterial3D secondVisor =
            GetMaterial(
                second,
                "ForwardVisor");
        Check(
            firstCore != null &&
                secondCore != null &&
                firstVisor != null &&
                secondVisor != null &&
                firstCore.GetInstanceId() !=
                    secondCore.GetInstanceId() &&
                firstVisor.GetInstanceId() !=
                    secondVisor.GetInstanceId() &&
                firstCore.ResourceLocalToScene &&
                secondCore.ResourceLocalToScene &&
                ColorsMatch(
                    firstCore.AlbedoColor,
                    firstBody) &&
                ColorsMatch(
                    secondCore.AlbedoColor,
                    secondBody) &&
                ColorsMatch(
                    firstVisor.AlbedoColor,
                    firstAccent) &&
                ColorsMatch(
                    secondVisor.AlbedoColor,
                    secondAccent),
            "two Player instances own independent skin materials");

        List<StandardMaterial3D> firstBodyMaterials =
            FindBodyMaterials(firstPresentation);
        List<StandardMaterial3D> secondBodyMaterials =
            FindBodyMaterials(secondPresentation);
        bool bodyMaterialsIndependent =
            firstBodyMaterials.Count > 0 &&
            firstBodyMaterials.Count ==
                secondBodyMaterials.Count;
        for (int index = 0;
            bodyMaterialsIndependent &&
                index < firstBodyMaterials.Count;
            index++)
        {
            bodyMaterialsIndependent =
                firstBodyMaterials[index].GetInstanceId() !=
                    secondBodyMaterials[index].GetInstanceId() &&
                ColorsMatch(
                    firstBodyMaterials[index].AlbedoColor,
                    firstBody) &&
                ColorsMatch(
                    secondBodyMaterials[index].AlbedoColor,
                    secondBody);
        }
        Check(
            bodyMaterialsIndependent,
            "imported body surfaces use independent recolored materials");

        ParticleProcessMaterial firstTrailMaterial =
            first.GetNode<GpuParticles3D>(
                "MomentumTrail")
            .ProcessMaterial as ParticleProcessMaterial;
        ParticleProcessMaterial secondTrailMaterial =
            second.GetNode<GpuParticles3D>(
                "MomentumTrail")
            .ProcessMaterial as ParticleProcessMaterial;
        Color secondTrailColorBefore =
            secondTrailMaterial?.Color ?? Colors.Transparent;
        firstPresentation.UpdateMotion(
            new Vector3(8.0f, 0.0f, -12.0f),
            Vector3.Forward,
            18.0f,
            Player.MomentumTier.Rush,
            1.0f,
            1.0f,
            0,
            Player.ComboFlowTier.Warmup,
            0.0f);
        Check(
            firstTrailMaterial != null &&
                secondTrailMaterial != null &&
                firstTrailMaterial.GetInstanceId() !=
                    secondTrailMaterial.GetInstanceId() &&
                firstTrailMaterial.ResourceLocalToScene &&
                secondTrailMaterial.ResourceLocalToScene &&
                !ColorsMatch(
                    firstTrailMaterial.Color,
                    secondTrailMaterial.Color) &&
                ColorsMatch(
                    secondTrailMaterial.Color,
                    secondTrailColorBefore),
            "trail color changes stay local to one Player instance");

        float firstMarkX = first
            .GetNode<MeshInstance3D>(
                "Pivot/SkinSilhouette/RunnerTechnicalMark")
            .Position.X;
        float secondMarkX = second
            .GetNode<MeshInstance3D>(
                "Pivot/SkinSilhouette/RunnerTechnicalMark")
            .Position.X;
        Check(
            Mathf.IsEqualApprox(
                firstMarkX,
                -0.18f) &&
                Mathf.IsEqualApprox(
                    secondMarkX,
                    0.18f),
            "skin variant repositions only its authored technical mark");

        AnimationPlayer animationPlayer =
            first.GetNode<AnimationPlayer>(
                "AnimationPlayer");
        firstPresentation.SetAnimationSpeed(
            Vector3.Forward,
            1.0f,
            1.2f);
        bool animationAccelerated =
            Mathf.IsEqualApprox(
                animationPlayer.SpeedScale,
                1.68f);
        firstPresentation.SetAnimationSpeed(
            Vector3.Zero,
            1.0f,
            2.0f);
        Check(
            animationAccelerated &&
                Mathf.IsEqualApprox(
                    animationPlayer.SpeedScale,
                    1.0f),
            "animation speed binding follows movement and idle state");

        firstPresentation.UpdateMotion(
            new Vector3(8.0f, 4.0f, -12.0f),
            Vector3.Right,
            18.0f,
            Player.MomentumTier.Medium,
            0.0f,
            0.0f,
            0,
            Player.ComboFlowTier.Warmup,
            0.0f);
        Check(
            !firstPresentation.Basis.IsEqualApprox(
                Basis.Identity) &&
                Mathf.Abs(
                    firstPresentation.Rotation.X) > 0.01f &&
                Mathf.Abs(
                    firstPresentation.Rotation.Z) > 0.01f,
            "motion presentation updates heading, pitch and turn lean");

        firstPresentation.SetDamageVisible(false);
        bool damageHidden =
            !firstPresentation.Visible;
        firstPresentation.SetDamageVisible(true);
        Check(
            damageHidden &&
                firstPresentation.Visible,
            "damage visibility controls the authored visual root");

        first.QueueFree();
        second.QueueFree();
        await ToSignal(
            GetTree(),
            SceneTree.SignalName.ProcessFrame);
        GD.Print(
            _failed
                ? "PLAYER_PRESENTATION_PROBE: FAILED"
                : "PLAYER_PRESENTATION_PROBE: PASSED");
        GetTree().Quit(
            _failed ? 1 : 0);
    }

    private static StandardMaterial3D GetMaterial(
        Player player,
        string meshName)
    {
        return player
            .GetNode<MeshInstance3D>(
                $"Pivot/SkinSilhouette/{meshName}")
            .MaterialOverride as StandardMaterial3D;
    }

    private static List<StandardMaterial3D> FindBodyMaterials(
        Node root)
    {
        List<StandardMaterial3D> materials = new();
        CollectBodyMaterials(
            root,
            materials);
        return materials;
    }

    private static void CollectBodyMaterials(
        Node root,
        List<StandardMaterial3D> materials)
    {
        foreach (Node child in root.GetChildren())
        {
            if (child is MeshInstance3D mesh &&
                mesh.Mesh != null)
            {
                for (int surface = 0;
                    surface < mesh.Mesh.GetSurfaceCount();
                    surface++)
                {
                    if (mesh.GetActiveMaterial(surface) is
                            StandardMaterial3D material &&
                        material.ResourceName == "body")
                    {
                        materials.Add(material);
                    }
                }
            }

            CollectBodyMaterials(
                child,
                materials);
        }
    }

    private static bool ColorsMatch(
        Color first,
        Color second)
    {
        return Mathf.IsEqualApprox(first.R, second.R) &&
            Mathf.IsEqualApprox(first.G, second.G) &&
            Mathf.IsEqualApprox(first.B, second.B) &&
            Mathf.IsEqualApprox(first.A, second.A);
    }

    private void Check(
        bool condition,
        string description)
    {
        if (condition)
        {
            GD.Print(
                $"PLAYER_PRESENTATION_ASSERT: PASS - {description}");
            return;
        }

        _failed = true;
        GD.PushError(
            $"PLAYER_PRESENTATION_ASSERT: FAIL - {description}");
    }
}
