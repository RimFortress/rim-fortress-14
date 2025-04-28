using System.Linq;
using Content.Client.NPC.HTN;
using Content.Shared._RF.NPC;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Client._RF.NPC;

public sealed class NpcControlSystem : SharedNpcControlSystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IOverlayManager _overlay = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public MapCoordinates? StartPoint { get; private set; }
    public MapCoordinates? EndPoint { get; private set; }
    public HashSet<EntityUid> Selected { get; private set; } = new();
    public Dictionary<EntityUid, NpcTask> Tasks { get; } = new();

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        _overlay.AddOverlay(new NpcControlOverlay(_prototype, EntityManager, EntityManager.EntitySysManager));

        CommandBinds.Builder
            .Bind(EngineKeyFunctions.Use, new PointerStateInputCmdHandler(OnSelectEnabled, OnSelectDisabled))
            .Bind(EngineKeyFunctions.UseSecondary, new PointerInputCmdHandler(OnUseSecondary))
            .Register<SharedNpcControlSystem>();

        SubscribeNetworkEvent<NpcTaskInfoMessage>(OnTaskInfo);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _overlay.RemoveOverlay<NpcControlOverlay>();
    }

    private bool OnSelectEnabled(ICommonSession? player, EntityCoordinates coords, EntityUid uid)
    {
        if (player?.AttachedEntity is not { Valid: true } entity
            || !TryComp(entity, out NpcControlComponent? _))
            return false;

        StartPoint = _transform.ToMapCoordinates(coords);
        EndPoint = StartPoint;
        return false;
    }

    private bool OnSelectDisabled(ICommonSession? player, EntityCoordinates coords, EntityUid uid)
    {
        if (player?.AttachedEntity is not { Valid: true } entity
            || !TryComp(entity, out NpcControlComponent? _))
            return false;

        StartPoint = null;
        return false;
    }

    private bool OnUseSecondary(ICommonSession? player, EntityCoordinates coords, EntityUid uid)
    {
        if (player is not { AttachedEntity: { Valid: true} requester }
            || Selected.Count == 0)
            return false;

        RaiseNetworkEvent(new NpcTaskRequest
        {
            Requester = GetNetEntity(requester),
            Entities = Selected.Select(entity => GetNetEntity(entity)).ToList(),
            Target = uid.IsValid() ? GetNetEntity(uid) : null,
            TargetCoordinates = GetNetCoordinates(coords),
        });

        return true;
    }

    private void OnTaskInfo(NpcTaskInfoMessage msg)
    {
        if (msg.Target == null && msg.TargetCoordinates == null)
        {
            Tasks.Remove(GetEntity(msg.Entity));
            return;
        }

        var task = new NpcTask(msg.Color, GetEntity(msg.Target), GetCoordinates(msg.TargetCoordinates));
        Tasks[GetEntity(msg.Entity)] = task;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (StartPoint is not { } start
            || EndPoint is not { } end
            || start.MapId != end.MapId)
        {
            StartPoint = null;
            EndPoint = null;
            return;
        }

        if (_input.MouseScreenPosition is { IsValid: true } mousePos)
            EndPoint = _eye.PixelToMap(mousePos);

        Selected = GetNpcInSelect();
    }

    /// <summary>
    /// Gets the list of NPCs in the selection area
    /// </summary>
    private HashSet<EntityUid> GetNpcInSelect()
    {
        if (StartPoint is not { } start
            || EndPoint is not { } end
            || start.MapId != end.MapId)
            return new HashSet<EntityUid>();

        var area = new Box2(start.Position, end.Position);
        var entities = _lookup.GetEntitiesIntersecting(start.MapId, area, LookupFlags.Dynamic);

        foreach (var entity in entities)
        {
            if (!TryComp(entity, out HTNComponent? _))
                entities.Remove(entity);
        }

        return entities;
    }
}
