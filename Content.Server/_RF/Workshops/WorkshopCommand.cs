using System.Linq;
using Content.Server._RF.Workshops.Systems;
using Content.Server.Administration;
using Content.Shared._RF.Toolshed;
using Content.Shared._RF.Workshops.Components;
using Content.Shared._RF.Workshops.Prototypes;
using Content.Shared.Administration;
using Robust.Shared.Prototypes;
using Robust.Shared.Toolshed;

namespace Content.Server._RF.Workshops;

[ToolshedCommand, AdminCommand(AdminFlags.Debug)]
public sealed class WorkshopCommand : SystemCommand<WorkshopSystem>
{
    [CommandImplementation("add_queue")]
    public IEnumerable<EntityUid> AddQueue(
        [PipedArgument] IEnumerable<EntityUid> uids,
        ProtoId<WorkshopRecipePrototype> recipe)
        => uids.Where(uid => System.AddToQueue(uid, recipe));

    [CommandImplementation("remove_queue")]
    public IEnumerable<EntityUid> RemoveQueue(
        [PipedArgument] IEnumerable<EntityUid> uids,
        int index)
        => uids.Where(uid => System.RemoveFromQueue(uid, index));

    [CommandImplementation("current_items")]
    public IEnumerable<EntProtoId> CurrentItems([PipedArgument] IEnumerable<EntityUid> uids)
    {
        var items = new List<EntProtoId>();

        foreach (var uid in uids)
        {
            if (!TryComp(uid, out WorkshopComponent? comp)
                || System.GetCurrentRecipe(new(uid, comp)) is not { } recipe)
                continue;

            items.AddRange(GetItems(recipe, comp.Recipes));
        }

        return items;
    }

    [CommandImplementation("all_items")]
    public IEnumerable<EntProtoId> AllItems([PipedArgument] IEnumerable<EntityUid> uids)
    {
        var items = new List<EntProtoId>();

        foreach (var uid in uids)
        {
            if (!TryComp(uid, out WorkshopComponent? comp))
                continue;

            foreach (var entry in comp.Queue.Queue)
            {
                items.AddRange(GetItems(entry.Recipe, comp.Recipes));

                foreach (var path in entry.Pathfinding)
                {
                    items.AddRange(GetItems(path, comp.Recipes));
                }
            }
        }

        return items;
    }

    private List<EntProtoId> GetItems(
        ProtoId<WorkshopRecipePrototype> recipe,
        ProtoId<WorkshopRecipeTablePrototype> tableId)
    {
        var items = new List<EntProtoId>();

        foreach (var (protoId, count) in System.GetRecipeIngredients(recipe, tableId).Items)
        {
            for (var i = 0; i < count; i++)
            {
                items.Add(protoId);
            }
        }

        return items;
    }
}
