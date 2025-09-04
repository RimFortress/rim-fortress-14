using Robust.Shared.Serialization;

namespace Content.Shared._RF.Info;

[Serializable, NetSerializable]
public sealed class EntityHealthInfoRequest(NetEntity uid) : EntityEventArgs
{
    public NetEntity Uid = uid;
}

[Serializable, NetSerializable]
public sealed class EntityHealthInfoResponse(
    NetEntity uid,
    float temperature,
    float coldDamageThreshold,
    float heatDamageThreshold,
    float bloodLevel,
    bool bleeding) : EntityEventArgs
{
    public NetEntity? Uid = uid;
    public float Temperature = temperature;
    public float ColdDamageThreshold = coldDamageThreshold;
    public float HeatDamageThreshold = heatDamageThreshold;
    public float BloodLevel = bloodLevel;
    public bool Bleeding = bleeding;
}
