using System.Numerics;
using Content.Shared._RF.NPC;
using Content.Shared.Hands.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Client._RF.NPC;

public sealed class NpcControlOverlay : Overlay
{
    public override bool RequestScreenTexture => true;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    [ValidatePrototypeId<ShaderPrototype>]
    private const string SelectShader = "DottedOutline";
    [ValidatePrototypeId<ShaderPrototype>]
    private const string PointCircleShader = "DottedCircle";
    [ValidatePrototypeId<ShaderPrototype>]
    private const string PointLineShader = "DottedLine";

    private readonly NpcControlSystem _npcControl;
    private readonly SharedTransformSystem _transform;
    private readonly IEntityManager _entityManager;
    private readonly IPrototypeManager _prototype;

    private readonly Color _selectColor = Color.LightGray.WithAlpha(0.80f);
    private readonly Color _attackColor = Color.Red.WithAlpha(0.80f);
    private readonly Color _pickUpColor = Color.Yellow.WithAlpha(0.80f);

    private readonly HashSet<SpriteComponent> _highlightedSprites = new();

    public NpcControlOverlay(
        NpcControlSystem npcControl,
        SharedTransformSystem transform,
        IPrototypeManager prototype,
        IEntityManager entityManager)
    {
        _npcControl = npcControl;
        _transform = transform;
        _entityManager = entityManager;
        _prototype = prototype;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        foreach (var sprite in _highlightedSprites)
        {
            sprite.PostShader = null;
            sprite.RenderOrder = 0;
        }

        _highlightedSprites.Clear();

        foreach (var entity in _npcControl.Selected)
        {
            SetShader(entity, _selectColor);

            if (!_npcControl.Tasks.TryGetValue(entity, out var task)
                || !_entityManager.TryGetComponent(entity, out TransformComponent? entityForm))
                continue;

            var start = _transform.ToMapCoordinates(entityForm.Coordinates);
            var end = _transform.ToMapCoordinates(task.Coordinates);

            switch (task.Type)
            {
                case NpcTaskType.Move:
                {
                    if ((end.Position - start.Position).Length() < 0.5f)
                        break;

                    DrawPointCircle(args, end, _selectColor);
                    DrawLine(args, _selectColor, start, end);
                    break;
                }
                case NpcTaskType.Attack:
                {
                    if (_entityManager.TryGetComponent(task.Target, out MobStateComponent? state)
                        && state.CurrentState != MobState.Alive)
                        break;

                    SetShader(task.Target, _attackColor);
                    DrawLine(args, _attackColor, start, end, 0.5f);
                    break;
                }
                case NpcTaskType.PickUp:
                {
                    if (_entityManager.TryGetComponent(entity, out HandsComponent? hands)
                        && hands.ActiveHand?.HeldEntity == task.Target)
                        break;

                    SetShader(task.Target, _pickUpColor);
                    DrawLine(args, _pickUpColor, start, end, 0.5f);
                    break;
                }
                default:
                    throw new NotImplementedException();
            }
        }

        if (_npcControl is { StartPoint: { } startPoint, EndPoint: { } endPoint })
        {
            var area = new Box2(startPoint.Position, endPoint.Position);
            args.WorldHandle.DrawRect(area, Color.White, false);
        }
    }

    private void SetShader(EntityUid entity, Color color)
    {
        if (!_entityManager.TryGetComponent(entity, out SpriteComponent? sprite)
            || _highlightedSprites.Contains(sprite)
            || !sprite.Visible)
            return;

        var shader = _prototype.Index<ShaderPrototype>(SelectShader).InstanceUnique();
        _highlightedSprites.Add(sprite);
        shader.SetParameter("color", color);

        sprite.PostShader = shader;
        sprite.RenderOrder = _entityManager.CurrentTick.Value;
    }

    private void DrawLine(
        in OverlayDrawArgs args,
        Color color,
        MapCoordinates start,
        MapCoordinates end,
        float? minLength = null)
    {
        if (start.Position == Vector2.Zero // Probably out-of-sight entity
            || end.Position == Vector2.Zero
            || minLength != null
            && (start.Position - end.Position).Length() < minLength)
            return;

        var shader = _prototype.Index<ShaderPrototype>(PointLineShader).InstanceUnique();
        args.WorldHandle.UseShader(shader);

        var screenEnd = args.Viewport.WorldToLocal(end.Position);
        screenEnd.Y = args.Viewport.Size.Y - screenEnd.Y;

        var screeStart = args.Viewport.WorldToLocal(start.Position);
        screeStart.Y = args.Viewport.Size.Y - screeStart.Y;

        // Find the number of pixels in the coordinate unit to scale the size correctly.
        // It can probably be done in a better way, but my head is about to explode
        var unit = (args.Viewport.WorldToLocal(start.Position + Vector2.UnitX) - args.Viewport.WorldToLocal(start.Position)).X;
        shader.SetParameter("unit", unit);

        shader.SetParameter("color", color);
        shader.SetParameter("start", screenEnd);
        shader.SetParameter("end", screeStart);

        args.WorldHandle.DrawRect(new Box2(start.Position, end.Position), Color.White);
    }

    private void DrawPointCircle(in OverlayDrawArgs args, MapCoordinates worldCoords, Color color)
    {
        var shader = _prototype.Index<ShaderPrototype>(PointCircleShader).InstanceUnique();
        args.WorldHandle.UseShader(shader);

        var position = args.Viewport.WorldToLocal(worldCoords.Position);
        var unit = (args.Viewport.WorldToLocal(worldCoords.Position + Vector2.UnitX) - position).X;
        shader.SetParameter("unit", unit);

        position.Y = args.Viewport.Size.Y - position.Y;
        shader.SetParameter("position", position);

        shader.SetParameter("color", color);

        args.WorldHandle.DrawRect(Box2.CenteredAround(worldCoords.Position, Vector2.One), Color.White);
    }
}
