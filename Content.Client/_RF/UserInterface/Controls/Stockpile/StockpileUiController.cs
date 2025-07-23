using Content.Client._RF.Stockpile;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client._RF.UserInterface.Controls.Stockpile;

public sealed class StockpileUiController : UIController
{
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IEyeManager _eye  = default!;
    [UISystemDependency] private readonly StockpileSystem _stockpile  = default!;
    [UISystemDependency] private readonly TransformSystem _xform = default!;

    private SelectMode _selectMode = SelectMode.None;

    public override void Initialize()
    {
        base.Initialize();

        CommandBinds.Builder
            .Bind(EngineKeyFunctions.Use, new PointerInputCmdHandler(OnUse))
            .Register<StockpileUiController>();
    }

    private bool OnUse(ICommonSession? player, EntityCoordinates coords, EntityUid uid)
    {
        if (_selectMode != SelectMode.Click || !_stockpile.TryGetStock(coords, out var stock))
            return false;

        _stockpile.SelectedStock = stock;
        return true;
    }

    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_selectMode != SelectMode.Hover
            || _input.MouseScreenPosition is not { IsValid: true } mouseCoords)
            return;

        var mapCoords = _eye.PixelToMap(mouseCoords);
        var coords = _xform.ToCoordinates(mapCoords);

        if (_stockpile.TryGetStock(coords, out var stock))
            _stockpile.SelectedStock = stock;
    }
}

public enum SelectMode
{
    Hover,
    Click,
    None,
}
