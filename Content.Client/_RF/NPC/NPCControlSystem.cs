using Content.Client.NPC.HTN;
using Content.Shared._RF.NPC;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Client._RF.NPC;

public sealed class NPCControlSystem : SharedNPCControlSystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IOverlayManager _overlay = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public MapCoordinates? StartPoint { get; private set; }
    public MapCoordinates? EndPoint { get; private set; }

    [ValidatePrototypeId<ShaderPrototype>]
    private const string ShaderTarget = "SelectionOutlineWhite";

    private ShaderInstance? _shaderTarget;
    private HashSet<EntityUid> _selected = new();
    private readonly HashSet<SpriteComponent> _highlightedSprites = new();

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        _shaderTarget = _prototype.Index<ShaderPrototype>(ShaderTarget).InstanceUnique();
        _overlay.AddOverlay(new NPCControlOverlay(this));

        CommandBinds.Builder
            .Bind(EngineKeyFunctions.Use, new PointerStateInputCmdHandler(OnSelectEnabled, OnSelectDisabled))
            .Register<SharedNPCControlSystem>();
    }

    private bool OnSelectEnabled(ICommonSession? player, EntityCoordinates coords, EntityUid uid)
    {
        if (player?.AttachedEntity is not { Valid: true } entity
            || !TryComp(entity, out NPCControlComponent? _))
            return false;

        StartPoint = _transform.ToMapCoordinates(coords);
        EndPoint = StartPoint;
        return false;
    }

    private bool OnSelectDisabled(ICommonSession? player, EntityCoordinates coords, EntityUid uid)
    {
        if (player?.AttachedEntity is not { Valid: true } entity
            || !TryComp(entity, out NPCControlComponent? _))
            return false;

        StartPoint = null;
        return false;
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

        foreach (var sprite in _highlightedSprites)
        {
            sprite.PostShader = null;
            sprite.RenderOrder = 0;
        }

        _selected.Clear();
        _highlightedSprites.Clear();

        foreach (var entity in GetNPCsInSelect())
        {
            if (!TryComp(entity, out SpriteComponent? sprite) || !sprite.Visible)
                continue;

            _selected.Add(entity);
            _highlightedSprites.Add(sprite);
            sprite.PostShader = _shaderTarget;
            sprite.RenderOrder = EntityManager.CurrentTick.Value;
        }
    }

    private HashSet<EntityUid> GetNPCsInSelect()
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
