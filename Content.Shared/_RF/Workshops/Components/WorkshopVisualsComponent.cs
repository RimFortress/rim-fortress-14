using Robust.Shared.Serialization;

namespace Content.Shared._RF.Workshops.Components;

[RegisterComponent]
public sealed partial class WorkshopVisualsComponent : Component
{
    /// <summary>
    /// List of item display states for different numbers of items.
    /// </summary>
    [DataField]
    public Dictionary<string, int> ItemsVisualStates = new();
}

[Serializable, NetSerializable]
public enum WorkshopVisuals : byte
{
    Idle,
    Crafting,
}

[Serializable, NetSerializable]
public enum WorkshopLayers : byte
{
    Base,
    Items,
}
