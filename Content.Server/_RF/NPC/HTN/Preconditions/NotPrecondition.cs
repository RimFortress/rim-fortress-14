using Content.Server.NPC;
using Content.Server.NPC.HTN.Preconditions;

namespace Content.Server._RF.NPC.HTN.Preconditions;

/// <summary>
/// Inverts the result of another check.
/// Used for checks that do not implement <see cref="InvertiblePrecondition"/>
/// </summary>
public sealed partial class NotPrecondition : HTNPrecondition
{
    [DataField(required: true)]
    public HTNPrecondition Precondition;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        Precondition.Initialize(sysManager);
    }

    public override bool IsMet(NPCBlackboard blackboard) => !Precondition.IsMet(blackboard);
}
