using Content.Shared.Parallax.Biomes;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.GameTicking.Rules;

/// <summary>
/// Basic game rule of RimFortress
/// </summary>
[RegisterComponent]
public sealed partial class RimFortressRuleComponent : Component
{
    [DataField]
    public string PlayerProtoId = "RimFortressObserver";

    [DataField]
    public ProtoId<BiomeTemplatePrototype> BaseBiomeTemplate = "Continental";

    [DataField]
    public float MaxPlanetLoadDistance = 100f;

    [DataField]
    public string PlanetBorderProtoId = "GhostImpassableWall";
}
