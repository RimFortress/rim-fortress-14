using Content.Shared._RF.Workshops.Components;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._RF.Workshops.UI;

[UsedImplicitly]
public sealed class WorkshopBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private WorkshopMenu? _menu;

    protected override void Open()
    {
        base.Open();
        _menu = this.CreateWindow<WorkshopMenu>();
        _menu.OnRemoved += index => SendPredictedMessage(new WorkshopRemoveFromQueueMessage(index));
        _menu.OnAdded += proto => SendPredictedMessage(new WorkshopAddToQueueMessage(proto));
        _menu.OnToggleRepeat += index => SendPredictedMessage(new WorkshopRepeatMessage(index));
        _menu.OnToggleSuspend += index => SendPredictedMessage(new WorkshopSuspendMessage(index));
        _menu.OnSupplyStockpile +=
            id => SendPredictedMessage(new WorkshopSuppliedStockMessage(EntMan.GetNetEntity(id)));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (_menu == null || state is not WorkshopUiState wState)
            return;

        _menu.SetTitle(Owner);
        _menu.UpdateUser(wState.User);
        _menu.UpdateContents(wState.Contained);
        _menu.UpdateResults(wState.ContainedResults, wState.ResultsCapacity);
        _menu.UpdateQueue(wState.Queue, wState.MaxQueue);
        _menu.UpdateRecipes(wState.RecipesTable);
        _menu.UpdateSupplying(Owner);
    }
}
