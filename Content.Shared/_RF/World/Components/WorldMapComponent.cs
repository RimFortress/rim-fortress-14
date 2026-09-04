using Robust.Shared.GameStates;

namespace Content.Shared._RF.World.Components;

/// <summary>
/// A component that marks the game world map.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class WorldMapComponent : Component;
