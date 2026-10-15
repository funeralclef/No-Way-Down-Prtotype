using Godot;

/// <summary>Each role owns a native creature scene, inside the stable gameplay visual adapter.</summary>
public static class EnemyVisualIdentity
{
    public static string ModelPath(EnemyArchetypeId id) => "res://art/enemies/" + id.ToString().ToLowerInvariant() + ".tscn";
    public static void ApplyModel(ModelVisualRoot visual, EnemyArchetypeId id)
    {
        string path = ModelPath(id);
        if (visual.ModelRoot?.SceneFilePath != path) visual.ReplaceModel(GD.Load<PackedScene>(path));
    }
}
