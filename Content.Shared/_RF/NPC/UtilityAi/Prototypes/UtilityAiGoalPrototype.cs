using Content.Shared._RF.MathHelpers.MathCurve;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search.Components;
using Content.Shared._RF.NPC.Search.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array;

namespace Content.Shared._RF.NPC.UtilityAi.Prototypes;

/// <summary>
/// A prototype of a goal that can be selected by a GOAP agent for completion.
/// </summary>
[Prototype]
public sealed partial class UtilityAiGoalPrototype : IPrototype, IInheritingPrototype
{
    /// <inheritdoc />
    [ParentDataField(typeof(AbstractPrototypeIdArraySerializer<UtilityAiGoalPrototype>))]
    public string[]? Parents { get; private set; }

    /// <inheritdoc />
    [AbstractDataField, NeverPushInheritance]
    public bool Abstract { get; private set; }

    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Human-readable goal name.
    /// </summary>
    [ViewVariables]
    public LocId Name => $"utility-ai-goal-{ID.ToLowerInvariant()}-name";

    /// <summary>
    /// The color associated with this goal for display in the UI.
    /// </summary>
    [DataField]
    public Color Color = Color.White;

    /// <summary>
    /// The conditions required to achieve this goal.
    /// If these conditions are not met, the score will not even be calculated.
    /// </summary>
    [DataField]
    public List<GoapCondition> Conditions = new();

    /// <summary>
    /// A mathematical function based on curves for calculating goal score.
    /// </summary>
    /// <remarks>
    /// The function must return a normalized result (between 0 and 1);
    /// otherwise, the result will simply be clipped.
    /// </remarks>
    [DataField(serverOnly: true)]
    public List<MathCurve> ScoreCurves = new();

    /// <summary>
    /// Curves that will be applied in score calculation after this goal is successfully completed.
    /// </summary>
    [DataField(serverOnly: true)]
    public List<MathCurve> IncumbentBonus = new();

    /// <summary>
    /// Goals for the agent to achieve.
    /// </summary>
    [DataField(serverOnly: true)]
    public GoapState GoalState = new();

    /// <summary>
    /// Fallback goals that will be assigned if the current goal cannot be completed or planned.
    /// </summary>
    /// <remarks>
    /// Score will not be calculated for this goal, but the conditions will still be checked;
    /// If none of the fallback conditions are met, the <see cref="FailPolicy"/> will be performed.
    /// </remarks>
    [DataField]
    public List<ProtoId<UtilityAiGoalPrototype>> Fallbacks = new();

    /// <inheritdoc cref="UtilityAiFailPolicy"/>
    [DataField]
    public UtilityAiFailPolicy FailPolicy = UtilityAiFailPolicy.Penalty;

    /// <summary>
    /// The duration for which the goal will be unavailable after a failure,
    /// when <see cref="FailPolicy"/> == <see cref="UtilityAiFailPolicy.Cooldown"/>.
    /// </summary>
    [DataField]
    public TimeSpan FailCooldown = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The penalty applied when calculating the goal's score after a failure,
    /// when <see cref="FailPolicy"/> == <see cref="UtilityAiFailPolicy.Penalty"/>.
    /// </summary>
    [DataField]
    public float FailPenalty = 0.2f;

    /// <summary>
    /// Keys that will be deleted when the goal is completed/failed.
    /// </summary>
    [DataField]
    public HashSet<string> TempKeys = new();

    /// <summary>
    /// Keys whose values will be captured when the goal's execution starts and released when it finishes.
    /// If the value of any key is not found, or if it cannot be captured,
    /// the goal will still start if the <see cref="Conditions"/> are met.
    /// </summary>
    /// <seealso cref="SharedNpcSearcherSystem.CaptureResult(Entity{SearchTrackedComponent?}, EntityUid)"/>
    /// <seealso cref="SharedNpcSearcherSystem.ReleaseCapturedResult(Entity{SearchTrackedComponent?}, EntityUid)"/>
    [DataField]
    public HashSet<StateKey<EntityUid>> Capture = new();
}

/// <summary>
/// Policy on how to proceed when planning or goal completion fails.
/// </summary>
[Serializable, NetSerializable]
public enum UtilityAiFailPolicy : byte
{
    /// <summary>
    /// The task will be unavailable for a while.
    /// </summary>
    Cooldown,

    /// <summary>
    /// The goal will be penalized during the next score calculation.
    /// </summary>
    Penalty,
}
