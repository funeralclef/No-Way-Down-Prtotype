using Godot;
using System;

public sealed record RunBiomeDefinition(string Name, string Weather, Color Sky, Color Fog, Color Accent,
    float FogDensity, Vector3 Wind, bool Rain, bool Snow);

/// <summary>One environment identity per arena chapter, shared by scenery and weather.</summary>
public static class RunBiomeCatalog
{
    private static readonly RunBiomeDefinition[] Definitions =
    {
        new("DUSTBOUND RUINS", "SAND / CROSSWIND", new("#b88661"), new("#d2ae7b"), new("#ecaa62"), .006f, new(2, -.4f, .6f), false, false),
        new("VERDANT VAULT", "RAIN / SPORES", new("#29534c"), new("#5b8e73"), new("#6bcba6"), .009f, new(.7f, -12, .5f), true, false),
        new("FROSTFALL RIDGE", "SNOW / DRIFT", new("#829caf"), new("#b2c6d1"), new("#a6e4f0"), .008f, new(1.8f, -1.7f, .5f), false, true),
        new("RAINLINE DISTRICT", "RAIN / NEON HAZE", new("#17233c"), new("#374a65"), new("#a58be3"), .007f, new(-1, -17, .3f), true, false),
        new("CINDER WORKS", "ASH / UPDRAFT", new("#56342e"), new("#8c5c46"), new("#f09b55"), .010f, new(.8f, 1.2f, .3f), false, false),
        new("SKYGLASS ISLES", "MIST / HIGH WIND", new("#6f9eb0"), new("#c1d9da"), new("#8ce4df"), .0045f, new(3.2f, .1f, .5f), false, false)
    };
    public static int Count => Definitions.Length;
    public static RunBiomeDefinition Get(int index) => Definitions[Mathf.PosMod(index, Count)];
}
