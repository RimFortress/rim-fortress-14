namespace Content.Shared._RF.NPC;

/// <summary>
/// This is used to refer to an entity that is owned by another entity
/// </summary>
[RegisterComponent, Access(typeof(SharedOwnedSystem))]
public sealed partial class OwnedComponent : Component
{
    [ViewVariables]
    public List<EntityUid> Owners = new();
}
