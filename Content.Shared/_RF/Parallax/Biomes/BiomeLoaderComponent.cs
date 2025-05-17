namespace Content.Shared._RF.Parallax.Biomes;

/// <summary>
/// An entity with this component will load biome chunks within a given radius
/// </summary>
[RegisterComponent]
public sealed partial class BiomeLoaderComponent : Component
{
    [DataField]
    public float Radius = 28f;
}
