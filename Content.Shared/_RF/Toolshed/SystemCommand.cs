using Robust.Shared.Toolshed;

namespace Content.Shared._RF.Toolshed;

public abstract class SystemCommand<T> : ToolshedCommand where T : EntitySystem
{
    private T? _system;
    public T System => _system ??= GetSys<T>();

    public Entity<TComp> EnsureEnt<TComp>(EntityUid uid) where TComp : Component, new()
        => new(uid, EnsureComp<TComp>(uid));
}
