using Robust.Shared.Prototypes;

namespace Content.Shared._RF.NPC.GOAP.Prototypes;

/// <summary>
/// A prototype that stores a set of GOAP actions.
/// </summary>
[Prototype]
public sealed partial class GoapCompoundPrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public List<GoapTask> Tasks = new();
}
