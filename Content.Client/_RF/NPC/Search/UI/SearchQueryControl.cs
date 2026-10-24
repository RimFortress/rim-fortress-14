using Content.Client._RF.NPC.UI;
using Content.Shared._RF.NPC.Search;
using Content.Client._RF.UserInterface.Controls;

namespace Content.Client._RF.NPC.Search.UI;

/// <summary>
/// A query of the NPC search: the filters, the considerations and the results.
/// </summary>
public sealed partial class SearchQueryControl : DebugNodeControl
{
    public SearchQueryControl(NpcSearchDebugInfo info)
    {
        InitializeComponent();

        RfTabContainer.SetTabTitle(QueryTab, "Query");
        RfTabContainer.SetTabTitle(FiltersTab, "Filters");
        RfTabContainer.SetTabTitle(ConsiderationsTab, "Considerations");

        RfTabContainer.SetTabVisible(FiltersTab, info.Filters.Count > 0);
        RfTabContainer.SetTabVisible(ConsiderationsTab, info.Considerations.Count > 0);

        Title.Text = $"[bold]{info.ProtoId}[/bold]";
        SubTitle.Text = $"[bold]{info.Results.Count}[/bold]";
        StatusLabel.Visible = false;

        SearchText = new() { info.ProtoId.Id, info.Results.Count.ToString() };

        // Query
        var query = AiUiHelper.AddBox(QueryTab, "Query", info.Query.Reflection.Name);
        AiUiHelper.AddTypes(query.Content, "Fields", info.Query.Reflection);

        if (info.Query.Result.Count > 0)
        {
            var queryResults = AiUiHelper.AddBox(query.Content, "Results");

            foreach (var ent in info.Query.Result)
            {
                AiUiHelper.AddLabel(queryResults.Content, ent, "");
            }
        }

        if (info.Results.Count > 0)
        {
            var results = AiUiHelper.AddBox(QueryTab, "Results");

            foreach (var (ent, (parts, result)) in info.Results)
            {
                var box = AiUiHelper.AddBox(results.Content, ent, $"{result:F2}");
                AiUiHelper.AddLabel(box.Content, $"{string.Join(" * ", parts)} = {result:F2}", "");
            }
        }

        // Filters
        foreach (var (reflection, filtered) in info.Filters)
        {
            var filterBox = AiUiHelper.AddBox(FiltersTab, reflection.Name);
            AiUiHelper.AddTypes(filterBox.Content, "Fields", reflection);

            if (filtered.Count == 0)
                continue;

            var box = AiUiHelper.AddBox(filterBox.Content, "Filtered");

            foreach (var ent in filtered)
            {
                AiUiHelper.AddLabel(box.Content, ent, "");
            }
        }

        // Considerations
        foreach (var (reflection, con) in info.Considerations)
        {
            var conBox = AiUiHelper.AddBox(ConsiderationsTab, reflection.Name);
            AiUiHelper.AddTypes(conBox.Content, "Fields", reflection);

            if (con.Count == 0)
                continue;

            var box = AiUiHelper.AddBox(conBox.Content, "Results");

            foreach (var (ent, score) in con)
            {
                AiUiHelper.AddLabel(box.Content, ent, $"{score:F2}");
            }
        }
    }
}

