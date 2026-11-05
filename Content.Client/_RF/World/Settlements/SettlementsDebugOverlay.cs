using System.Linq;
using System.Numerics;
using Content.Client._RF.Graphics;
using Content.Shared._RF.World.Settlements;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Map;

namespace Content.Client._RF.World.Settlements;

/// <summary>
/// Shows the settlements: each in a color of its own, with the faint circle of how far it reaches (what is inside is adopted by it), the
/// circle of how far its things are, its center, its residents with a line to each, and what is said about it in words (the residents,
/// the wealth by the categories, the points of the narrator). What it owns is not drawn here: the sprites of those are painted in its
/// color (see <see cref="SettlementDebugSystem"/>), and the sprites of what waits with the player for a settlement are orange. See the
/// <c>showsettlements</c> command.
/// </summary>
public sealed class SettlementsDebugOverlay : Overlay
{
    [Dependency] private IEntityManager _entity = null!;
    [Dependency] private IResourceCache _resource = null!;

    private readonly SettlementDebugSystem _system;
    private readonly SharedTransformSystem _transform;
    private readonly Font _font;
    private readonly Font _bigFont;

    private static readonly Color WaitingColor = Color.Orange;

    // Under the entities: the area is a tint of the ground, the sprites of the entities are to be seen above it.
    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowEntities | OverlaySpace.ScreenSpace;

    public SettlementsDebugOverlay()
    {
        IoCManager.InjectDependencies(this);

        _system = _entity.System<SettlementDebugSystem>();
        _transform = _entity.System<SharedTransformSystem>();

        var regular = _resource.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Regular.ttf");
        _font = new VectorFont(regular, 10);
        _bigFont = new VectorFont(regular, 12);
    }

    /// <summary>
    /// The color of a settlement: it is the same all the time, and the neighbours are far from each other in the colors.
    /// </summary>
    public static Color SettlementColor(NetEntity settlement)
    {
        var hue = (float) (settlement.Id * 0.61803398875 % 1.0);
        return Color.FromHsv(new Vector4(hue, 0.7f, 1f, 1f));
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_system.Snapshot is not { } snapshot)
            return;

        if (args.Space == OverlaySpace.WorldSpaceBelowEntities)
            DrawWorld(args, snapshot);
        else
            DrawScreen(args, snapshot);
    }

    private bool TryGetPosition(NetCoordinates coordinates, MapId map, out Vector2 position)
    {
        position = default;
        var local = _entity.GetCoordinates(coordinates);

        if (!local.IsValid(_entity))
            return false;

        var mapCoordinates = _transform.ToMapCoordinates(local);

        if (mapCoordinates.MapId != map)
            return false;

        position = mapCoordinates.Position;
        return true;
    }

    private void DrawWorld(in OverlayDrawArgs args, SettlementsDebugMessage snapshot)
    {
        var handle = args.WorldHandle;

        foreach (var settlement in snapshot.Settlements)
        {
            if (!TryGetPosition(settlement.Coordinates, args.MapId, out var center))
                continue;

            var color = SettlementColor(settlement.Uid);

            // The area of the settlement is tinted: it is as large as the group of its things. How far it reaches (what is
            // beyond it, and within this circle, is adopted by it) is the line only: it is so large that a tint of it would be the world.
            if (settlement.Reach > 0f)
                handle.DrawCircle(center, settlement.Reach, color.WithAlpha(0.5f), false);

            handle.DrawCircle(center, Math.Max(settlement.Radius, 0.5f), color.WithAlpha(0.12f));
            handle.DrawCircle(center, Math.Max(settlement.Radius, 0.5f), color.WithAlpha(0.7f), false);

            // The residents are tied to the center of the settlement, a settlement that waits to be disbanded is paler.
            var lineColor = color.WithAlpha(settlement.Dissolving ? 0.15f : 0.35f);

            foreach (var member in settlement.Members)
            {
                if (member.Category == "Pop" && TryGetPosition(member.Coordinates, args.MapId, out var position))
                    handle.DrawLine(center, position, member.Away >= 0f ? Color.White.WithAlpha(0.5f) : lineColor);
            }

            // The center of it.
            handle.DrawCircle(center, 1.2f, color);
            handle.DrawCircle(center, 1.2f, Color.Black, false);
        }
    }

    private void DrawScreen(in OverlayDrawArgs args, SettlementsDebugMessage snapshot)
    {
        if (args.ViewportControl == null)
            return;

        var handle = args.ScreenHandle;
        var map = args.MapId;
        var age = (_system.SnapshotTime == default ? 0f : (float) (IoCManager.Resolve<Robust.Shared.Timing.IGameTiming>().CurTime - _system.SnapshotTime).TotalSeconds);

        // The legend: what is what.
        var y = 70f;
        handle.DrawStringOutlined(_bigFont, new Vector2(10, y), $"Settlements: {snapshot.Settlements.Count} (updated {age:0.0}s ago)", Color.White);
        y += 16;

        foreach (var settlement in snapshot.Settlements.OrderBy(x => x.Uid.Id))
        {
            var color = SettlementColor(settlement.Uid);
            handle.DrawStringOutlined(_font, new Vector2(10, y), $"#{settlement.Uid.Id} {settlement.PlayerName}: {settlement.Residents} residents, {settlement.Owned} owned" + (settlement.Dissolving ? " (dissolving)" : string.Empty), color);
            y += 13;
        }

        handle.DrawStringOutlined(_font, new Vector2(10, y), $"Waiting for a settlement (the player's, orange): {snapshot.Waiting.Count}", WaitingColor);
        y += 13;
        handle.DrawStringOutlined(_font, new Vector2(10, y), "the sprites are painted in the color of the owner; a white line is a pop that is away from the group", Color.LightGray);

        // The text by each settlement and by each pop that is away.
        foreach (var settlement in snapshot.Settlements)
        {
            if (!TryGetPosition(settlement.Coordinates, map, out var center))
                continue;

            var screen = args.ViewportControl.WorldToScreen(center);
            var color = SettlementColor(settlement.Uid);

            if (screen.X < -400 || screen.Y < -400 || screen.X > args.ViewportBounds.Width + 400 || screen.Y > args.ViewportBounds.Height + 400)
                continue;

            handle.DrawStringOutlined(_bigFont, screen + new Vector2(24, -24), SettlementText(settlement), color);

            foreach (var member in settlement.Members.Where(x => x.Away >= 0f))
            {
                if (TryGetPosition(member.Coordinates, map, out var position))
                    handle.DrawStringOutlined(_font, args.ViewportControl.WorldToScreen(position) + new Vector2(10, -6), $"away {member.Away:0}s", Color.White);
            }
        }
    }

    private static string SettlementText(SettlementDebugInfo settlement)
    {
        var text = $"Settlement #{settlement.Uid.Id} ({settlement.PlayerName})\n" +
                   $"residents {settlement.Residents}, owns {settlement.Owned}, radius {settlement.Radius:0}, reach {settlement.Reach:0}";

        if (settlement.Dissolving)
            text += $"\nDISSOLVING in {settlement.DissolveIn:0}s";

        if (settlement.Wealth.Count > 0)
            text += "\nwealth " + string.Join(", ", settlement.Wealth.OrderBy(x => x.Key).Select(x => $"{x.Key} {x.Value:0}"));

        if (settlement.EventPoints >= 0)
            text += $"\nevent points {settlement.EventPoints} (waiting {settlement.WaitPoints}, bonus {settlement.BonusPoints})";

        return text;
    }
}
