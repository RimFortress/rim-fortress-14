using Robust.Shared.Serialization;

namespace Content.Shared._RF.GameTicking.Rules;

/// <summary>
/// Gradually changes the air temperature of the entire world to a given temperature
/// </summary>
[RegisterComponent]
public sealed partial class TemperatureRuleComponent : Component
{
    /// <summary>
    /// Target world temperature
    /// </summary>
    [DataField]
    public float TargetTemperature = 293.15f;

    [ViewVariables]
    public float DefaultTemp;
}

[Serializable, NetSerializable]
public sealed class WorldTemperatureChangedMessage(NetEntity worldEntity, float temperature) : EntityEventArgs
{
    public NetEntity WorldEntity = worldEntity;
    public float Temperature = temperature;
}
