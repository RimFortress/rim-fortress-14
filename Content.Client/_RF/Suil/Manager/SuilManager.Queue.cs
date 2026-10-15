using Content.Client._RF.Suil;
using Robust.Client.UserInterface;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Suil;

public sealed partial class SuilManager
{
    // A control the styles of which are queued is restyled once however many times it changed before the flush;
    // a few passes are enough for the restyling that its own restyling causes (a pseudo-class set by a property).
    private const int MaxFlushPasses = 4;

    /// <summary>
    /// How many controls are restyled in a frame at most, like <see cref="UserInterfaceManager.ControlUpdateLimit"/>;
    /// the rest waits for the next frame. Ignored with <see cref="CVars.UIObeyUpdateLimits"/> off.
    /// </summary>
    public const int StyleUpdateLimit = 25_000;

    [Dependency] private IConfigurationManager _cfg = null!;

    private readonly Queue<UiElement> _queue = new();
    private readonly List<(int Depth, UiElement Control)> _batch = new();
    private RfStyleFlusher? _flusher;
    private bool _flushing;

    /// <inheritdoc/>
    public void QueueStyleUpdate(UiElement control) => Enqueue(control, SuilAncestorReach.None);

    /// <inheritdoc/>
    public void QueueStyleUpdate(UiElement control, string? key)
        => Enqueue(control, Stylesheet.GetAncestorReach(key));

    private void Enqueue(UiElement control, SuilAncestorReach reach)
    {
        control.SuilStyleDirty = true;

        if (reach > control.SuilPendingReach)
            control.SuilPendingReach = reach;

        if (control.SuilQueued)
            return;

        if (!EnsureFlusher())
        {
            // No UI to flush from yet (the UI is being built before the first frame).
            Process(control);
            return;
        }

        control.SuilQueued = true;
        _queue.Enqueue(control);
    }

    private bool EnsureFlusher()
    {
        if (_flusher != null)
            return true;

        if (_ui.RootControl is not { } root)
            return false;

        _flusher = new RfStyleFlusher(FlushStyleUpdates);
        root.AddChild(_flusher);
        return true;
    }

    /// <inheritdoc/>
    public void FlushStyleUpdates()
    {
        if (_flushing)
            return;

        _flushing = true;
        var total = 0;
        var obeyLimit = _cfg.GetCVar(CVars.UIObeyUpdateLimits);

        try
        {
            for (var pass = 0; pass < MaxFlushPasses && _queue.Count > 0; pass++)
            {
                _batch.Clear();

                while (_queue.TryDequeue(out var control))
                {
                    // Whatever changes the control from now on is queued again, for the next pass.
                    control.SuilQueued = false;
                    _batch.Add((Depth(control), control));
                }

                // Parents first, so that a child sees the variables its parent has just got, and so that restyling
                // a parent, which restyles its descendants when its variables change, makes their own entries no-ops.
                _batch.Sort(static (a, b) => a.Depth.CompareTo(b.Depth));

                for (var i = 0; i < _batch.Count; i++)
                {
                    var control = _batch[i].Control;

                    if (obeyLimit && total >= StyleUpdateLimit)
                    {
                        // Keep what is left, in the order it was, for the next frame.
                        for (var j = i; j < _batch.Count; j++)
                        {
                            _batch[j].Control.SuilQueued = true;
                            _queue.Enqueue(_batch[j].Control);
                        }

                        _sawmill.Warning(
                            $"Hit style update limit. Queued: {_queue.Count}. Next in queue: {control}. Parent: {control.Parent}");
                        return;
                    }

                    total++;

                    try
                    {
                        Process(control);
                    }
                    catch (Exception e)
                    {
                        _sawmill.Error($"Failed to restyle {control}: {e}");
                    }
                }
            }
        }
        finally
        {
            _batch.Clear();
            _flushing = false;
        }
    }

    /// <inheritdoc/>
    public void FlushStyleUpdate(UiElement control)
    {
        if (control.SuilStyleDirty || control.SuilPendingReach != SuilAncestorReach.None)
            Process(control);
    }

    private void Process(UiElement control)
    {
        if (control.Disposed)
        {
            control.SuilStyleDirty = false;
            control.SuilPendingReach = SuilAncestorReach.None;
            return;
        }

        // Might be done already: restyled by an ancestor, or on demand by FlushStyleUpdate.
        if (control.SuilStyleDirty)
            ApplyStyle(control, Stylesheet);

        var reach = control.SuilPendingReach;

        if (reach == SuilAncestorReach.None)
            return;

        control.SuilPendingReach = SuilAncestorReach.None;
        UpdateDependents(control, Stylesheet, reach == SuilAncestorReach.Descendants);
    }

    private static int Depth(Control control)
    {
        var depth = 0;

        for (var parent = control.Parent; parent != null; parent = parent.Parent)
        {
            depth++;
        }

        return depth;
    }
}

/// <summary>
/// Sits in the root of the UI to flush the queued SUIL style updates every frame, in the frame update, before the
/// UI manager measures and arranges the controls the new styles have changed.
/// </summary>
internal sealed class RfStyleFlusher : Control
{
    private readonly Action _flush;

    public RfStyleFlusher(Action flush)
    {
        _flush = flush;
        MouseFilter = MouseFilterMode.Ignore;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        _flush();
    }
}
