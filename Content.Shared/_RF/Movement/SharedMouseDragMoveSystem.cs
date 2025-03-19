using System.Numerics;
using Robust.Shared.Serialization;

namespace Content.Shared._RF.Movement;

/// <summary>
/// This handles entities with <see cref="MouseDragMoveComponent"/>
/// </summary>
public abstract class SharedMouseDragMoveSystem : EntitySystem
{

}

[Serializable, NetSerializable]
public sealed class MouseDragToggleMessage : EntityEventArgs
{
    public bool Enabled { get; set; }
}

[Serializable, NetSerializable]
public sealed class MouseDragVelocityRequest : EntityEventArgs
{
    public Vector2 LinearVelocity { get; set; }
}
