using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.Database;
using Content.Shared._RF.Equipment;
using Content.Shared._RF.Preferences;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._RF.Equipment;

public sealed class PlayerEquipmentManager : IPlayerEquipmentManager, IPostInjectInit
{
    [Dependency] private readonly IServerNetManager _net = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly ILogManager _log = default!;
    [Dependency] private readonly UserDbDataManager _userDb = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    private Dictionary<EntProtoId, int>? _costs;
    private readonly Dictionary<NetUserId, PlayerEquipData> _cachedPlayerPrefs = new();

    private ISawmill _sawmill = default!;

    public void PostInject()
    {
        _userDb.AddOnLoadPlayer(LoadData);
        _userDb.AddOnFinishLoad(FinishLoad);
        _userDb.AddOnPlayerDisconnect(OnClientDisconnected);

        _net.RegisterNetMessage<MsgPlayerEquipment>();
        _net.RegisterNetMessage<MsgUpdatePlayerEquipment>(HandleUpdateEquipmentMessage);

        _sawmill = _log.GetSawmill("equip");

        _prototype.PrototypesReloaded += OnPrototypeReloaded;
    }

    private void OnPrototypeReloaded(PrototypesReloadedEventArgs args)
    {
        if (!args.WasModified<ExpeditionEquipmentPrototype>())
            return;

        UpdateCosts();
    }

    private async void HandleUpdateEquipmentMessage(MsgUpdatePlayerEquipment message)
    {
        var userId = message.MsgChannel.UserId;

        if (!_cachedPlayerPrefs.TryGetValue(userId, out var data) || !data.Loaded)
        {
            _sawmill.Warning($"User {userId} tried to modify preferences before they loaded.");
            return;
        }

        var equip = SanitizeEquipment(message.Equipment
            .Select(x => ((EntProtoId) x.Key, x.Value))
            .ToDictionary());
        data.Equip = equip;

        await _db.SaveEquipmentsAsync(userId, equip);
    }

    public async Task LoadData(ICommonSession session, CancellationToken cancel)
    {
        var equip = await _db.GetPlayerEquipment(session.UserId) ?? new();
        _cachedPlayerPrefs[session.UserId] = new PlayerEquipData(SanitizeEquipment(equip));
    }

    public void FinishLoad(ICommonSession session)
    {
        var data = _cachedPlayerPrefs[session.UserId];
        DebugTools.Assert(data.Equip != null);
        data.Loaded = true;

        var msg = new MsgPlayerEquipment { Equipment = data.Equip.Select(x => ((string) x.Key, x.Value)).ToDictionary() };
        _net.ServerSendMessage(msg, session.Channel);
    }

    public void OnClientDisconnected(ICommonSession session)
    {
        _cachedPlayerPrefs.Remove(session.UserId);
    }

    private void UpdateCosts()
    {
        _costs = new();

        var prototypes = _prototype.EnumeratePrototypes<ExpeditionEquipmentPrototype>();
        foreach (var prototype in prototypes)
        {
            foreach (var (proto, cost) in prototype.Items)
            {
                _costs[proto] = cost;
            }
        }
    }

    public Dictionary<EntProtoId, int>? GetPlayerEquipment(NetUserId userId)
    {
        return _cachedPlayerPrefs.GetValueOrDefault(userId)?.Equip;
    }

    private Dictionary<EntProtoId, int> SanitizeEquipment(Dictionary<EntProtoId, int> equip)
    {
        var maxPoints = _cfg.GetCVar(CCVars.RoundstartEquipmentPoints);
        var sanitized = new Dictionary<EntProtoId, int>();

        if (_costs == null)
            UpdateCosts();

        var total = 0;
        foreach (var (proto, count) in equip)
        {
            if (!_costs!.TryGetValue(proto, out var cost))
                continue;

            total += cost;
            if (total > maxPoints)
                break;

            sanitized.Add(proto, count);
        }

        return sanitized;
    }

    private sealed class PlayerEquipData(Dictionary<EntProtoId, int>? equip)
    {
        public bool Loaded;
        public Dictionary<EntProtoId, int>? Equip = equip;
    }
}

public interface IPlayerEquipmentManager
{
    Task LoadData(ICommonSession session, CancellationToken cancel);
    void FinishLoad(ICommonSession session);
    void OnClientDisconnected(ICommonSession session);

    Dictionary<EntProtoId, int>? GetPlayerEquipment(NetUserId userId);
}
