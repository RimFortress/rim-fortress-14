using Content.Shared._RF.Info;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._RF.Info.Controls;

public sealed class PopInfoUIController : UIController
{
    [Dependency] private readonly IEntityManager _entity = default!;
    [Dependency] private readonly IEntityNetworkManager _net = default!;

    private PopInfoWindow? _window;

    public event Action<EntityHealthInfoResponse>? OnHealthInfo;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<EntityHealthInfoResponse>(OnHealthInfoResponse);
    }

    private void OnHealthInfoResponse(EntityHealthInfoResponse msg, EntitySessionEventArgs args)
    {
        OnHealthInfo?.Invoke(msg);
    }

    public void OpenWindow(EntityUid uid)
    {
        EnsureWindow();
        _window!.SetInfo(uid);
        _window.Open();
    }

    private void EnsureWindow()
    {
        if (_window is { Disposed: false })
            return;

        _window = UIManager.CreateWindow<PopInfoWindow>();
        LayoutContainer.SetAnchorPreset(_window, LayoutContainer.LayoutPreset.Center);
    }

    public void HealthInfoRequest(EntityUid uid)
    {
        var netUid = _entity.GetNetEntity(uid);
        _net.SendSystemNetworkMessage(new EntityHealthInfoRequest(netUid));
    }
}

