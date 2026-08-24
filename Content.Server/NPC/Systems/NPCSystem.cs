using System.Diagnostics.CodeAnalysis;
using Content.Server.NPC.Components;
using Content.Server.NPC.HTN;
using Content.Shared.CCVar;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC;
using Content.Shared.NPC.Systems;
using Prometheus;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Player;

// RimFortress Start
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Server._RF.NPC.GOAP.Systems;
using Content.Server._RF.NPC.UtilityAi.Systems;
using Content.Shared._RF.NPC.GOAP;
// RimFortress End

namespace Content.Server.NPC.Systems
{
    /// <summary>
    ///     Handles NPCs running every tick.
    /// </summary>
    public sealed partial class NPCSystem : SharedNPCSystem
    {
        private static readonly Gauge ActiveGauge = Metrics.CreateGauge(
            "npc_active_count",
            "Amount of NPCs that are actively processing");

        [Dependency] private readonly IConfigurationManager _configurationManager = default!;
        [Dependency] private readonly HTNSystem _htn = default!;
        [Dependency] private readonly MobStateSystem _mobState = default!;
        // RimFortress Start
        [Dependency] private readonly GoapSystem _goap = default!;
        [Dependency] private readonly UtilityAiSystem _utilityAi = default!;
        // RimFortress End

        /// <summary>
        /// Whether any NPCs are allowed to run at all.
        /// </summary>
        public bool Enabled { get; set; } = true;

        private int _maxUpdates;

        private int _count;

        /// <inheritdoc />
        public override void Initialize()
        {
            base.Initialize();

            Subs.CVar(_configurationManager, CCVars.NPCEnabled, value => Enabled = value, true);
            Subs.CVar(_configurationManager, CCVars.NPCMaxUpdates, obj => _maxUpdates = obj, true);
        }

        public void OnPlayerNPCAttach(EntityUid uid, IComponent component, PlayerAttachedEvent args) // RimFortress
        {
            SleepNPC(uid); // RimFortress
        }

        public void OnPlayerNPCDetach(EntityUid uid, IComponent component, PlayerDetachedEvent args) // RimFortress
        {
            if (_mobState.IsIncapacitated(uid) || TerminatingOrDeleted(uid))
                return;

            // This NPC has an attached mind, so it should not wake up.
            if (TryComp<MindContainerComponent>(uid, out var mindContainer) && mindContainer.HasMind)
                return;

            WakeNPC(uid); // RimFortress
        }

        public void OnNPCStartup(EntityUid uid, HTNComponent component, ComponentStartup args)
        {
            component.Blackboard.SetValue(NPCBlackboard.Owner, uid);
        }

        public void OnNPCMapInit(EntityUid uid, IComponent component, MapInitEvent args) // RimFortress
        {
            WakeNPC(uid); // RimFortress
        }

        public void OnNPCShutdown(EntityUid uid, IComponent component, ComponentShutdown args) // RimFortress
        {
            SleepNPC(uid); // RimFortress
        }

        public override bool IsNpc(EntityUid uid)
        {
            return HasComp<HTNComponent>(uid) || HasComp<GoapComponent>(uid); // RimFortress
        }

        /// <summary>
        /// Is the NPC awake and updating?
        /// </summary>
        public bool IsAwake(EntityUid uid, HTNComponent component, ActiveNPCComponent? active = null)
        {
            return Resolve(uid, ref active, false);
        }

        public bool TryGetNpc(EntityUid uid, [NotNullWhen(true)] out NPCComponent? component)
        {
            // If you add your own NPC components then add them here.

            if (TryComp<HTNComponent>(uid, out var htn))
            {
                component = htn;
                return true;
            }

            component = null;
            return false;
        }

        /// <summary>
        /// Allows the NPC to actively be updated.
        /// </summary>
        public void WakeNPC(EntityUid uid, HTNComponent? component = null)
        {
            /* RimFortress
            if (!Resolve(uid, ref component, false))
            {
                return;
            }
            RimFortress */

            Log.Debug($"Waking {ToPrettyString(uid)}");
            EnsureComp<ActiveNPCComponent>(uid);
        }

        public void SleepNPC(EntityUid uid, HTNComponent? component = null)
        {
            /* RimFortress
            if (!Resolve(uid, ref component, false))
            {
                return;
            }
            RimFortress */

            // Don't bother with an event
            if (TryComp<HTNComponent>(uid, out var htn))
            {
                if (htn.Plan != null)
                {
                    var currentOperator = htn.Plan.CurrentOperator;
                    _htn.ShutdownTask(currentOperator, htn.Blackboard, HTNOperatorStatus.Failed);
                    _htn.ShutdownPlan(htn);
                    htn.Plan = null;
                }
            }

            // RimFortress Start
            if (TryComp<GoapComponent>(uid, out var goap) && goap.Plan is { } plan)
                _goap.PlanShutdown(new(uid, goap), GoapPlanFinishReason.Interrupted);
            // RimFortress End

            Log.Debug($"Sleeping {ToPrettyString(uid)}");
            RemComp<ActiveNPCComponent>(uid);
        }

        /// <inheritdoc />
        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            if (!Enabled)
                return;

            // Add your system here.
            _htn.UpdateNPC(ref _count, _maxUpdates, frameTime);
            // RimFortress Start
            _goap.UpdateNpc(ref _count, _maxUpdates);
            _utilityAi.UpdateNpc(ref _count, _maxUpdates);
            // RimFortress End

            ActiveGauge.Set(Count<ActiveNPCComponent>());
        }

        public void OnMobStateChange(EntityUid uid, IComponent component, MobStateChangedEvent args) // RimFortress
        {
            if (HasComp<ActorComponent>(uid))
                return;

            switch (args.NewMobState)
            {
                case MobState.Alive:
                    WakeNPC(uid); // RimFortress
                    break;
                case MobState.Critical:
                case MobState.Dead:
                    SleepNPC(uid); // RimFortress
                    break;
            }
        }
    }
}
