using Godot;

/// <summary>Seeded decoration outside the playable bounds. No collision or gameplay RNG.</summary>
public static class RunBiomeScenery
{
    public static Node3D Build(Node3D section, int biome, float width, float length, ulong seed)
    {
        var root = new Node3D { Name = "BiomeEnvironment" };
        root.SetMeta("biome_id", biome); section.AddChild(root);
        var random = new RandomNumberGenerator { Seed = seed };
        var profile = RunBiomeCatalog.Get(biome);
        var stone = Material(biome == 2 ? new Color("#b6c8cc") : new Color("#46525a"), .84f);
        var dark = Material(new Color("#202c35"), .72f);
        var foliage = Material(new Color("#315c48"), .9f);
        var light = Material(profile.Accent, .4f, .65f);
        int rows = Mathf.Clamp(Mathf.CeilToInt(length / 24), 2, 5);
        for (int row = 0; row < rows; row++)
        for (int side = -1; side <= 1; side += 2)
        {
            float x = side * (width * .5f + random.RandfRange(4, 10));
            float z = -length * .5f + (row + .5f) * length / rows + random.RandfRange(-2, 2);
            Vector3 p = new(x, -1.2f, z);
            switch (biome)
            {
                case 0:
                    Box(root, "RuinPlinth", p, new(4, 2.4f, 5), stone);
                    Cylinder(root, "BrokenColumn", p + new Vector3(0, 4, 0), 1.05f, .8f, 8, stone, new(0, 0, side * .09f), 7);
                    Box(root, "ColumnFoot", p + new Vector3(0, 1.2f, 0), new(2.5f, .45f, 2.5f), dark);
                    Box(root, "RuinInscription", p + new Vector3(-side * .92f, 4, 0), new(.05f, 1.8f, .22f), light);
                    Box(root, "RuinLintel", p + new Vector3(side * 1.7f, 8.2f, 0), new(5, .8f, 1.8f), stone);
                    Rock(root, p + new Vector3(3, .2f, 3), new(3, 2, 2.5f), stone);
                    break;
                case 1:
                    Cylinder(root, "AncientTrunk", p + new Vector3(0, 5, 0), 1.1f, .55f, 11, dark);
                    for (int canopy = 0; canopy < 3; canopy++)
                        Rock(root, p + new Vector3((canopy - 1) * 2, 9 + canopy % 2, 0), new(4.5f, 2.4f, 4), foliage);
                    Cylinder(root, "Root", p + new Vector3(-side * 2, .5f, 0), .4f, .16f, 6, dark, new(0, 0, side * 1.0f));
                    Rock(root, p + new Vector3(-side * 2, 1, 2), new(1.2f, .7f, 1.3f), light);
                    break;
                case 2:
                    Rock(root, p, new(5, 2, 5), stone);
                    for (int shard = 0; shard < 3; shard++)
                        Cylinder(root, "IceSpire", p + new Vector3(shard * 1.2f, 2 + shard, 0), .85f, 0, 5 + shard * 2, light, new(.15f, .4f, (shard - 1) * .2f), 5);
                    break;
                case 3:
                    float h = random.RandfRange(10, 21);
                    Box(root, "CityBlock", p + new Vector3(0, h * .5f, 0), new(5, h, 7), dark);
                    Box(root, "RoofCap", p + new Vector3(0, h, 0), new(5.5f, .5f, 7.5f), stone);
                    for (int mullion = -1; mullion <= 1; mullion++)
                        Box(root, "FacadeRib", p + new Vector3(-side * 2.56f, h * .5f, mullion * 2.3f), new(.12f, h - .5f, .16f), stone);
                    Cylinder(root, "Antenna", p + new Vector3(1.2f, h + 1.5f, .7f), .08f, .04f, 3, light);
                    for (int window = 0; window < 4; window++)
                        Box(root, "WindowBand", p + new Vector3(-side * 2.55f, 3 + window * 3, 0), new(.06f, .24f, 5), light);
                    Box(root, "NeonSign", p + new Vector3(-side * 3, 8, 2), new(.22f, 3, .7f), light);
                    break;
                case 4:
                    Rock(root, p, new(5, 2.5f, 4), dark);
                    Cylinder(root, "ExhaustStack", p + new Vector3(0, 4, 0), 1.25f, 1.25f, 9, dark);
                    Cylinder(root, "HotVent", p + new Vector3(0, 8.6f, 0), 1.26f, 1.26f, .4f, light);
                    Box(root, "FoundryPipe", p + new Vector3(-side * 2, 2, 0), new(1.1f, 1.1f, 13), stone);
                    break;
                default:
                    Rock(root, p + new Vector3(0, -4, 0), new(6, 3, 6), stone);
                    Cylinder(root, "FloatingIsland", p, 4, 2, 2.6f, foliage);
                    var arch = new TorusMesh { InnerRadius = 2.3f, OuterRadius = 2.6f, Rings = 20, RingSegments = 8 };
                    Mesh(root, "SkyglassArch", arch, p + new Vector3(0, 5, 0), light, new(Mathf.Pi / 2, 0, 0));
                    break;
            }
        }
        return root;
    }

    private static StandardMaterial3D Material(Color color, float rough, float emission = 0) => new()
    { AlbedoColor = color, Roughness = rough, EmissionEnabled = emission > 0, Emission = color, EmissionEnergyMultiplier = emission };
    private static void Box(Node3D root, string name, Vector3 p, Vector3 size, Material material, Vector3 rotation = default) =>
        Mesh(root, name, new BoxMesh { Size = size }, p, material, rotation);
    private static void Rock(Node3D root, Vector3 p, Vector3 size, Material material)
    {
        var node = Mesh(root, "Rock", new SphereMesh { Radius = 1, Height = 2, RadialSegments = 7, Rings = 4 }, p, material);
        node.Scale = size;
    }
    private static void Cylinder(Node3D root, string name, Vector3 p, float bottom, float top, float height,
        Material material, Vector3 rotation = default, int sides = 10) =>
        Mesh(root, name, new CylinderMesh { BottomRadius = bottom, TopRadius = top, Height = height, RadialSegments = sides }, p, material, rotation);
    private static MeshInstance3D Mesh(Node3D root, string name, Mesh mesh, Vector3 position, Material material, Vector3 rotation = default)
    {
        var node = new MeshInstance3D { Name = name, Mesh = mesh, Position = position, Rotation = rotation,
            MaterialOverride = material, CastShadow = GeometryInstance3D.ShadowCastingSetting.On,
            VisibilityRangeEnd = 180, VisibilityRangeEndMargin = 18 };
        root.AddChild(node); return node;
    }
}
