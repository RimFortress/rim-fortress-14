using Content.Server._RF.NPC.Prototypes;
using Content.Server._RF.Workshops.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.Workshops.Components;

/// <summary>
/// A workshop with this component will create a passive NPC task every time there are uncompleted recipes in the queue.
/// </summary>
[RegisterComponent, Access(typeof(WorkshopSystem))]
public sealed partial class WorkshopTaskSourceComponent : Component
{
    /// <summary>
    /// The task that will be given for crafting the recipe.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<NpcTaskPrototype> Task;

    /// <summary>
    /// The key to which the target recipe will be saved in Blackboard when production begins.
    /// </summary>
    [DataField]
    public string TargetRecipeKey = "TargetRecipe";

    /// <summary>
    /// Will the recipe crafting be suspended if the task fails.
    /// </summary>
    [DataField]
    public bool SuspendOnFail = true;
}
