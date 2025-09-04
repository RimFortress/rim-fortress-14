using Content.Server.Stack;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.EntityEffects.Effects;

/// <summary>
/// Changes the amount of material in the stack
/// </summary>
public sealed partial class ChangeStack : EntityEffect
{
    /// <summary>
    /// Amount of material to be changed to
    /// </summary>
    [DataField]
    public int Amount;

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) => null;

    public override void Effect(EntityEffectBaseArgs args)
    {
        var stack = args.EntityManager.System<StackSystem>();
        stack.SetCount(args.TargetEntity, stack.GetCount(args.TargetEntity) + Amount);
    }
}
