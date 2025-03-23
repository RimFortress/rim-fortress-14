using Robust.Client.Graphics;
using Robust.Shared.Enums;

namespace Content.Client._RF.NPC;

public sealed class NPCControlOverlay(NPCControlSystem npcControl) : Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (npcControl.StartPoint is not { } start
            || npcControl.EndPoint is not { } end)
            return;

        var area = new Box2(start.Position, end.Position);
        args.WorldHandle.DrawRect(area, Color.White, false);
    }
}
