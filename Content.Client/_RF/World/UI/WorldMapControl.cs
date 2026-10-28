using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Client._RF.Graphics;
using Content.Client._RF.Stylesheets;
using Content.Client._RF.UserInterface.Scope;
using Content.Client.Light.EntitySystems;
using Content.Client.UserInterface.Controls;
using Content.Shared._RF.Parallax.Fog;
using Content.Shared._RF.Pinpointer;
using Content.Shared._RF.World;
using Content.Shared._RF.World.Components;
using Content.Shared.Follower;
using Content.Shared.Light.Components;
using Content.Shared.Maps;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Pinpointer;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Collections;
using Robust.Shared.Input;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Client._RF.World.UI;

public sealed partial class WorldMapControl : MapGridControl
{
    [Dependency] private IInputManager _input = default!;
    [Dependency] private IResourceCache _cache = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IEntityNetworkManager _net = default!;

    private const byte ChunkSize = SharedBiomeSystem.ChunkSize;
    private const float MinDisplayedRange = 8f;
    private const float MaxDisplayedRange = 128f;
    private const float DefaultDisplayedRange = 48f;
    private const float NonScalePointsRadius = 5f;

    public EntityUid? MapUid
    {
        get;
        set
        {
            field = value;

            if (_attached)
                _chunks.Attach(value);
        }
    }

    public Color RoofColor = Color.Black;

    private bool _attached;
    private Vector2i _markerCoords;
    private EntityUid? _marker;

    private readonly TransformSystem _transform;
    private readonly WorldMapChunkSystem _chunks;

    private readonly Font _font;
    private readonly WorldMapContextWindow _contextWindow;
    private readonly WorldMarkerWindow _markedWindow;

    protected override bool Draggable => true;

    public WorldMapControl() : base(MinDisplayedRange, MaxDisplayedRange, DefaultDisplayedRange)
    {
        _font = _cache.SourceCodeStack();
        _contextWindow = UserInterfaceManager.CreateWindow<WorldMapContextWindow>();
        _markedWindow = UserInterfaceManager.CreateWindow<WorldMarkerWindow>();

        _transform = EntManager.System<TransformSystem>();
        _chunks = EntManager.System<WorldMapChunkSystem>();

        _contextWindow.AddMarkerButton.OnPressed += _ => OpenMarkerWindow();
        _contextWindow.ChangeMarkerButton.OnPressed += _ => OpenMarkerWindow();

        _contextWindow.TpHereButton.OnPressed += _ =>
        {
            if (_marker != null)
                _net.SendSystemNetworkMessage(new FollowEntityRequest(EntManager.GetNetEntity(_marker.Value)));
        };

        _contextWindow.DeleteMarkerButton.OnPressed += _ =>
        {
            if (_marker != null)
                EntManager.DeleteEntity(_marker);

            _contextWindow.Close();
        };

        _markedWindow.OnSubmit += args =>
        {
            var world = EntManager.System<SharedRimFortressWorldSystem>();

            // The beacon appears on the map by itself, the chunks listen to the components
            if (MapUid != null && _marker == null)
                world.CreateMapBeacon(MapUid.Value, _markerCoords, args.Color, args.Text);
            else if (_marker != null)
                world.ChangeBeacon(_marker.Value, args.Color, args.Text);
        };
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        var mousePos = GetLocalPosition(_input.MouseScreenPosition);
        _contextWindow.Close();

        if (args.Function != EngineKeyFunctions.UseSecondary)
            return;

        _marker = null;

        if (GetBeaconUnder(mousePos) is { } uid)
        {
            _marker = uid;
            _contextWindow.SetState(EntManager.IsClientSide(uid)
                ? WorldMapContextWindowState.Marker
                : WorldMapContextWindowState.NoChangeMarker);
        }
        else
        {
            _markerCoords = (Vector2i) MousePos();
            _contextWindow.SetState(WorldMapContextWindowState.World);
        }

        _contextWindow.Open(UserInterfaceManager.MousePositionScaled.Position);
    }

    private void OpenMarkerWindow()
    {
        _markedWindow.Open(UserInterfaceManager.MousePositionScaled.Position);
        _contextWindow.Close();
    }

    public void CenterToCoordinates(Vector2 coordinates)
    {
        Offset = coordinates;
    }

    /// <summary>
    /// Returns mouse coordinates converted to local world coordinates
    /// </summary>
    public Vector2 MousePos()
    {
        if (_input.MouseScreenPosition is not { IsValid: true } screen
            || !GlobalPixelRect.Contains((Vector2i) screen.Position))
            return Vector2i.Zero;

        var pos = GetLocalPosition(screen);
        return InverseMapPosition(pos);
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();

        _attached = true;
        _chunks.Attach(MapUid);
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();

        // The baked chunks are kept, so the map is ready at once the next time it opens
        _attached = false;
    }

    /// <summary>
    /// Position of the beacon on the map, in the tiles of the world
    /// </summary>
    private bool TryGetBeaconPos(EntityUid uid, [NotNullWhen(true)] out NavMapBeaconComponent? beacon, out Vector2 pos)
    {
        pos = default;

        if (MapUid == null
            || !EntManager.TryGetComponent(uid, out beacon)
            || beacon.Text == null
            || !EntManager.TryGetComponent(uid, out TransformComponent? xform)
            || xform.MapUid != MapUid)
        {
            beacon = null;
            return false;
        }

        pos = _transform.GetWorldPosition(xform).Floored() + new Vector2(0.5f);

        // Beacons are shown only where the map is known
        return _chunks.HasChunk(WorldMapChunkSystem.ToChunk((Vector2i) pos.Floored()));
    }

    public EntityUid? GetBeaconUnder(Vector2 screenPos)
    {
        foreach (var uid in _chunks.Beacons)
        {
            if (TryGetBeaconPos(uid, out _, out var pos)
                && Vector2.Distance(ScaleOffsetPos(pos), screenPos) <= NonScalePointsRadius)
                return uid;
        }

        return null;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        if (MapUid == null)
            return;

        var viewBox = (UIBox2) PixelRect.Translated(-PixelPosition);

        _chunks.Bake(handle);

        // Chunks that are visible: the corners of the control converted to the tiles of the world
        var a = InverseMapPosition(viewBox.TopLeft);
        var b = InverseMapPosition(viewBox.BottomRight);
        var from = WorldMapChunkSystem.ToChunk((Vector2i) Vector2.Min(a, b).Floored());
        var to = WorldMapChunkSystem.ToChunk((Vector2i) Vector2.Max(a, b).Floored());

        for (var x = from.X; x <= to.X; x++)
        {
            for (var y = from.Y; y <= to.Y; y++)
            {
                var chunk = new Vector2i(x, y);

                if (!_chunks.TryGetChunk(chunk, out var texture))
                    continue;

                var box = ScaleOffsetBox(Box2.FromDimensions(chunk * ChunkSize, new Vector2(ChunkSize)));
                handle.DrawTextureRect(texture, box);
            }
        }

        foreach (var uid in _chunks.Beacons)
        {
            if (!TryGetBeaconPos(uid, out var beacon, out var pos))
                continue;

            var screen = ScaleOffsetPos(pos);

            if (!viewBox.Contains(screen))
                continue;

            handle.DrawCircle(screen, NonScalePointsRadius, beacon.Color);
            handle.DrawStringOutlined(_font, ScaleOffsetPos(pos + Vector2.One), beacon.Text!, beacon.Color);
        }

        if (_player.LocalSession is { } session
            && EntManager.TryGetComponent(_player.LocalEntity, out TransformComponent? playerForm)
            && EntManager.TryGetComponent(_player.LocalEntity, out RimFortressPlayerComponent? player))
        {
            var pos = _transform.GetWorldPosition(playerForm).Floored() + new Vector2(0.5f);
            var screen = ScaleOffsetPos(pos);

            if (viewBox.Contains(screen))
            {
                handle.DrawCircle(screen, NonScalePointsRadius, player.FactionColor);
                handle.DrawStringOutlined(_font, ScaleOffsetPos(pos + Vector2.One), session.Data.UserName, player.FactionColor);
            }
        }
    }

    private Vector2 ScaleOffsetPos(Vector2 pos)
    {
        return ScalePosition(new Vector2(pos.X - Offset.X, (pos.Y - Offset.Y) * -1));
    }

    private UIBox2 ScaleOffsetBox(Box2 box)
    {
        var lt = ScaleOffsetPos(new Vector2(box.Left, box.Top));
        var rb = ScaleOffsetPos(new Vector2(box.Right, box.Bottom));
        return new UIBox2(lt, rb);
    }
}
