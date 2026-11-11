using System.Linq;
using System.Numerics;
using Content.Server._RF.NPC.Pathfinding.Systems;
using Content.Shared._RF.Conversation;
using Content.Shared._RF.Conversation.Components;
using Content.Shared._RF.NPC.Pathfinding;
using Robust.Shared.Map;

namespace Content.Server._RF.Conversation;

/// <summary>
/// Finds the place for a conversation (<see cref="ConversationPlaceEvent"/>) by the navigation map:
/// a place that the participants can stand at (not blocked, no lava, not behind a wall from them),
/// and a spot for each of them round it, apart from the others, so they do not stand in a crowd on one tile.
/// </summary>
public sealed partial class ConversationPlacementSystem : EntitySystem
{
    [Dependency] private RfPathfindingSystem _pathfinding = null!;

    [SubscribeLocalEvent]
    private void OnPlace(Entity<ConversationComponent> ent, ref ConversationPlaceEvent args)
    {
        if (args.Actors.Count == 0 || !args.Preferred.EntityId.IsValid())
            return;

        var settings = ent.Comp.Place;
        var lead = args.Actors[0];
        var near = _pathfinding.FindPlaces(lead, args.Preferred, settings.SearchRadius, settings.SearchNodes);

        if (near.Count == 0)
            return;

        // Everybody can get there: the place is joined to every one of them by what the profiles go through.
        var wide = _pathfinding.FindPlaces(lead, args.Preferred, settings.ReachRadius, settings.ReachNodes);

        foreach (var actor in args.Actors)
        {
            if (!IsNear(wide, actor, settings.ReachMargin))
                return;
        }

        var preferred = args.Preferred.Position;
        near.Sort((a, b) => Vector2
            .Distance(a.Poly.Box.Center, preferred)
            .CompareTo(Vector2.Distance(b.Poly.Box.Center, preferred)));

        foreach (var (poly, _) in near.Take(settings.MaxCenters).ToList())
        {
            var center = poly.Box.Center;

            if (TrySpots(args, settings, near.Select(x => x.Poly).ToList(), poly.GraphUid, center))
            {
                args.Center = new EntityCoordinates(poly.GraphUid, center);
                args.Handled = true;
                return;
            }
        }

        // No room for a circle of them: the place is the one that is the closest, and they all go to it as they would.
        args.Center = new EntityCoordinates(near[0].Poly.GraphUid, near[0].Poly.Box.Center);
        args.Spots.Clear();
        args.Handled = true;
    }

    /// <summary>
    /// Whether any of the nodes that are places is close to the participant, so that it is joined to them by what it goes through.
    /// </summary>
    private bool IsNear(List<(RfPathPoly Poly, float Cost)> places, EntityUid actor, float margin)
    {
        var position = Transform(actor).Coordinates.Position;

        foreach (var (poly, _) in places)
        {
            if (poly.GraphUid == Transform(actor).ParentUid && Vector2.Distance(poly.Box.Center, position) <= margin)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Takes a spot for each participant round the center: the nodes at the radius of the circle, in the directions that divide it evenly,
    /// each apart from the ones taken, and gives them to the participants that are the closest to them.
    /// </summary>
    private bool TrySpots(ConversationPlaceEvent args, ConversationPlaceSettings settings, List<RfPathPoly> places, EntityUid graph, Vector2 center)
    {
        var count = args.Actors.Count;
        var radius = Math.Max(settings.MinSpotRadius, settings.SpotRadiusPerActor * count);
        var ring = places
            .Where(x => x.GraphUid == graph
                        && Math.Abs(Vector2.Distance(x.Box.Center, center) - radius) <= settings.SpotBand)
            .Select(x => x.Box.Center)
            .ToList();

        if (ring.Count < count)
            return false;

        var taken = new List<Vector2>();

        for (var i = 0; i < count; i++)
        {
            var wanted = MathF.Tau * i / count;
            Vector2? best = null;
            var bestDifference = float.MaxValue;

            foreach (var candidate in ring)
            {
                if (taken.Any(t => Vector2.Distance(t, candidate) < settings.MinSeparation))
                    continue;

                var direction = candidate - center;
                var difference = (float)Math.Abs(Angle
                    .ShortestDistance(new Angle(Math.Atan2(direction.Y, direction.X)), new Angle(wanted))
                    .Theta);

                if (difference >= bestDifference)
                    continue;

                best = candidate;
                bestDifference = difference;
            }

            if (best == null)
                return false;

            taken.Add(best.Value);
        }

        // The ones who are the closest to a spot go to it, the others are given what is left.
        var pairs = new List<(EntityUid Actor, Vector2 Spot, float Distance)>();

        foreach (var actor in args.Actors)
        {
            var position = Transform(actor).Coordinates.Position;

            foreach (var spot in taken)
            {
                pairs.Add((actor, spot, Vector2.Distance(position, spot)));
            }
        }

        pairs.Sort((a, b) => a.Distance.CompareTo(b.Distance));
        args.Spots.Clear();
        var used = new HashSet<Vector2>();

        foreach (var (actor, spot, _) in pairs)
        {
            if (args.Spots.ContainsKey(actor) || !used.Add(spot))
                continue;

            args.Spots[actor] = new EntityCoordinates(graph, spot);
        }

        return args.Spots.Count == count;
    }
}
