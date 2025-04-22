using System.Linq;
using Content.Shared._RF.Equipment;
using Robust.Client;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Client._RF.Lobby;


public sealed class PlayerEquipmentManager : IPostInjectInit, IPlayerEquipmentManager
{
    [Dependency] private readonly IClientNetManager _net = default!;
    [Dependency] private readonly IBaseClient _client = default!;

    public Dictionary<EntProtoId, int> Equipment { get; private set; } = new();

    public void PostInject()
    {
        _net.RegisterNetMessage<MsgPlayerEquipment>(HandlePlayerEquipmentMessage);
        _net.RegisterNetMessage<MsgUpdatePlayerEquipment>();

        _client.RunLevelChanged += BaseClientOnRunLevelChanged;
    }

    private void BaseClientOnRunLevelChanged(object? sender, RunLevelChangedEventArgs e)
    {
        if (e.NewLevel == ClientRunLevel.Initialize)
            Equipment.Clear();
    }

    private void HandlePlayerEquipmentMessage(MsgPlayerEquipment msg)
    {
        Equipment = msg.Equipment.Select(x => ((EntProtoId) x.Key, x.Value)).ToDictionary();
    }

    public void SetPlayerEquipment(Dictionary<EntProtoId, int> equipment)
    {
        // I think we dont need to do client-side equipment sanitizing, because ui already do it
        Equipment = new(equipment);
        var msg = new MsgUpdatePlayerEquipment
        {
            Equipment = equipment.Select(x => ((string) x.Key, x.Value)).ToDictionary()
        };
        _net.ClientSendMessage(msg);
    }
}

public interface IPlayerEquipmentManager
{
    Dictionary<EntProtoId, int> Equipment { get; }

    void SetPlayerEquipment(Dictionary<EntProtoId, int> equipment);
}
