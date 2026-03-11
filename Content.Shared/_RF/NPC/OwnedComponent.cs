using Robust.Shared.GameStates;

namespace Content.Shared._RF.NPC;

/// <summary>
/// This is used to refer to an entity that is owned by another entity
/// </summary>
[Access(typeof(OwnedSystem))]
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class OwnedComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public List<EntityUid> Owners = new();
}
