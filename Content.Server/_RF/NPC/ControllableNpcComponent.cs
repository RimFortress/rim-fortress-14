namespace Content.Server._RF.NPC;

/// <summary>
/// Npc that can be controlled by the player
/// </summary>
[RegisterComponent]
public sealed partial class ControllableNpcComponent : Component
{
    [DataField]
    public List<EntityUid> CanControl = new();
}
