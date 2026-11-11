namespace Content.Shared._RF.World.Settlements;

/// <summary>
/// A pop that lives in a settlement.
/// </summary>
[RegisterComponent]
[Access(typeof(SharedSettlementSystem))]
public sealed partial class SettlementResidentComponent : Component
{
    /// <summary>
    /// The settlement the pop lives in.
    /// </summary>
    [ViewVariables]
    public EntityUid Settlement;
}
