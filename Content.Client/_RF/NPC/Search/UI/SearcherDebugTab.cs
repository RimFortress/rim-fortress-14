using Content.Client._RF.NPC.UI;
using Content.Client._RF.Suil;
using Content.Shared._RF.NPC;
using Content.Shared._RF.NPC.Search;
using Content.Shared.Input;

namespace Content.Client._RF.NPC.Search.UI;

/// <summary>
/// The tab of the search of an NPC: the queries and what they found.
/// </summary>
public sealed partial class SearcherDebugTab : UiElement
{
    public SearcherDebugTab()
    {
        InitializeComponent();

        Graph.NodeSize = new(350, 400);
        Graph.ZoomStep = 0.05f;
    }

    public void Update(NpcSearchGraph graph)
    {
        var firstUpdate = Graph.BeginUpdate(GraphLayoutBuilder.Build(graph, Graph.NodeSize));

        foreach (var node in graph.Nodes)
        {
            var control = new SearchQueryControl(node);
            control.OnKeyBindUp += args =>
            {
                if (args.Function == ContentKeyFunctions.MouseMiddle)
                    Graph.ToggleExpanded(node.Id);
            };

            Graph.AddNode(node.Id, control);
        }

        Graph.EndUpdate(firstUpdate);
    }
}
