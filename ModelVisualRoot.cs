using Godot;
using System.Collections.Generic;

/// <summary>
/// Stable adapter between gameplay scenes and replaceable imported models.
/// The wrapper owns model-local transforms and material discovery, while
/// collisions, gameplay state and effect sockets remain outside it.
/// </summary>
[GlobalClass]
public partial class ModelVisualRoot : Node3D
{
    [Export]
    public string TintMaterialName { get; set; } = "";

    [Export]
    public bool RequireTintTarget { get; set; }

    [Export]
    public bool RequireModelMesh { get; set; } = true;

    public Node3D ModelRoot =>
        GetNodeOrNull<Node3D>("Model");

    public int TintTargetCount =>
        _localizedTintMaterials.Count;

    private readonly List<StandardMaterial3D>
        _localizedTintMaterials = new();
    private bool _tintTargetsLocalized;
    private bool _contractErrorReported;

    public override void _Ready()
    {
        ValidateContract();
    }

    public void ReplaceModel(PackedScene scene)
    {
        if (scene == null) return;
        Node3D previous = ModelRoot;
        Node3D replacement = scene.Instantiate<Node3D>();
        if (previous != null) { RemoveChild(previous); previous.QueueFree(); }
        replacement.Name = "Model";
        _localizedTintMaterials.Clear(); _tintTargetsLocalized = false; _contractErrorReported = false;
        AddChild(replacement);
        ValidateContract();
    }

    public bool ValidateContract()
    {
        if (!IsInstanceValid(ModelRoot))
        {
            ReportContractError(
                "requires a Node3D child named 'Model'.");
            return false;
        }

        if (RequireModelMesh && CountModelMeshes() == 0)
        {
            ReportContractError(
                "requires at least one MeshInstance3D below 'Model'.");
            return false;
        }

        if (!RequireTintTarget)
        {
            return true;
        }

        if (EnsureLocalTintMaterials())
        {
            return true;
        }

        ReportContractError(
            $"requires at least one StandardMaterial3D named " +
            $"'{TintMaterialName}'.");
        return false;
    }

    public bool ApplyTint(Color color)
    {
        if (!EnsureLocalTintMaterials())
        {
            return false;
        }

        foreach (StandardMaterial3D material in
            _localizedTintMaterials)
        {
            material.AlbedoColor = color;
        }

        return true;
    }

    public int ApplyMaterialOverrideToAll(
        Material material)
    {
        if (!IsInstanceValid(ModelRoot) || material == null)
        {
            return 0;
        }

        return ApplyMaterialOverrideRecursive(
            ModelRoot,
            material);
    }

    public int CountModelMeshes()
    {
        return IsInstanceValid(ModelRoot)
            ? CountMeshesRecursive(ModelRoot)
            : 0;
    }

    /// <summary>Per-instance palette that preserves authored eye/body surface boundaries.</summary>
    public void ApplySurfacePalette(IReadOnlyDictionary<string, Material> palette, Material fallback)
    {
        if (IsInstanceValid(ModelRoot)) ApplySurfacePaletteRecursive(ModelRoot, palette, fallback);
    }

    private static void ApplySurfacePaletteRecursive(Node root, IReadOnlyDictionary<string, Material> palette, Material fallback)
    {
        if (root is MeshInstance3D instance && instance.Mesh != null)
        {
            instance.MaterialOverride = null;
            for (int i = 0; i < instance.Mesh.GetSurfaceCount(); i++)
            {
                string name = instance.Mesh.SurfaceGetMaterial(i)?.ResourceName ?? "";
                instance.SetSurfaceOverrideMaterial(i, palette.TryGetValue(name, out var material) ? material : fallback);
            }
        }
        foreach (Node child in root.GetChildren()) ApplySurfacePaletteRecursive(child, palette, fallback);
    }

    private bool EnsureLocalTintMaterials()
    {
        if (_tintTargetsLocalized)
        {
            return _localizedTintMaterials.Count > 0;
        }

        if (!IsInstanceValid(ModelRoot) ||
            string.IsNullOrWhiteSpace(TintMaterialName))
        {
            return false;
        }

        LocalizeTintMaterialsRecursive(ModelRoot);
        _tintTargetsLocalized = true;
        return _localizedTintMaterials.Count > 0;
    }

    private void LocalizeTintMaterialsRecursive(Node root)
    {
        if (root is MeshInstance3D meshInstance &&
            meshInstance.Mesh != null)
        {
            for (int surface = 0;
                surface < meshInstance.Mesh.GetSurfaceCount();
                surface++)
            {
                Material activeMaterial =
                    meshInstance.GetActiveMaterial(surface);
                if (activeMaterial is not StandardMaterial3D source ||
                    source.ResourceName != TintMaterialName)
                {
                    continue;
                }

                StandardMaterial3D localMaterial =
                    source.Duplicate(true) as StandardMaterial3D;
                if (localMaterial == null)
                {
                    continue;
                }

                localMaterial.ResourceLocalToScene = true;
                meshInstance.SetSurfaceOverrideMaterial(
                    surface,
                    localMaterial);
                _localizedTintMaterials.Add(localMaterial);
            }
        }

        foreach (Node child in root.GetChildren())
        {
            LocalizeTintMaterialsRecursive(child);
        }
    }

    private static int ApplyMaterialOverrideRecursive(
        Node root,
        Material material)
    {
        int changed = 0;
        if (root is MeshInstance3D meshInstance)
        {
            meshInstance.MaterialOverride = material;
            changed++;
        }

        foreach (Node child in root.GetChildren())
        {
            changed += ApplyMaterialOverrideRecursive(
                child,
                material);
        }

        return changed;
    }

    private static int CountMeshesRecursive(Node root)
    {
        int count = root is MeshInstance3D ? 1 : 0;
        foreach (Node child in root.GetChildren())
        {
            count += CountMeshesRecursive(child);
        }

        return count;
    }

    private void ReportContractError(string detail)
    {
        if (_contractErrorReported)
        {
            return;
        }

        _contractErrorReported = true;
        GD.PushError(
            $"ModelVisualRoot '{GetPath()}' {detail}");
    }
}
