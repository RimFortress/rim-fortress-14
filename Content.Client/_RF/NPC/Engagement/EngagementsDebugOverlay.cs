using System.Linq;
using System.Numerics;
using Content.Client._RF.Graphics;
using Content.Shared._RF.NPC.Engagement;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Client._RF.NPC.Engagement;

/// <summary>
/// Shows the situations (engagements): each in a color of its own, with a line from the middle of it to every participant. By every
/// participant it says in which situations it is and in which role, and what it is doing; in the corner there is the list of the
/// situations with who owns them and how many are in which role. See the <c>showengagements</c> command.
/// </summary>
public sealed partial class EngagementsDebugOverlay : Overlay
{
    [Dependency] private IEntityManager _entity = null!;
    [Dependency] private IResourceCache _resource = null!;
    [Dependency] private IGameTiming _timing = null!;

    private readonly EngagementDebugSystem _system;
    private readonly SharedTransformSystem _transform;
    private readonly Font _font;
    private readonly Font _bigFont;

    public override OverlaySpace Space => OverlaySpace.WorldSpace | OverlaySpace.ScreenSpace;

    public EngagementsDebugOverlay()
    {
        IoCManager.InjectDependencies(this);

        _system = _entity.System<EngagementDebugSystem>();
        _transform = _entity.System<SharedTransformSystem>();

        var regular = _resource.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Regular.ttf");
        _font = new VectorFont(regular, 10);
        _bigFont = new VectorFont(regular, 12);
    }

    public static Color EngagementColor(NetEntity engagement)
    {
        var hue = (float) (engagement.Id * 0.61803398875 % 1.0);
        return Color.FromHsv(new Vector4(hue, 0.75f, 1f, 1f));
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_system.Snapshot is not { } snapshot)
            return;

        if (args.Space == OverlaySpace.WorldSpace)
            DrawWorld(args, snapshot);
        else
            DrawScreen(args, snapshot);
    }

    /// <summary>
    /// Where the entity is now, the client asks the entity itself, so it moves smoothly. The ones that the client does not have are not drawn.
    /// </summary>
    private bool TryGetPosition(NetEntity net, MapId map, out Vector2 position)
    {
        position = default;

        if (!_entity.TryGetEntity(net, out var uid) || !_entity.EntityExists(uid))
            return false;

        var mapCoordinates = _transform.GetMapCoordinates(uid.Value);

        if (mapCoordinates.MapId != map)
            return false;

        position = mapCoordinates.Position;
        return true;
    }

    /// <summary>
    /// The middle of the participants that are on this map.
    /// </summary>
    private bool TryGetCenter(EngagementDebugInfo engagement, MapId map, out Vector2 center)
    {
        var sum = Vector2.Zero;
        var count = 0;

        foreach (var actor in engagement.Actors)
        {
            if (!TryGetPosition(actor.Uid, map, out var position))
                continue;

            sum += position;
            count++;
        }

        center = count == 0 ? default : sum / count;
        return count > 0;
    }

    private void DrawWorld(in OverlayDrawArgs args, EngagementsDebugMessage snapshot)
    {
        var handle = args.WorldHandle;

        foreach (var engagement in snapshot.Engagements.Where(_system.Matches))
        {
            if (!TryGetCenter(engagement, args.MapId, out var center))
                continue;

            var color = EngagementColor(engagement.Uid);

            // Those who are down or dead are drawn paler.
            foreach (var actor in engagement.Actors)
            {
                if (!TryGetPosition(actor.Uid, args.MapId, out var position))
                    continue;

                var alpha = actor.State is "Dead" or "Critical" ? 0.25f : 0.6f;
                handle.DrawLine(center, position, color.WithAlpha(alpha));
                handle.DrawCircle(position, 0.6f, color.WithAlpha(0.15f));
                handle.DrawCircle(position, 0.6f, color.WithAlpha(0.9f), false);
            }

            handle.DrawCircle(center, 0.4f, color);
            handle.DrawCircle(center, 0.4f, Color.Black, false);
        }
    }

    private void DrawScreen(in OverlayDrawArgs args, EngagementsDebugMessage snapshot)
    {
        if (args.ViewportControl == null)
            return;

        var handle = args.ScreenHandle;
        var map = args.MapId;
        var shown = snapshot.Engagements.Where(_system.Matches).OrderBy(x => x.Kind).ThenBy(x => x.Uid.Id).ToList();
        var age = _system.SnapshotTime == default ? 0f : (float) (_timing.CurTime - _system.SnapshotTime).TotalSeconds;

        // The list of the situations goes in the right corner, so it does not lie over the one of the settlements.
        var x = args.ViewportBounds.Width - 460f;
        var y = 70f;
        var filter = string.IsNullOrWhiteSpace(_system.Filter) ? "all" : _system.Filter;
        handle.DrawStringOutlined(_bigFont, new Vector2(x, y), $"Engagements: {shown.Count} of {snapshot.Engagements.Count} [{filter}] (updated {age:0.0}s ago)", Color.White);
        y += 16;

        foreach (var engagement in shown)
        {
            handle.DrawStringOutlined(_font, new Vector2(x, y), EngagementText(engagement), EngagementColor(engagement.Uid));
            y += 13;
        }

        // By every participant: where it is, in what and as whom, what it does.
        var labels = new Dictionary<NetEntity, (NetEntity Uid, string Name, string State, string Goal, List<(Color Color, string Text)> Lines)>();

        foreach (var engagement in shown)
        {
            foreach (var actor in engagement.Actors)
            {
                if (!labels.TryGetValue(actor.Uid, out var label))
                {
                    label = (actor.Uid, actor.Name, actor.State, actor.Goal, new());
                    labels[actor.Uid] = label;
                }

                label.Lines.Add((EngagementColor(engagement.Uid), $"{engagement.Kind}#{engagement.Uid.Id}: {actor.Role}"));
            }
        }

        foreach (var label in labels.Values)
        {
            if (!TryGetPosition(label.Uid, map, out var position))
                continue;

            var screen = args.ViewportControl.WorldToScreen(position);

            if (screen.X < -200 || screen.Y < -200 || screen.X > args.ViewportBounds.Width + 200 || screen.Y > args.ViewportBounds.Height + 200)
                continue;

            var at = screen + new Vector2(14, -14);
            var header = label.State.Length == 0 ? label.Name : $"{label.Name} ({label.State})";
            handle.DrawStringOutlined(_font, at, header, Color.White);

            foreach (var (color, text) in label.Lines)
            {
                at += new Vector2(0, 11);
                handle.DrawStringOutlined(_font, at, text, color);
            }

            if (label.Goal.Length > 0)
            {
                at += new Vector2(0, 11);
                handle.DrawStringOutlined(_font, at, $"> {label.Goal}", Color.LightGray);
            }
        }
    }

    private static string EngagementText(EngagementDebugInfo engagement)
    {
        var roles = string.Join(", ", engagement.Actors
            .GroupBy(a => a.Role)
            .OrderBy(g => g.Key)
            .Select(g => $"{g.Key} {g.Count()}"));
        var text = $"{engagement.Kind}#{engagement.Uid.Id}" + (engagement.Started ? string.Empty : " (waiting)") + $": {roles}";

        if (engagement.Invites.Count > 0)
            text += $"; invited {engagement.Invites.Count}";

        if (engagement.Owners.Count > 0)
            text += $" <- {string.Join(", ", engagement.Owners)}";

        return text;
    }
}
