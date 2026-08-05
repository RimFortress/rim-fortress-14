using System.Numerics;
using Content.Server._RF.NPC.GOAP.Actions.Movement;
using Content.Server._RF.NPC.GOAP.Systems;
using Content.Server._RF.NPC.Search.Systems;
using Content.Server._RF.NPC.Systems;
using Content.Server._RF.Skills;
using Content.Server.Hands.Systems;
using Content.Server.NPC.Components;
using Content.Server.NPC.Pathfinding;
using Content.Server.Wieldable;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.Search.Prototypes;
using Content.Shared._RF.Skills;
using Content.Shared.CombatMode;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Physics;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Whitelist;
using Content.Shared.Wieldable.Components;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._RF.NPC.GOAP.Actions.Combat;

/// <summary>
/// Action responsible for the logic of the agent's firing at the target entity.
/// </summary>
public sealed partial class Gun : BaseGoapAction<Gun>
{
    /// <summary>
    /// Target entity.
    /// </summary>
    [DataField(required: true)]
    public StateKey<EntityUid> TargetKey;

    /// <summary>
    /// Minimum damage state that the target has to be in for us to consider attacking.
    /// </summary>
    [DataField]
    public MobState TargetState = MobState.Alive;

    /// <summary>
    /// Do we require line of sight of the target before failing?
    /// </summary>
    [DataField]
    public bool RequireLos;

    /// <summary>
    /// If true, only opaque objects will block line of sight.
    /// </summary>
    [DataField]
    public bool UseOpaqueForLosChecks;

    [DataField]
    public float ShootDelay = 1f;

    [DataField]
    public float AccuracyThreshold = 0.1f;

    [DataField]
    public float ReloadRetryDelay = 1f;

    [ViewVariables]
    public static readonly StateKey<SoundSpecifier> SoundTargetInLos = "SoundTargetInLos";

    [ViewVariables]
    public static readonly StateKey<TimeSpan> NextLosCheckKey = "GunNextLosCheck";

    [ViewVariables]
    public static readonly StateKey<TimeSpan> ShootReadyAtKey = "GunShootReadyAt";

    [ViewVariables]
    public static readonly StateKey<bool> TargetInLosKey = "GunTargetInLos";

    /// <summary>
    /// A flag indicating that the agent is moving toward a new magazine.
    /// </summary>
    [ViewVariables]
    public static readonly StateKey<bool> MovingToMagazineKey = "MovingToMagazine";

    /// <summary>
    /// Where the pathfinding result will be stored (if applicable).
    /// </summary>
    [ViewVariables]
    public static readonly StateKey<PathResultEvent> PathfindKey = "GunMovementPathfinding";

    /// <summary>
    /// The key that unlocks the found store, which should be picked up.
    /// </summary>
    [ViewVariables]
    public static readonly StateKey<EntityUid> NearbyMagazineKey = "NearbyMagazine";

    [ViewVariables]
    public static readonly StateKey<JukeType> PreviousJukeTypeKey = "PreviousJukeType";
}

public sealed class GunActionSystem : GoapActionSystem<Gun>
{
    [Dependency] private readonly SharedCombatModeSystem _combatMode = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly NpcSearcherSystem _searcher = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly RotateToFaceSystem _rotate = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ItemSlotsSystem _slots = default!;
    [Dependency] private readonly WieldableSystem _wield = default!;
    [Dependency] private readonly ThrowingSystem _throw = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly HandsSystem _hands = default!;
    [Dependency] private readonly NpcTimingSystem _npcTiming = default!;
    [Dependency] private readonly MoveToActionSystem _moveTo = default!;
    [Dependency] private readonly SkillsSystem _skills = default!;

    [Dependency] private readonly EntityQuery<MobStateComponent> _mobStateQuery = default!;
    [Dependency] private readonly EntityQuery<PhysicsComponent> _physicsQuery = default!;
    [Dependency] private readonly EntityQuery<TransformComponent> _xformQuery = default!;
    [Dependency] private readonly EntityQuery<RechargeBasicEntityAmmoComponent> _rechargeQuery = default!;
    [Dependency] private readonly EntityQuery<NPCSteeringComponent> _steeringQuery = default!;
    [Dependency] private readonly EntityQuery<ItemSlotsComponent> _slotsQuery = default!;
    [Dependency] private readonly EntityQuery<WieldableComponent> _wieldQuery = default!;
    [Dependency] private readonly EntityQuery<GunRequiresWieldComponent> _wieldRequireQuery = default!;
    [Dependency] private readonly EntityQuery<ChamberMagazineAmmoProviderComponent> _chamberQuery = default!;
    [Dependency] private readonly EntityQuery<NPCJukeComponent> _jukeQuery = default!;

    private static readonly ProtoId<SearchQueryPrototype> InventoryMagazineQuery = "InventoryMagazine";
    private static readonly ProtoId<SearchQueryPrototype> NearbyMagazineQuery = "NearbyMagazine";
    private static readonly StateKey<EntityWhitelist> MagazineWhitelistKey = "MagazineWhitelist";
    private static readonly StateKey<EntityWhitelist> MagazineBlacklistKey = "MagazineBlacklist";

    private const float ShootSpeed = 20f;

    /// <summary>
    /// Cooldown on raycasting to check LOS.
    /// </summary>
    private const float UnoccludedCooldown = 0.2f;

    /// <summary>
    /// A modifier affecting the spread of fire based on the agent's current speed.
    /// </summary>
    private const float MovementScatterFactor = 2f;

    /// <summary>
    /// A modifier affecting the spread of fire based on the distance from the agent to the target.
    /// </summary>
    private const float DistanceScatterFactor = 0.5f;

    protected override bool ActionStartup(Entity<GoapComponent> ent, Gun action)
    {
        if (!TryGetValue(ent, action, action.TargetKey, out var target))
            return false;

        if (_mobStateQuery.TryComp(target, out var mobState) && mobState.CurrentState > action.TargetState)
            return true;

        _combatMode.SetInCombatMode(ent, true);

        var state = ent.Comp.State;
        state.SetValue(Gun.NextLosCheckKey, TimeSpan.Zero);
        state.SetValue(Gun.ShootReadyAtKey, _timing.CurTime + TimeSpan.FromSeconds(action.ShootDelay));
        state.SetValue(Gun.TargetInLosKey, false);
        state.SetValue(Gun.MovingToMagazineKey, false);
        state.Remove(Gun.NearbyMagazineKey);
        state.Remove(Gun.PreviousJukeTypeKey);

        return true;
    }

    protected override void ActionShutdown(Entity<GoapComponent> ent, Gun action)
    {
        _combatMode.SetInCombatMode(ent, false);

        var state = ent.Comp.State;
        state.Remove(Gun.NextLosCheckKey);
        state.Remove(Gun.ShootReadyAtKey);
        state.Remove(Gun.TargetInLosKey);
        state.Remove(Gun.MovingToMagazineKey);
        state.Remove(Gun.NearbyMagazineKey);
        state.Remove(Gun.PreviousJukeTypeKey);
    }

    protected override GoapActionResult ActionUpdate(Entity<GoapComponent> ent, Gun action)
    {
        if (!TryGetValue(ent, action, action.TargetKey, out var target))
            return GoapActionResult.Failed;

        // Success
        if (Deleted(target)
            || _mobStateQuery.TryComp(target, out var mobState) && mobState.CurrentState > action.TargetState)
            return GoapActionResult.Finished;

        var waitResult = _npcTiming.WaitQueue(ent, action);

        if (waitResult != GoapActionResult.Finished)
            return waitResult;

        var state = ent.Comp.State;

        if (state.GetValue(Gun.MovingToMagazineKey))
        {
            if (!TryGetValue(ent, action, Gun.NearbyMagazineKey, out var ammoUid))
                return GoapActionResult.Failed;

            var result = _moveTo.UpdateMovement(ent,
                action,
                Transform(ammoUid).Coordinates,
                Gun.PathfindKey,
                GoapState.InteractRange,
                false);

            if (result != GoapActionResult.Finished)
                return result;

            _moveTo.ShutdownMovement(ent, Gun.PathfindKey);
            state.SetValue(Gun.MovingToMagazineKey, false);

            if (TryGetValue(ent, action, Gun.PreviousJukeTypeKey, out var type))
                EnsureComp<NPCJukeComponent>(ent).JukeType = type;

            state.Remove(Gun.PreviousJukeTypeKey);
            return ReplaceMagazine(ent, action, ammoUid);
        }

        if (!TryGetValue(ent, action, Gun.NextLosCheckKey, out var nextLosCheck))
            return GoapActionResult.Failed;

        if (_steeringQuery.TryComp(ent, out var steering) && steering.Status == SteeringStatus.NoPath)
        {
            CreateDump(ent, action, "target unreachable: no path");
            return GoapActionResult.Failed;
        }

        if (!_xformQuery.TryComp(target, out var targetXform))
        {
            ComponentNotFound<TransformComponent>(ent, action, target);
            return GoapActionResult.Failed;
        }

        if (!_physicsQuery.TryComp(target, out var targetBody))
        {
            ComponentNotFound<PhysicsComponent>(ent, action, target);
            return GoapActionResult.Failed;
        }

        var xform = Transform(ent);

        if (targetXform.MapID != xform.MapID)
        {
            CreateDump(ent, action, "target is on a different map");
            return GoapActionResult.Failed;
        }

        if (!_gun.TryGetGun(ent, out var gun))
        {
            CreateDump(ent, action, "no gun equipped");
            return GoapActionResult.Failed;
        }

        // Wield
        if (_wieldRequireQuery.HasComp(gun))
        {
            if (!_wieldQuery.TryComp(gun, out var wield))
            {
                ComponentNotFound<WieldableComponent>(ent, action, gun);
                return GoapActionResult.Failed;
            }

            if (!wield.Wielded && _wield.TryWield(gun, wield, ent))
            {
                CreateDump(ent, action, $"failed to wield gun `{ToPrettyString(gun)}` with GunRequiresWieldComponent");
                return GoapActionResult.Failed;
            }
        }
        else
        {
            if (_wieldQuery.TryComp(gun, out var wield))
                _wield.TryWield(gun, wield, ent);
        }

        // Chamber
        if (_chamberQuery.TryComp(gun, out var chamber) && chamber.BoltClosed == true)
        {
            return _npcTiming.EnqueueWait(ent,
                action,
                _random.NextFloat(0.05f, 0.33f),
                onFinish: () => _gun.SetBoltClosed(gun, chamber, false, ent));
        }

        var ammoEv = new GetAmmoCountEvent();
        RaiseLocalEvent(gun, ref ammoEv);

        if (_slots.TryGetSlot(gun, SharedGunSystem.ChamberSlot, out var chamberSlot)
            && !chamberSlot.HasItem
            && ammoEv.Count > 0)
        {
            return _npcTiming.EnqueueWait(ent,
                action,
                _random.NextFloat(0.05f, 0.33f),
                onFinish: () => _interaction.UseInHandInteraction(ent, gun));
        }

        if (ammoEv.Count == 0)
            return HandleEmptyAmmo(ent, action, gun);

        // --- LOS ---
        var worldPos = _transform.GetWorldPosition(xform);
        var targetPos = _transform.GetWorldPosition(targetXform);
        var distance = (targetPos - worldPos).Length();

        var targetInLos = state.GetValue(Gun.TargetInLosKey);

        if (_timing.CurTime >= nextLosCheck)
        {
            state.SetValue(Gun.NextLosCheckKey, _timing.CurTime + TimeSpan.FromSeconds(UnoccludedCooldown));

            var oldInLos = targetInLos;
            var collisionGroup = action.UseOpaqueForLosChecks
                ? CollisionGroup.Opaque
                : CollisionGroup.Impassable | CollisionGroup.InteractImpassable;
            targetInLos = _interaction.InRangeUnobstructed(ent.Owner, target, distance + 0.1f, collisionGroup);
            state.SetValue(Gun.TargetInLosKey, targetInLos);

            if (!oldInLos && targetInLos && TryGetValue(ent, action, Gun.SoundTargetInLos, out var sound))
                _audio.PlayPvs(sound, ent);
        }

        if (!targetInLos)
        {
            // Re-arm the shoot delay so returning into LOS requires "re-aiming".
            state.SetValue(Gun.ShootReadyAtKey, _timing.CurTime + TimeSpan.FromSeconds(action.ShootDelay));

            if (steering != null)
                steering.ForceMove = true;

            return action.RequireLos ? GoapActionResult.Failed : GoapActionResult.Continuing;
        }

        if (_timing.CurTime < state.GetValue(Gun.ShootReadyAtKey))
            return GoapActionResult.Continuing;

        // --- Aim & shoot ---
        var mapVelocity = targetBody.LinearVelocity;
        var targetSpot = targetPos + mapVelocity * distance / ShootSpeed;
        var goalRotation = (targetSpot - worldPos).ToWorldAngle();
        var rotationSpeed = TryGetValue(ent, action, GoapState.RotateSpeed, out var rs) ? new Angle(rs) : (Angle?)null;
        var frameTime = (float)_timing.FrameTime.TotalSeconds;

        if (!_rotate.TryRotateTo(ent,
                goalRotation,
                frameTime,
                action.AccuracyThreshold,
                rotationSpeed?.Theta ?? double.MaxValue,
                xform)
            || !_gun.CanShoot(gun))
            return GoapActionResult.Continuing;

        if (gun.Comp.NextFire <= _timing.CurTime)
            _gun.AttemptShoot(ent, gun, ShootCoords(), target);

        return GoapActionResult.Continuing;

        // Returns the coordinates for firing at a target, taking into account the required spread
        EntityCoordinates ShootCoords()
        {
            var coords = _mapManager.TryFindGridAt(xform.MapID, targetPos, out var gridUid, out var mapGrid)
                ? new EntityCoordinates(gridUid, _map.WorldToLocal(gridUid, mapGrid, targetSpot))
                : new EntityCoordinates(xform.MapUid!.Value, targetSpot);

            if (!_physicsQuery.TryComp(ent, out var agentBody))
                return coords;

            var offset = agentBody.AngularVelocity * MovementScatterFactor + 0.5f;
            /* TODO: skills refactor

            offset = _skills.GetInteractionResult(gun.Owner, ent.Owner, offset);

            switch (_skills.DoInteractionCheck(gun.Owner, ent.Owner, target))
            {
                case SkillCheckResult.AdditionalSuccess:
                    offset /= 2;
                    break;
                case SkillCheckResult.Fail:
                    offset *= 2;
                    break;
            }
            */

            var offsetVec = new Vector2(_random.NextFloat(-offset, offset), _random.NextFloat(-offset, offset));
            return new EntityCoordinates(coords.EntityId, coords.Position + offsetVec);
        }
    }

    private GoapActionResult HandleEmptyAmmo(
        Entity<GoapComponent> ent,
        Gun action,
        Entity<GunComponent> gun)
    {
        if (_rechargeQuery.HasComp(gun.Owner))
            return GoapActionResult.Continuing;

        if (!_slotsQuery.TryComp(gun, out var itemSlots))
        {
            ComponentNotFound<ItemSlotsComponent>(ent, action, gun);
            return GoapActionResult.Failed;
        }

        if (!_slots.TryGetSlot(gun, SharedGunSystem.MagazineSlot, out var slot, itemSlots))
        {
            CreateDump(ent,
                action,
                $"magazine slot `{SharedGunSystem.MagazineSlot}` in gun {ToPrettyString(gun)} not found");
            return GoapActionResult.Failed;
        }

        var state = ent.Comp.State;

        // Searching for an ammo
        if (slot.Whitelist != null)
            state.SetValue(MagazineWhitelistKey, slot.Whitelist);

        if (slot.Blacklist != null)
            state.SetValue(MagazineBlacklistKey, slot.Blacklist);

        _searcher.TryGetBestResult(ent, state, InventoryMagazineQuery, out var ammoUid);
        CreateDump(ent, action, $"query `{InventoryMagazineQuery}` returned: {ToPrettyString(ammoUid)}");

        if (ammoUid == null)
        {
            _searcher.TryGetBestResult(ent, state, NearbyMagazineQuery, out ammoUid);
            CreateDump(ent, action, $"query `{NearbyMagazineQuery}` returned: {ToPrettyString(ammoUid)}");
            state.SetValue(Gun.MovingToMagazineKey, ammoUid != null);
        }

        state.Remove(MagazineWhitelistKey);
        state.Remove(MagazineBlacklistKey);

        if (ammoUid == null)
        {
            CreateDump(ent, action, "out of ammo, no spare magazine/speedloader found");
            return GoapActionResult.Failed;
        }

        // If the magazine is within arm's reach, we change it right away
        if (!state.GetValue(Gun.MovingToMagazineKey))
            return ReplaceMagazine(ent, action, ammoUid.Value, gun: gun, slot: slot);

        // Else, going to the magazine
        state.SetValue(Gun.MovingToMagazineKey, true);
        state.SetValue(Gun.NearbyMagazineKey, ammoUid.Value);

        if (_jukeQuery.TryComp(ent, out var juke))
        {
            state.SetValue(Gun.PreviousJukeTypeKey, juke.JukeType);
            RemComp(ent, juke);
        }

        if (!_moveTo.StartupMovement(
                ent,
                action,
                Transform(ammoUid.Value).Coordinates,
                true,
                Gun.PathfindKey,
                GoapState.InteractRange,
                false))
            return GoapActionResult.Failed;

        return GoapActionResult.Continuing;
    }

    private GoapActionResult ReplaceMagazine(
        Entity<GoapComponent> ent,
        Gun action,
        EntityUid ammoUid,
        Entity<GunComponent>? gun = null,
        ItemSlot? slot = null)
    {
        if (gun == null)
        {
            if (!_gun.TryGetGun(ent, out var g))
            {
                CreateDump(ent, action, "no gun equipped");
                return GoapActionResult.Failed;
            }

            gun = g;
        }

        if (slot == null)
        {
            if (!_slotsQuery.TryComp(gun, out var itemSlots))
            {
                ComponentNotFound<ItemSlotsComponent>(ent, action, gun);
                return GoapActionResult.Failed;
            }

            if (!_slots.TryGetSlot(gun.Value, SharedGunSystem.MagazineSlot, out slot, itemSlots))
            {
                CreateDump(ent,
                    action,
                    $"magazine slot `{SharedGunSystem.MagazineSlot}` in gun {ToPrettyString(gun)} not found");
                return GoapActionResult.Failed;
            }
        }

        _wieldQuery.TryComp(gun, out var wield);

        // While reloading, release the two-handed grip on the weapon
        if (wield is { Wielded: true })
        {
            _npcTiming.EnqueueWait(ent,
                action,
                _random.NextFloat(0.15f, 0.33f),
                onFinish: () => _wield.TryUnwield(gun.Value, wield, ent));
        }

        // Remove the old magazine
        _npcTiming.EnqueueWait(ent,
            action,
            _random.NextFloat(0.33f, 0.5f),
            onFinish: () =>
            {
                if (_slots.TryEject(gun.Value, slot, ent, out var eject))
                {
                    // For dramatic effect, throw the magazine in a random direction.
                    _throw.TryThrow(eject.Value,
                        _random.NextAngle().ToVec(),
                        baseThrowSpeed: _random.NextFloat(1f, 10f),
                        user: ent);
                    // TODO: A skill check; if passed, the item will be stored in the inventory instead of being thrown away
                }
            });

        // Pickup and insert new magazine
        _npcTiming.EnqueueWait(ent,
            action,
            _random.NextFloat(0.33f, 0.55f),
            onFinish: () =>
            {
                if (!_hands.TryPickupAnyHand(ent, ammoUid))
                {
                    CreateDump(ent, action, $"failed to pickup {ToPrettyString(ammoUid)}");
                    return false;
                }

                if (!_slots.TryInsert(gun.Value, slot, ammoUid, ent))
                {
                    CreateDump(ent,
                        action,
                        $"failed to insert magazine `{ToPrettyString(ammoUid)}` in gun `{ToPrettyString(gun)}`");
                    return false;
                }

                return true;
            });

        // Taking up the weapon again with both hands
        _npcTiming.EnqueueWait(ent,
            action,
            0.05f,
            onFinish: () =>
            {
                if (wield != null)
                    _wield.TryWield(gun.Value, wield, ent);

                CreateDump(ent, action, "gun reloaded");
            });

        return GoapActionResult.Continuing;
    }
}
