using Robust.Shared.Prototypes;

namespace Content.Shared._RF.Zoning.Components;

/// <summary>
/// An entity with this component can create and modify created zones.
/// </summary>
[RegisterComponent]
public sealed partial class ZoneCreatorComponent : Component
{
    /// <summary>
    /// Prototypes of zones that an entity can create/modify.
    /// </summary>
    [DataField]
    public List<EntProtoId<ZoneComponent>> Zones = new();
}
