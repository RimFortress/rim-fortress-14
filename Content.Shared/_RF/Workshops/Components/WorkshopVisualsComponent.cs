using Robust.Shared.Serialization;

namespace Content.Shared._RF.Workshops.Components;

/// <summary>
/// Client-side visual configuration for workshop entities.
/// Used by WorkshopVisualizerSystem to determine how the workshop
/// should look depending on:<br/>
/// - Whether it is currently crafting.<br/>
/// - How many items are stored inside.
/// </summary>
[RegisterComponent]
public sealed partial class WorkshopVisualsComponent : Component
{
    /// <summary>
    /// RSI state used for the base layer when the workshop is idle.
    /// </summary>
    /// <remarks>
    /// Applied when <see cref="WorkshopVisualsState.Crafting"/> is false.
    /// </remarks>
    [DataField]
    public string IdleBaseState = "base";

    /// <summary>
    /// RSI state used for the base layer while the workshop is crafting.
    /// </summary>
    /// <remarks>
    /// Applied when <see cref="WorkshopVisualsState.Crafting"/> is true.
    /// </remarks>
    [DataField]
    public string CraftingBaseState = "base_crafting";

    /// <summary>
    /// Ordered list of visual stages based on item count inside the workshop.
    /// </summary>
    /// <remarks>
    /// Each stage defines:<br/>
    /// - Minimum threshold condition (see <see cref="WorkshopVisualStage.Threshold"/>)<br/>
    /// - Sprite states for idle and crafting modes<br/>
    /// - Visibility of the items layer<br/>
    ///
    /// The visualizer selects the first stage where:
    /// <code>Threshold >= current item count</code>
    ///
    /// If no such stage exists, the last (highest threshold) stage is used.<br/>
    ///
    /// IMPORTANT:
    /// Stages should be configured in ascending order of <see cref="WorkshopVisualStage.Threshold"/>.
    /// </remarks>
    [DataField]
    public List<WorkshopVisualStage> Stages = new();
}

/// <summary>
/// Describes a single visual stage of the workshop depending on item count.
/// </summary>
[DataDefinition]
public sealed partial class WorkshopVisualStage
{
    /// <summary>
    /// Upper bound threshold for this stage.
    /// </summary>
    /// <remarks>
    /// This stage is selected when:
    /// <code>itemCount &lt;= Threshold</code>
    ///
    /// Example:<br/>
    /// - Threshold = 4 → used for 0–4 items<br/>
    /// - Threshold = 8 → used for 5–8 items
    /// </remarks>
    [DataField(required: true)]
    public int Threshold;

    /// <summary>
    /// RSI state for the items layer when the workshop is idle.
    /// </summary>
    /// <remarks>
    /// If null, the items layer state will not be changed.
    /// </remarks>
    [DataField]
    public string? IdleState;

    /// <summary>
    /// RSI state for the items layer while the workshop is crafting.
    /// </summary>
    /// <remarks>
    /// If null, the items layer state will not be changed.
    /// </remarks>
    [DataField]
    public string? CraftingState;

    /// <summary>
    /// Whether the items layer should be visible in this stage.
    /// </summary>
    /// <remarks>
    /// This applies regardless of crafting state.
    /// </remarks>
    [DataField]
    public bool Visible = true;
}

[Serializable, NetSerializable]
public enum WorkshopVisualsState : byte
{
    Crafting,
    Items,
}

[Serializable, NetSerializable]
public enum WorkshopLayers : byte
{
    Base,
    Items,
}
