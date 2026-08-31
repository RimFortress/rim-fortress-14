using Robust.Shared.Serialization;

namespace Content.Shared._RF.NPC;

[Serializable, NetSerializable]
public record ObjectDebugReflection
{
    public required string Name { get; init; }
    public required string TypeName { get; init; }
    public Dictionary<string, (string Type, string Value)> Fields { get; init; } = new();
    public List<ObjectDebugReflection> Children { get; init; } = new();
}
