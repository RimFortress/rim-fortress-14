using System.Linq;
using Content.Shared._RF.Workshops.Components;
using Robust.Client.GameObjects;

namespace Content.Client._RF.Workshops.Systems;

public sealed class WorkshopVisualizerSystem : VisualizerSystem<WorkshopVisualsComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, WorkshopVisualsComponent comp, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null
            || !AppearanceSystem.TryGetData<bool>(uid, WorkshopVisualsState.Crafting, out var crafting, args.Component)
            || !AppearanceSystem.TryGetData<int>(uid, WorkshopVisualsState.Items, out var items, args.Component)
            || comp.Stages.Count == 0)
            return;

        var ent = new Entity<SpriteComponent?>(uid, args.Sprite);
        var stage = GetStage(comp, items);
        var itemsState = crafting ? stage.CraftingState : stage.IdleState;
        var baseState = crafting ? comp.CraftingBaseState : comp.IdleBaseState;

        SpriteSystem.LayerSetVisible(ent, WorkshopLayers.Items, stage.Visible);
        SpriteSystem.LayerSetRsiState(ent, WorkshopLayers.Base, baseState);

        if (itemsState != null)
            SpriteSystem.LayerSetRsiState(ent, WorkshopLayers.Items, itemsState);
    }

    private static WorkshopVisualStage GetStage(WorkshopVisualsComponent comp, int items)
        => comp.Stages
            .Where(x => x.Threshold >= items)
            .OrderBy(x => x.Threshold)
            .FirstOrDefault()
           ?? comp.Stages.OrderBy(x => x.Threshold).Last();
}
