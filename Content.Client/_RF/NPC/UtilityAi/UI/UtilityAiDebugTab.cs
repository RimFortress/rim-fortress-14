using System.Linq;
using Content.Client._RF.NPC.UI;
using Content.Client._RF.Suil;
using Content.Shared._RF.NPC;
using Content.Shared._RF.NPC.UtilityAi;
using Content.Shared.Input;

namespace Content.Client._RF.NPC.UtilityAi.UI;

/// <summary>
/// The tab of the Utility AI graph of an NPC: the goals and the one that is chosen.
/// </summary>
public sealed partial class UtilityAiDebugTab : UiElement
{
    private UtilityAiDebugInfo? _info;

    public UtilityAiDebugTab()
    {
        InitializeComponent();

        Graph.NodeSize = new(350, 400);
        Graph.ZoomStep = 0.05f;
        Graph.EdgeColor = ConnectionColor;
    }

    public void Update(UtilityAiDebugInfo info)
    {
        _info = info;

        var firstUpdate = Graph.BeginUpdate(GraphLayoutBuilder.Build(info.Graph, Graph.NodeSize));

        foreach (var node in info.Graph.Nodes)
        {
            var control = new UtilityAiGoalControl(node, info.CurrentGoal);
            control.OnKeyBindUp += args =>
            {
                if (args.Function == ContentKeyFunctions.MouseMiddle)
                    Graph.ToggleExpanded(node.Id);
            };

            Graph.AddNode(node.Id, control);
        }

        Graph.EndUpdate(firstUpdate);
    }

    private Color ConnectionColor((int From, int To) fromTo)
    {
        if (_info?.Graph.Nodes.Any(x => x.Id == fromTo.From && x.InActiveBranch) == true)
            return Color.LimeGreen;

        return Color.DimGray; // Default color
    }
}
