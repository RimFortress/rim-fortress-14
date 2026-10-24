using System.Linq;
using Content.Client._RF.NPC.UI;
using Content.Client._RF.Stylesheets;
using Content.Client._RF.UserInterface.Controls;
using Content.Client._RF.UserInterface.Scope;
using Content.Shared._RF.NPC.GOAP;
using Robust.Shared.Utility;

namespace Content.Client._RF.NPC.GOAP.UI;

/// <summary>
/// A node of the GOAP graph: the actions, the preconditions and the state of the planning.
/// </summary>
public sealed partial class GraphNodeControl : DebugNodeControl
{
    private readonly GoapStaticGraphNodeDebug _graphNode;
    private List<GoapActionDebugInfo>? _actions;
    private readonly GoapNodeDebugEntry? _debug;

    private AiDevWindowUiController DebugController => Controller<AiDevWindowUiController>();

    public GraphNodeControl(
        GoapStaticGraphNodeDebug graphNode,
        List<GoapActionDebugInfo>? actions = null,
        GoapNodeDebugEntry? debug = null)
    {
        InitializeComponent();
        Title.Text = $"{graphNode.Compound ?? "Node"} #{graphNode.Id}";

        RfTabContainer.SetTabTitle(CommonTab, "Common");
        RfTabContainer.SetTabTitle(ActionsTab, "Actions");
        RfTabContainer.SetTabTitle(PreconditionsTab, "Preconditions");

        RfTabContainer.SetTabVisible(ActionsTab, graphNode.Actions.Count > 0);
        RfTabContainer.SetTabVisible(PreconditionsTab, graphNode.Preconditions.Count > 0);

        _graphNode = graphNode;
        _actions = actions;
        _debug = debug;

        Scope.ListenController<AiDevWindowUiController, (GoapBreakpoint Point, GoapPlanDebugInfo Plan)>(
            (c, h) => c.OnBreakpointHit += h,
            (c, h) => c.OnBreakpointHit -= h,
            OnBreakpointHit);
        Scope.ListenController<AiDevWindowUiController, GoapBreakpoint>(
            (c, h) => c.OnBreakpointAdded += h,
            (c, h) => c.OnBreakpointAdded -= h,
            _ => UpdateStatus());
        Scope.ListenController<AiDevWindowUiController, GoapBreakpoint>(
            (c, h) => c.OnBreakpointRemoved += h,
            (c, h) => c.OnBreakpointRemoved -= h,
            _ => UpdateStatus());

        // ActionsTab
        AddAction();

        // PreconditionsTab
        for (var i = 0; i < graphNode.Preconditions.Count; i++)
        {
            var (condition, entityCon) = graphNode.Preconditions[i];

            // Static graph info
            var box = debug == null
                ? AiUiHelper.AddBox(PreconditionsTab, condition.Name)
                : AiUiHelper.AddBox(PreconditionsTab,
                    condition.Name,
                    debug.Value.Preconditions[i].Result
                        ? $"[color={StyleFortress.LightGood.ToHex()}]True[/color]"
                        : $"[color={StyleFortress.LightBad.ToHex()}]False[/color]");
            AiUiHelper.AddLabel(box.Content, "Type:", entityCon ? "ECS" : "Static");
            AiUiHelper.AddTypes(box.Content, "Fields", condition);

            // Planing debug info
            if (debug == null)
                continue;

            var debugCon = debug.Value.Preconditions[i];
            AiUiHelper.AddTypes(box.Content, "State Snapshot", debugCon.Dump.StateSnapshot.State);
            AiUiHelper.AddLogs(box.Content, debugCon.Dump.Dump);
        }

        // Common
        UpdateStatus();

        AiUiHelper.AddTypes(CommonTab, "Effects", graphNode.EffectsDump.State);

        if (debug == null)
            return;

        AiUiHelper.AddLabel(CommonTab, "Cost:", $"{debug.Value.TaskCost}");
        AiUiHelper.AddTypes(CommonTab, "State Before", debug.Value.StateBefore.State);
        AiUiHelper.AddTypes(CommonTab, "State After", debug.Value.StateAfter?.State);

        if (debug.Value.SkipReason != null)
            AiUiHelper.AddExpandLabel(CommonTab, "Skip Reason",  debug.Value.SkipReason);
    }

    private static string UpdateText(GoapActionResult result)
    {
        return result switch
        {
            GoapActionResult.Finished => $"[color={StyleFortress.LightGood.ToHex()}]Finished[/color]",
            GoapActionResult.Failed => $"[color={StyleFortress.LightBad.ToHex()}]Failed[/color]",
            GoapActionResult.Continuing => $"[color={Color.Yellow.ToHex()}]Continuing[/color]",
            _ => throw new ArgumentOutOfRangeException(),
        };
    }

    private static string? ActionStatusText(GoapActionDebugInfo action)
    {
        if (action.StartupSuccess == null)
            return null;

        if (action.StartupSuccess == false)
            return $"[color={StyleFortress.LightBad.ToHex()}]Fail[/color]";

        if (action.UpdateDumps.Count == 0)
            return $"[color={Color.Yellow.ToHex()}]Not Started[/color]";

        return action.UpdateDumps.Count > 0 ? UpdateText(action.UpdateDumps.Last().Result) : null;
    }

    private void AddAction()
    {
        for (var i = 0; i < _graphNode.Actions.Count; i++)
        {
            var index = i;
            var action = _graphNode.Actions[i];

            if (_actions?.FirstOrNull(x => x.ActionIndex == index) is not { } debugAction)
            {
                // Static graph info
                var staticBox = AiUiHelper.AddBox(ActionsTab, action.Name);
                AiUiHelper.AddTypes(staticBox.Content, "Fields", action);
                continue;
            }

            // Runtime debug info
            var box = AiUiHelper.AddBox(ActionsTab, action.Name, ActionStatusText(debugAction));
            AiUiHelper.AddTypes(box.Content, "Fields", action);

            // Startup
            if (debugAction.StartupSuccess != null)
            {
                var startupBox = AiUiHelper.AddBox(
                    box.Content,
                    "Startup",
                    debugAction.StartupSuccess == true
                        ? $"[color={StyleFortress.LightGood.ToHex()}]Success[/color]"
                        : $"[color={StyleFortress.LightBad.ToHex()}]Fail[/color]");

                if (debugAction.StartupDump != null)
                {
                    AiUiHelper.AddTypes(startupBox.Content,
                        "State Snapshot",
                        debugAction.StartupDump.Value.StateSnapshot.State);
                    AiUiHelper.AddLogs(startupBox.Content, debugAction.StartupDump.Value.Dump);
                }
            }

            // Shutdown
            if (debugAction.ShutdownDump != null)
            {
                var shutdownBox = AiUiHelper.AddBox(box.Content, "Shutdown");

                AiUiHelper.AddTypes(shutdownBox.Content,
                    "State Snapshot",
                    debugAction.ShutdownDump.Value.StateSnapshot.State);
                AiUiHelper.AddLogs(shutdownBox.Content, debugAction.ShutdownDump.Value.Dump);
            }

            // Plan Shutdown
            if (debugAction.ShutdownPlanDump != null)
            {
                var shutdownBox = AiUiHelper.AddBox(box.Content, "Plan Shutdown");

                AiUiHelper.AddTypes(shutdownBox.Content,
                    "State Snapshot",
                    debugAction.ShutdownPlanDump.Value.StateSnapshot.State);
                AiUiHelper.AddLogs(shutdownBox.Content, debugAction.ShutdownPlanDump.Value.Dump);
            }

            // Updates
            if (debugAction.UpdateDumps.Count == 0)
                continue;

            var updateBox =
                AiUiHelper.AddBox(box.Content, "Updates", UpdateText(debugAction.UpdateDumps.Last().Result));

            foreach (var update in debugAction.UpdateDumps)
            {
                var upd = AiUiHelper.AddBox(updateBox.Content, update.Tick.ToString(), UpdateText(update.Result));
                AiUiHelper.AddTypes(upd.Content, "State Snapshot", update.Dump.StateSnapshot.State);
                AiUiHelper.AddLogs(upd.Content, update.Dump.Dump);
            }
        }
    }

    private void UpdateStatus()
    {
        SearchText.Clear();
        SearchText.Add(_graphNode.Compound ?? "Node");
        SearchText.Add(_graphNode.Id.ToString());

        StatusLabel.Text = string.Empty;
        SubTitle.Text = string.Empty;

        if (DebugController.GoapBreakpoints.Any(x => x.NodeId == _graphNode.Id))
        {
            SubTitle.Text = $@"[color={StyleFortress.LightBad.ToHex()}]\[BP\][/color]";
            SearchText.Add("[BP]");
        }

        if (_debug?.IndexInPlan is { } index)
            SubTitle.Text = $"{StatusLabel.Text} [color={Color.Aqua.ToHex()}]#{index}[/color]";

        if (_debug is { HelpGoal: true })
        {
            StatusLabel.Text = $@"[color={StyleFortress.GoldFortress.ToHex()}]\[GOAL\][/color]";
            SearchText.Add("[GOAL]");
        }

        if (_actions?.Count > 0)
        {
            StatusLabel.Text = $@"{StatusLabel.Text} [color={Color.Aqua.ToHex()}]\[PLAN\][/color]";

            if (_actions.All(x => x.StartupSuccess == null))
            {
                StatusLabel.Text = $@"{StatusLabel.Text} [color={Color.Yellow.ToHex()}]\[START\][/color]";
                SearchText.Add("[START]");
            }
            else if (_actions.Any(x
                    => x.UpdateDumps.Any(y
                           => y.Result == GoapActionResult.Failed)
                       || x.StartupSuccess == false))
            {
                StatusLabel.Text = $@"{StatusLabel.Text} [color={Color.BetterViolet.ToHex()}]\[FAIL\][/color]";
                SearchText.Add("[FAIL]");
            }
            else if (_actions.Any(x
                    => x.StartupSuccess == null || x.UpdateDumps.Any(y
                        => y.Result == GoapActionResult.Continuing)))
            {
                StatusLabel.Text = $@"{StatusLabel.Text} [color={Color.Yellow.ToHex()}]\[CONT\][/color]";
                SearchText.Add("[CONT]");
            }
            else if (_actions.Count > 0
                     && _actions.All(x
                         => x.UpdateDumps.Any(y
                             => y.Result == GoapActionResult.Finished)))
            {
                StatusLabel.Text = $@"{StatusLabel.Text} [color={StyleFortress.Good.ToHex()}]\[FINISH\][/color]";
                SearchText.Add("[FINISH]");
            }
        }
        else
        {
            switch (_debug?.PreconditionsMet)
            {
                case true:
                    StatusLabel.Text = $@"{StatusLabel.Text} [color={StyleFortress.LightBad.ToHex()}]\[NOT MET\][/color]";
                    SearchText.Add("[NOT MET]");
                    break;
                case false:
                    StatusLabel.Text = $@"{StatusLabel.Text} [color={StyleFortress.LightGood.ToHex()}]\[MET\][/color]";
                    SearchText.Add("[MET]");
                    break;
            }
        }

        SubTitle.Text = $"[bold]{SubTitle.Text.Trim()}[/bold]";
        StatusLabel.Visible = !string.IsNullOrWhiteSpace(StatusLabel.Text);
        StatusLabel.Text = $"[bold]{StatusLabel.Text.Trim()}[/bold]";
    }

    private void OnBreakpointHit((GoapBreakpoint Point, GoapPlanDebugInfo Plan) args)
    {
        if (args.Point.NodeId == _graphNode.Id)
            TabContainer.CurrentTab = 1;

        _actions = args.Plan.Actions
            .Where(x => x.NodeIndex == _graphNode.Id)
            .ToList();

        ActionsTab.RemoveAllChildren();
        AddAction();
        UpdateStatus();
    }
}
