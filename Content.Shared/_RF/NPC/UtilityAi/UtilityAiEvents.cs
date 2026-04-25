using Content.Shared._RF.NPC.UtilityAi.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.NPC.UtilityAi;

/// <summary>
/// An event triggered to allow other systems to modify the Utility AI goal score.
/// </summary>
/// <param name="Goal">Utility AI goal prototype.</param>
/// <param name="Score">Goal score.</param>
[ByRefEvent]
public record struct UtilityAiGoalScoreModify(ProtoId<UtilityAiGoalPrototype> Goal, float Score);
