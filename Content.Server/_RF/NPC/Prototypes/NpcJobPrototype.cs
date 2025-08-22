using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Server._RF.NPC.Prototypes;

/// <summary>
/// A prototype of an NPC's job that he can do passively, without instructions from the player
/// </summary>
[Prototype]
public sealed partial class NpcJobPrototype : IPrototype, ISerializationHooks
{
    private ILocalizationManager _loc = default!;

    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; } = default!;

    [DataField("name")]
    private string _name = string.Empty;

    public string Name => _loc.TryGetString(_name, out var value) ? value : _name;

    /// <summary>
    /// The icon for this job.
    /// </summary>
    [DataField("icon")]
    private string? _icon;

    public SpriteSpecifier.Texture? Icon
        => _icon != null ? new SpriteSpecifier.Texture(new(_icon)) : null;

    /// <summary>
    /// Tasks included in this job
    /// </summary>
    [DataField]
    public List<ProtoId<NpcTaskPrototype>> Tasks = new();

    /// <summary>
    /// The original priority of this job
    /// </summary>
    [DataField]
    public int DefaultPriority = 1;

    /// <summary>
    /// Whether the job is hidden from the user.
    /// The priority of a hidden job cannot be configured by the user
    /// </summary>
    [DataField]
    public bool Hidden;

    /// <inheritdoc/>
    void ISerializationHooks.AfterDeserialization()
    {
        _loc = IoCManager.Resolve<ILocalizationManager>();
    }
}
