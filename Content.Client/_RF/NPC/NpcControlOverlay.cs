using System.Numerics;
using Content.Shared._RF.NPC;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client._RF.NPC;

public sealed class NpcControlOverlay : Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    [ValidatePrototypeId<ShaderPrototype>]
    private const string SelectShader = "SelectionOutlineWhite";
    [ValidatePrototypeId<ShaderPrototype>]
    private const string AttackShader = "SelectionOutlineRed";
    [ValidatePrototypeId<ShaderPrototype>]
    private const string PickUpShader = "SelectionOutlineYellow";

    private readonly NpcControlSystem _npcControl;
    private readonly SharedTransformSystem _transform;
    private readonly IEntityManager _entityManager;

    private readonly Color _selectColor = Color.LightGray.WithAlpha(0.80f);
    private readonly Color _attackColor = Color.Red.WithAlpha(0.80f);
    private readonly Color _pickUpColor = Color.Yellow.WithAlpha(0.80f);
    private readonly ShaderInstance _selectShader;
    private readonly ShaderInstance _attackShader;
    private readonly ShaderInstance _pickUpShader;

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
        _selectShader = prototype.Index<ShaderPrototype>(SelectShader).InstanceUnique();
        _attackShader = prototype.Index<ShaderPrototype>(AttackShader).InstanceUnique();
        _pickUpShader = prototype.Index<ShaderPrototype>(PickUpShader).InstanceUnique();
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
            if (!_entityManager.TryGetComponent(entity, out SpriteComponent? sprite) || !sprite.Visible)
                continue;

            _highlightedSprites.Add(sprite);
            sprite.PostShader = _selectShader;
            sprite.RenderOrder = _entityManager.CurrentTick.Value;

            if (!_npcControl.Tasks.TryGetValue(entity, out var task))
                continue;

            switch (task.Type)
            {
                case NpcTaskType.Move:
                {
                    var startPos = _transform.GetWorldPosition(entity);
                    var direction = task.Coordinates.Position - startPos;
                    var endPos = task.Coordinates.Position + Vector2.Normalize(direction) * -0.35f;

                    if (direction.Length() > 0.5f)
                    {
                        args.WorldHandle.DrawCircle(task.Coordinates.Position, 0.35f, _selectColor, false);
                        args.WorldHandle.DrawCircle(task.Coordinates.Position, 0.34f, _selectColor, false);
                        args.WorldHandle.DrawLine(startPos, endPos, _selectColor);
                    }

                    break;
                }
                case NpcTaskType.Attack:
                {
                    if (_entityManager.TryGetComponent(task.Target, out SpriteComponent? attackSprite)
                        && !_highlightedSprites.Contains(attackSprite)
                        && attackSprite.Visible)
                    {
                        _highlightedSprites.Add(attackSprite);
                        attackSprite.PostShader = _attackShader;
                        attackSprite.RenderOrder = _entityManager.CurrentTick.Value;
                    }

                    if (!_entityManager.TryGetComponent(task.Target, out TransformComponent? xform))
                        break;

                    var startPos = _transform.GetWorldPosition(entity);
                    var endPos = xform.Coordinates.Position;
                    var direction = endPos - startPos;
                    if (direction.Length() > 0.5f)
                        args.WorldHandle.DrawLine(startPos, endPos, _attackColor);

                    break;
                }
                case NpcTaskType.PickUp:
                {
                    if (_entityManager.TryGetComponent(task.Target, out SpriteComponent? pickUpSprite)
                        && !_highlightedSprites.Contains(pickUpSprite)
                        && pickUpSprite.Visible)
                    {
                        _highlightedSprites.Add(pickUpSprite);
                        pickUpSprite.PostShader = _pickUpShader;
                        pickUpSprite.RenderOrder = _entityManager.CurrentTick.Value;
                    }

                    if (!_entityManager.TryGetComponent(task.Target, out TransformComponent? xform)
                        || xform.Coordinates.Position == Vector2.Zero) // Entities in the inventory have zero coordinates
                        break;

                    var startPos = _transform.GetWorldPosition(entity);
                    var endPos = xform.Coordinates.Position;
                    var direction = endPos - startPos;
                    if (direction.Length() > 0.5f)
                        args.WorldHandle.DrawLine(startPos, endPos, _pickUpColor);

                    break;
                }
                default:
                    throw new NotImplementedException();
            }
        }

        if (_npcControl is { StartPoint: { } start, EndPoint: { } end })
        {
            var area = new Box2(start.Position, end.Position);
            args.WorldHandle.DrawRect(area, Color.White, false);
        }
    }
}
