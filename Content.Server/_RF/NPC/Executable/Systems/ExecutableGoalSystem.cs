using Content.Server.Construction;
using Content.Server.Construction.Components;
using Content.Shared._RF.Construction;
using Content.Shared._RF.NPC.Executable.Systems;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.UtilityAi.Components;

namespace Content.Server._RF.NPC.Executable.Systems;

public sealed partial class ExecutableGoalSystem : SharedExecutableGoalSystem
{
    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ConstructionComponent, ConstructionChangeEntityEvent>(OnEntityChange);
        SubscribeLocalEvent<CommonConstructionGhostComponent, ConstructionChangeEntityEvent>(OnEntityChange);
    }

    // Help construction NPCs keep up-to-date information on the entity to be built
    private void OnEntityChange(EntityUid uid, IComponent component, ConstructionChangeEntityEvent ev)
    {
        var enumerator = EntityQueryEnumerator<UtilityAiComponent, GoapComponent>();

        while (enumerator.MoveNext(out var comp, out var goap))
        {
            if (comp.CurrentGoal == null
                || !Executables.TryGetValue(comp.CurrentGoal.Value, out var prototypes))
                continue;

            foreach (var protoId in prototypes)
            {
                if (!Proto.Resolve(protoId, out var proto)
                    || !Goap.TryGetValue(goap.State, proto.TargetKey, out var target)
                    || target != ev.Old)
                    continue;

                goap.State.SetValue(proto.TargetKey, ev.New);
                break;
            }
        }
    }
}
