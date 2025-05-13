using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;

namespace Content.Client._RF.World;

public sealed class WorldOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IResourceCache _resource = default!;

    private readonly RimFortressWorldSystem _world;

    private readonly Font _font;

    public WorldOverlay()
    {
        IoCManager.InjectDependencies(this);

        _world = _entityManager.System<RimFortressWorldSystem>();
        _font = new VectorFont(_resource.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Regular.ttf"), 10);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.ViewportControl == null)
            return;

        var handle = args.ScreenHandle;

        foreach (var (player, coords) in _world.Settlemets)
        {
            if (!_player.TryGetSessionByEntity(player, out var session))
                continue;

            var index = 1;
            foreach (var coord in coords)
            {
                if (!args.WorldAABB.Contains(coord.Position))
                    continue;

                var screenPos = args.ViewportControl.WorldToScreen(coord.Position);
                handle.DrawCircle(screenPos, 20f, Color.Yellow);

                var text = $"{session.Data.UserName}\nSettlement: {index}";
                handle.DrawString(_font, screenPos + new Vector2(20, -20), text, Color.White);

                index++;
            }
        }
    }
}
