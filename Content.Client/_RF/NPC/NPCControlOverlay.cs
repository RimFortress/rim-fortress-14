using Robust.Client.Graphics;
using Robust.Shared.Enums;

namespace Content.Client._RF.NPC;

public sealed class NPCControlOverlay(NPCControlSystem npcControl) : Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    protected override void Draw(in OverlayDrawArgs args)
    {
        foreach (var entity in npcControl.Selected)
        {
            if (npcControl.Targets.TryGetValue(entity, out var target)
                && target is { } coordinates)
            {
                args.WorldHandle.DrawCircle(coordinates.Position, 0.35f, Color.LightGray.WithAlpha(0.80f), false);
                args.WorldHandle.DrawCircle(coordinates.Position, 0.34f, Color.LightGray.WithAlpha(0.80f), false);
            }
        }

        if (npcControl is { StartPoint: { } start, EndPoint: { } end })
        {
            var area = new Box2(start.Position, end.Position);
            args.WorldHandle.DrawRect(area, Color.White, false);
        }
    }
}
