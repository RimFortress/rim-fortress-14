using System.Linq;
using System.Numerics;
using Content.Client._RF.NPC.UI;
using Content.Client._RF.Stylesheets;
using Content.Client._RF.Suil;
using Content.Client._RF.UserInterface.Controls;
using Content.Client._RF.UserInterface.Scope;
using Content.Shared._RF.NPC;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Prototypes;
using Content.Shared.Input;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._RF.NPC.GOAP.UI;

/// <summary>
/// The tab of the GOAP graph of an NPC: the nodes, the plan and the breakpoints.
/// </summary>
public sealed partial class GoapDebugTab : UiElement
{
    [Dependency] private IClipboardManager _clipboard = default!;

    private readonly Dictionary<GoapBreakpoint, BreakpointControl> _points = new();
    private readonly Dictionary<ProtoId<GoapCompoundPrototype>, List<Control>> _compounds = new();
    private readonly Dictionary<ProtoId<GoapCompoundPrototype>, bool> _compoundSettings = new();

    private GoapStaticGraphDebug? _graph;
    private GoapPlanDebugInfo? _plan;
    private EntityUid? _target;

    private AiDevWindowUiController DebugController => Controller<AiDevWindowUiController>();

    public GoapDebugTab()
    {
        IoCManager.InjectDependencies(this);
        InitializeComponent();

        Graph.NodeSize = new(350, 400);
        Graph.ZoomStep = 0.05f;
        Graph.EdgeColor = ConnectionColor;

        LayoutContainer.SetAnchorAndMarginPreset(PlanPanel, LayoutContainer.LayoutPreset.BottomRight, margin: 50);
        LayoutContainer.SetAnchorAndMarginPreset(BreakpointsPanel, LayoutContainer.LayoutPreset.TopLeft, margin: 50);

        NodeId.OnItemSelected += args => SelectNodeId((int) args.Value!);
        PointType.OnItemSelected += args => SelectPointType((GoapBreakpointKind) args.Value!);
        ActionIndex.OnItemSelected += _ => UpdateConfirm();
        Result.OnItemSelected += _ => UpdateConfirm();

        Confirm.OnPressed += _ =>
        {
            if (_graph == null
                || _target == null
                || NodeId.SelectedValue is not int nodeId
                || ActionIndex.SelectedValue is not int index
                || PointType.SelectedValue is not GoapBreakpointKind kind
                || Result.SelectedValue is not GoapBreakpointResultKind result)
                return;

            DebugController.AddBreakpoint(_target.Value, nodeId, index, kind, result);
            CreatePointBox.Visible = false;
            AddPointButton.Visible = true;
        };

        Cancel.OnPressed += _ =>
        {
            CreatePointBox.Visible = false;
            AddPointButton.Visible = true;
        };

        AddPointButton.OnPressed += _ =>
        {
            CreatePointBox.Visible = true;
            AddPointButton.Visible = false;
            UpdateConfirm();
        };

        Scope.ListenController<AiDevWindowUiController, GoapBreakpoint>(
            (c, h) => c.OnBreakpointAdded += h,
            (c, h) => c.OnBreakpointAdded -= h,
            OnBreakpointAdded);
        Scope.ListenController<AiDevWindowUiController, GoapBreakpoint>(
            (c, h) => c.OnBreakpointRemoved += h,
            (c, h) => c.OnBreakpointRemoved -= h,
            OnBreakpointRemoved);
        Scope.ListenController<AiDevWindowUiController, (GoapBreakpoint Point, GoapPlanDebugInfo Plan)>(
            (c, h) => c.OnBreakpointHit += h,
            (c, h) => c.OnBreakpointHit -= h,
            OnBreakpointHit);

        // The breakpoints belong to the tab: they are removed from the NPC when it is closed.
        Scope.Listen(() => { }, () =>
        {
            foreach (var (point, _) in _points)
            {
                DebugController.RemoveBreakpoint(point);
            }

            if (_target != null)
                DebugController.Unsubscribe(_target.Value);
        });
    }

    public void SetTarget(EntityUid target)
    {
        if (_target != null && _target != target)
            DebugController.Unsubscribe(_target.Value);

        DebugController.RequestGoapDebug(target);
        _target = target;
    }

    public void Update(GoapStaticGraphDebug graphDebug, GoapPlanDebugInfo? plan)
    {
        _graph = graphDebug;
        _plan = plan;
        _compounds.Clear();

        var firstUpdate = Graph.BeginUpdate(GraphLayoutBuilder.Build(graphDebug, Graph.NodeSize));

        foreach (var node in graphDebug.Nodes)
        {
            var planNode = GetNode(node.Id);
            var actions = plan?.Actions
                .Where(x => x.NodeIndex == node.Id)
                .ToList();

            var control = new GraphNodeControl(node, actions, planNode);
            control.OnMouseEntered += _ =>
            {
                if (string.IsNullOrWhiteSpace(Graph.SearchText))
                    SelectNode(node.Id);
            };
            control.OnMouseExited += _ =>
            {
                if (string.IsNullOrWhiteSpace(Graph.SearchText))
                    Graph.DeselectNodes();
            };
            control.OnKeyBindUp += args => NodeClick(node, args);

            Graph.AddNode(node.Id, control);

            if (node.Compound == null)
                continue;

            _compoundSettings.TryAdd(node.Compound.Value, true);
            control.Visible = _compoundSettings[node.Compound.Value];

            if (!_compounds.TryAdd(node.Compound.Value, new() { control }))
                _compounds[node.Compound.Value].Add(control);
        }

        Graph.EndUpdate(firstUpdate);

        // BreakpointsPanel
        if (NodeId.Items.Count != graphDebug.Nodes.Count + 1)
        {
            NodeId.Clear();
            PointType.Visible = false;
            ActionIndex.Visible = false;
            Result.Visible = false;
            Confirm.Disabled = true;
            NodeId.AddItem("Any", -1);

            foreach (var node in graphDebug.Nodes)
            {
                NodeId.AddItem(
                    (graphDebug.Nodes.FirstOrNull(x => x.Id == node.Id)?.Compound ?? "Node") + $" #{node.Id}",
                    node.Id);
            }
        }

        // PlanPanel
        PlanPanel.Visible = plan != null;

        if (plan == null)
            return;

        TotalCost.Text = $"[bold]Total Cost: {plan.Value.TotalCost}[/bold]";
        ConditionsChecked.Text = $"[bold]Conditions Checked: {plan.Value.ConditionsChecked}[/bold]";
        PlanningNodes.Text = $"[bold]Available Nodes: {graphDebug.Nodes.Count}[/bold]";
        NodesInPlan.Text = "[bold]Nodes in Plan: "
                           + graphDebug.Nodes.Count(x
                               => plan.Value.Actions.Any(y
                                   => y.NodeIndex == x.Id))
                           + "[/bold]";
        NodesExpanded.Text = $"[bold]Nodes Expanded: {plan.Value.NodesExpanded}[/bold]";
        Planning.Text = plan.Value.Success
            ? $"[bold]Planning: [color={StyleFortress.LightGood.ToHex()}]Success[/color][/bold]"
            : $"[bold]Planning: [color={StyleFortress.LightBad.ToHex()}]Failed[/color][/bold]";
        ElapsedTime.Text = $"[bold]Elapsed Time: {plan.Value.ElapsedTime.TotalMilliseconds}ms[/bold]";
        AddTypes(StartState, plan.Value.StartState);
        AddTypes(GoalState, plan.Value.GoalState);

        return;

        void AddTypes(ExpandableBox parent, GoapStateDebugDump data)
        {
            parent.Content.RemoveAllChildren();

            if (data.State.Count == 0)
                return;

            foreach (var (key, (type, value)) in data.State)
            {
                parent.Content.AddChild(new AiSeparator());
                parent.Content.AddChild(new AiFieldRow(key, type, value));
            }
        }

        GoapNodeDebugEntry? GetNode(int id)
        {
            if (plan == null)
                return null;

            var nodes = plan.Value.Nodes
                .Where(x => x.NodeId == id)
                .ToList();
            return nodes.FirstOrNull(x => x.InPlan)
                   ?? nodes.FirstOrNull(x => x.HelpGoal)
                   ?? nodes.OrderByDescending(x
                           => x.Preconditions.Count(y => y.Result))
                       .FirstOrNull();
        }
    }

    private void SelectNode(int id)
    {
        var highlight = new HashSet<int>();
        var queue = new Queue<int>();
        queue.Enqueue(id);

        while (queue.TryDequeue(out var nodeId))
        {
            if (!highlight.Add(nodeId))
                continue;

            if (_plan != null)
            {
                foreach (var child in _plan.Value.Nodes.Where(x => x.FromNodeId == nodeId))
                {
                    queue.Enqueue(child.NodeId);
                }
            }

            if (_graph != null)
            {
                foreach (var edge in _graph.Value.Edges.Where(x => x.ToNodeId == nodeId))
                {
                    queue.Enqueue(edge.FromNodeId);
                }
            }
        }

        Graph.SelectNodes(highlight);
    }

    private void NodeClick(GoapStaticGraphNodeDebug node, GUIBoundKeyEventArgs args)
    {
        if (CreatePointBox.Visible && args.Function == EngineKeyFunctions.UIClick)
            SelectNodeId(node.Id);

        if (args.Function == ContentKeyFunctions.MouseMiddle)
            Graph.ToggleExpanded(node.Id);

        if (args.Function != EngineKeyFunctions.UIRightClick)
            return;

        if (_target is not { } uid)
            return;

        (GoapBreakpointKind Kind, GoapBreakpointResultKind[] Results)[] kinds =
        {
            (GoapBreakpointKind.ActionStartup,
                new[] { GoapBreakpointResultKind.True, GoapBreakpointResultKind.False }),
            (GoapBreakpointKind.ActionUpdate,
                new[]
                {
                    GoapBreakpointResultKind.Finished, GoapBreakpointResultKind.Continuing,
                    GoapBreakpointResultKind.Failed,
                }),
        };

        var existing = DebugController.GoapBreakpoints
            .Where(p => p.NodeId == node.Id || p.NodeId == -1)
            .ToList();
        var nodeActions = _plan?.Actions
            .Where(x => x.NodeIndex == node.Id)
            .ToList();
        var menu = ContextMenuBuilder
            .New(Root!.ModalRoot)
            .Submenu("Add breakpoint",
                r => r
                    .Submenus(node.Actions,
                        act => act.Name,
                        (b, _, actIndex) => b
                            .Submenus(kinds,
                                k => PointTypeString(k.Kind),
                                (s, k) => s.Items(k.Results,
                                    res => res.ToString(),
                                    result => DebugController.AddBreakpoint(uid, node.Id, actIndex, k.Kind, result)))
                            .Item("Shutdown",
                                () => DebugController.AddBreakpoint(uid,
                                    node.Id,
                                    actIndex,
                                    GoapBreakpointKind.ActionShutdown,
                                    GoapBreakpointResultKind.None))
                            .Item("Plan Shutdown",
                                () => DebugController.AddBreakpoint(uid,
                                    node.Id,
                                    actIndex,
                                    GoapBreakpointKind.ActionPlanShutdown,
                                    GoapBreakpointResultKind.None)))
                    .Submenu("Planning",
                        s => s.Items(new[] { GoapBreakpointResultKind.True, GoapBreakpointResultKind.False },
                            result => result == GoapBreakpointResultKind.True ? "Success" : "Failure",
                            result => DebugController.AddBreakpoint(uid,
                                node.Id,
                                -1,
                                GoapBreakpointKind.Planning,
                                result))))
            .When(nodeActions is { Count: > 0 },
                r => r.Submenu("Copy logs",
                    b => b.Items(nodeActions!,
                        act => node.Actions[act.ActionIndex].Name,
                        act => _clipboard.SetText(act.GetLogsString()))))
            .When(existing.Count > 0,
                r => r.Submenu("Remove breakpoint",
                    b => b
                        .Items(existing, GetName, point => DebugController.RemoveBreakpoint(point))))
            .Build();

        menu.Open(UIBox2.FromDimensions(args.PointerLocation.Position / UIScale, new Vector2(1, 1)));
    }

    private Color ConnectionColor((int From, int To) fromTo)
    {
        if (_graph?.Nodes.FirstOrNull(x => x.Id == fromTo.From) is not { Compound: { } compound } node)
            return Color.DimGray; // Default color

        // Some magic
        var hash = (uint) (compound.GetHashCode() + node.Id.GetHashCode());
        hash = (hash ^ (hash >> 16)) * 0x45d9f3b;
        hash = (hash ^ (hash >> 16)) * 0x45d9f3b;
        hash ^= hash >> 16;
        var hue = hash / (float) uint.MaxValue;
        return Color.FromHsv(new(hue, 0.5f, 0.6f, 1f));
    }

    private void SelectNodeId(int id)
    {
        if (_graph == null)
            return;

        NodeId.TrySelectValue(id);
        PointType.Clear();
        PointType.Visible = true;

        PointType.AddItem("Action Startup", GoapBreakpointKind.ActionStartup);
        PointType.AddItem("Action Update", GoapBreakpointKind.ActionUpdate);
        PointType.AddItem("Action Shutdown", GoapBreakpointKind.ActionShutdown);
        PointType.AddItem("Action Plan Shutdown", GoapBreakpointKind.ActionPlanShutdown);
        PointType.AddItem("Planning", GoapBreakpointKind.Planning);

        // The first type is selected by itself
        SelectPointType(GoapBreakpointKind.ActionStartup);
    }

    private void SelectPointType(GoapBreakpointKind kind)
    {
        if (_graph == null)
            return;

        ActionIndex.Visible = true;
        Result.Visible = true;
        ActionIndex.Clear();
        Result.Clear();

        ActionIndex.AddItem("Any", -1);

        if (NodeId.SelectedValue is int nodeId
            && _graph.Value.Nodes.TryGetValue(nodeId, out var node))
        {
            for (var i = 0; i < node.Actions.Count; i++)
            {
                ActionIndex.AddItem(node.Actions[i].Name, i);
            }
        }

        switch (kind)
        {
            case GoapBreakpointKind.Planning:
            case GoapBreakpointKind.ActionStartup:
                Result.AddItem("True", GoapBreakpointResultKind.True);
                Result.AddItem("False", GoapBreakpointResultKind.False);
                break;
            case GoapBreakpointKind.ActionUpdate:
                Result.AddItem("Continuing", GoapBreakpointResultKind.Continuing);
                Result.AddItem("Finished", GoapBreakpointResultKind.Finished);
                Result.AddItem("Failed", GoapBreakpointResultKind.Failed);
                break;
            default:
                Result.AddItem("None", GoapBreakpointResultKind.None);
                break;
        }

        UpdateConfirm();
    }

    private void UpdateConfirm()
    {
        Confirm.Disabled = _graph == null
                           || _target == null
                           || PointType.SelectedValue == null
                           || Result.SelectedValue == null;
    }

    private string GetName(GoapBreakpoint point)
    {
        if (_graph?.Nodes is not { } nodes)
            return string.Empty;

        var nodeName = point.NodeId != -1
            ? $"#{point.NodeId}"
            : "#Any";
        var actName = point.Index != -1
            ? $"{nodes[point.NodeId].Actions[point.Index].Name} ({point.Index})"
            : "Any";
        var str = $"{nodeName} {actName}";

        return point.Kind switch
        {
            GoapBreakpointKind.ActionStartup => $"{str} Startup: {point.Result}",
            GoapBreakpointKind.ActionUpdate => $"{str} Update: {point.Result}",
            GoapBreakpointKind.ActionShutdown => $"{str} Shutdown",
            GoapBreakpointKind.ActionPlanShutdown => $"{str} Plan Shutdown",
            GoapBreakpointKind.Planning => $"{nodeName} Planning: {point.Result}",
            _ => throw new ArgumentOutOfRangeException(),
        };
    }

    private static string PointTypeString(GoapBreakpointKind type)
    {
        return type switch
        {
            GoapBreakpointKind.ActionStartup => "Startup",
            GoapBreakpointKind.ActionUpdate => "Update",
            GoapBreakpointKind.ActionShutdown => "Shutdown",
            GoapBreakpointKind.ActionPlanShutdown => "Plan Shutdown",
            GoapBreakpointKind.Planning => "Planning",
            _ => throw new ArgumentOutOfRangeException(),
        };
    }

    private void OnBreakpointAdded(GoapBreakpoint point)
    {
        if (_points.TryGetValue(point, out var check))
        {
            check.Check.Pressed = true;
            return;
        }

        check = new BreakpointControl();
        check.Check.Text = GetName(point);
        check.Check.Pressed = true;
        _points[point] = check;
        Breakpoints.AddChild(check);

        check.Check.OnToggled += _ =>
        {
            if (!check.Check.Pressed)
                DebugController.RemoveBreakpoint(point);
            else
                DebugController.AddBreakpoint(point);
        };

        check.RemoveButton.OnPressed += _ =>
        {
            _points.Remove(point);
            Breakpoints.RemoveChild(check);
            DebugController.RemoveBreakpoint(point);
        };
    }

    private void OnBreakpointRemoved(GoapBreakpoint point)
    {
        if (_points.TryGetValue(point, out var check))
            check.Check.Pressed = false;
    }

    private void OnBreakpointHit((GoapBreakpoint Point, GoapPlanDebugInfo Plan) point)
    {
        if (_graph != null)
            Update(_graph.Value, point.Plan);

        if (Graph.NodeControls.TryGetValue(point.Point.NodeId, out var node))
            Graph.CenterOnNode(node);
    }
}

