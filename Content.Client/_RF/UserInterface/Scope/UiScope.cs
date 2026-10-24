namespace Content.Client._RF.UserInterface.Scope;

/// <summary>
/// What a control does only while it is in the tree of the controls: it listens to events, updates itself at an
/// interval, redraws once for a few changes. The scope subscribes when the control enters the tree and unsubscribes
/// when it leaves, so the control needs no <c>EnteredTree</c> / <c>ExitedTree</c> / <c>FrameUpdate</c> of its own.
/// </summary>
/// <remarks>
/// <para>
/// Get it from <c>Scope</c> of <see cref="Content.Client._RF.Suil.UiElement"/> or of
/// <see cref="Controls.RfWindow"/>; those pass the hooks of the tree to it. Everything is registered once, usually in the
/// constructor:
/// </para>
/// <code>
/// var refresh = Scope.Deferred(Rebuild);
///
/// Scope.Listen&lt;Entity&lt;ZoneComponent&gt;&gt;(
///     h =&gt; _zoning.OnZoneUpdated += h,
///     h =&gt; _zoning.OnZoneUpdated -= h,
///     _ =&gt; refresh());
///
/// Scope.Every(1f, UpdateInfo, runOnEnter: true);
/// </code>
/// <para>
/// The scope knows nothing about controls, so it can be tested by calling <see cref="Enter"/>, <see cref="Update"/> and
/// <see cref="Exit"/>.
/// </para>
/// </remarks>
public sealed class UiScope
{
    private readonly List<Listener> _listeners = new();
    private readonly List<Interval> _intervals = new();
    private readonly List<FrameCallback> _frames = new();
    private readonly List<DeferredCall> _deferred = new();

    /// <summary>
    /// Creates the scope of a control that is already in the tree when <paramref name="active"/>.
    /// </summary>
    public UiScope(bool active = false)
    {
        Active = active;
    }

    /// <summary>
    /// Whether the control is in the tree: the listeners are subscribed.
    /// </summary>
    public bool Active { get; private set; }

    // ---- Registration ----

    /// <summary>
    /// Subscribes while the control is in the tree: right away if it is there already, otherwise when it enters.
    /// </summary>
    /// <param name="subscribe">Adds the handler to an event of a system, a controller or a control.</param>
    /// <param name="unsubscribe">Removes it, exactly the opposite of <paramref name="subscribe"/>.</param>
    public void Listen(Action subscribe, Action unsubscribe)
    {
        var listener = new Listener(subscribe, unsubscribe);
        _listeners.Add(listener);

        if (Active)
            listener.Attach();
    }

    /// <summary>
    /// Subscribes the handler to an event while the control is in the tree. The same delegate is passed to both lambdas,
    /// so it can be removed:
    /// <code>Scope.Listen&lt;int&gt;(h =&gt; source.OnChanged += h, h =&gt; source.OnChanged -= h, OnChanged);</code>
    /// </summary>
    public void Listen<T>(Action<Action<T>> add, Action<Action<T>> remove, Action<T> handler)
    {
        Listen(() => add(handler), () => remove(handler));
    }

    /// <inheritdoc cref="Listen{T}(Action{Action{T}}, Action{Action{T}}, Action{T})"/>
    public void Listen(Action<Action> add, Action<Action> remove, Action handler)
    {
        Listen(() => add(handler), () => remove(handler));
    }

    /// <summary>
    /// Runs the action every time the control enters the tree, and right away if it is there already: the first fill of
    /// what <see cref="Listen(Action, Action)"/> keeps up to date afterwards.
    /// </summary>
    public void Entered(Action action)
    {
        Listen(action, static () => { });
    }

    /// <summary>
    /// Subscribes the handler to an event of a source that is found when the control enters the tree, not when it is
    /// made: a system may not exist yet by then. The source found is kept, so the handler is removed from it even if it is
    /// gone from the game when the control leaves the tree.
    /// <code>Scope.Listen(GetSystem, (Sys s, Action&lt;int&gt; h) =&gt; s.OnChanged += h, (s, h) =&gt; s.OnChanged -= h, OnChanged);</code>
    /// </summary>
    public void Listen<TSource, T>(
        Func<TSource> resolve,
        Action<TSource, Action<T>> add,
        Action<TSource, Action<T>> remove,
        Action<T> handler)
    {
        TSource? source = default;

        Listen(() =>
        {
            source = resolve();
            add(source, handler);
        },
        () =>
        {
            remove(source!, handler);
            source = default;
        });
    }

    /// <inheritdoc cref="Listen{TSource, T}(Func{TSource}, Action{TSource, Action{T}}, Action{TSource, Action{T}}, Action{T})"/>
    public void Listen<TSource>(
        Func<TSource> resolve,
        Action<TSource, Action> add,
        Action<TSource, Action> remove,
        Action handler)
    {
        TSource? source = default;

        Listen(() =>
        {
            source = resolve();
            add(source, handler);
        },
        () =>
        {
            remove(source!, handler);
            source = default;
        });
    }

    /// <summary>
    /// Calls the action at an interval while the control is in the tree. The time is counted from the moment the
    /// control enters it.
    /// </summary>
    /// <param name="seconds">The interval.</param>
    /// <param name="action">What to do.</param>
    /// <param name="whenVisible">
    /// Fire only while the control is visible in the tree. The time of a hidden control is counted, so it fires on the
    /// first visible frame if the interval has passed.
    /// </param>
    /// <param name="runOnEnter">Fire on the first frame after the control enters the tree, not after the interval.</param>
    public void Every(float seconds, Action action, bool whenVisible = true, bool runOnEnter = false)
    {
        if (seconds <= 0f)
            throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "the interval must be positive");

        var interval = new Interval(new UiTimer(seconds, runOnEnter), action, whenVisible, runOnEnter);
        _intervals.Add(interval);
    }

    /// <summary>
    /// Calls the action every frame while the control is in the tree, with the time of the frame in seconds.
    /// </summary>
    /// <param name="action">What to do.</param>
    /// <param name="whenVisible">Call only while the control is visible in the tree.</param>
    public void EveryFrame(Action<float> action, bool whenVisible = true)
    {
        _frames.Add(new FrameCallback(action, whenVisible));
    }

    /// <summary>
    /// Makes an action that runs on the next frame, once, however many times it is called before: a few events in a row
    /// rebuild the control once. While the control is out of the tree or hidden, the call waits and runs on the first
    /// frame it is in the tree and visible.
    /// </summary>
    /// <param name="action">What to do.</param>
    /// <param name="whenVisible">Wait for the control to be visible, not only to be in the tree.</param>
    /// <returns>The action that asks for the run.</returns>
    public Action Deferred(Action action, bool whenVisible = true)
    {
        var call = new DeferredCall(action, whenVisible);
        _deferred.Add(call);
        return call.Request;
    }

    // ---- Hooks of the control ----

    /// <summary>
    /// The control entered the tree: subscribes the listeners and starts the intervals.
    /// </summary>
    public void Enter()
    {
        if (Active)
            return;

        Active = true;

        foreach (var listener in _listeners)
        {
            listener.Attach();
        }

        foreach (var interval in _intervals)
        {
            interval.Start();
        }
    }

    /// <summary>
    /// The control left the tree: unsubscribes the listeners, the order backwards.
    /// </summary>
    public void Exit()
    {
        if (!Active)
            return;

        Active = false;

        for (var i = _listeners.Count - 1; i >= 0; i--)
        {
            _listeners[i].Detach();
        }
    }

    /// <summary>
    /// A frame of the control. Does nothing while the scope is not active.
    /// </summary>
    /// <param name="deltaSeconds">The time of the frame.</param>
    /// <param name="visible">Whether the control is visible in the tree.</param>
    public void Update(float deltaSeconds, bool visible)
    {
        if (!Active)
            return;

        // Registrations made by a callback wait for the next frame: the counts are taken before.
        var intervals = _intervals.Count;

        for (var i = 0; i < intervals; i++)
        {
            _intervals[i].Update(deltaSeconds, visible);
        }

        var frames = _frames.Count;

        for (var i = 0; i < frames; i++)
        {
            _frames[i].Update(deltaSeconds, visible);
        }

        var deferred = _deferred.Count;

        for (var i = 0; i < deferred; i++)
        {
            _deferred[i].Update(visible);
        }
    }

    // ---- Parts ----

    private sealed class Listener(Action subscribe, Action unsubscribe)
    {
        private bool _attached;

        public void Attach()
        {
            if (_attached)
                return;

            _attached = true;
            subscribe();
        }

        public void Detach()
        {
            if (!_attached)
                return;

            _attached = false;
            unsubscribe();
        }
    }

    private sealed class Interval(UiTimer timer, Action action, bool whenVisible, bool runOnEnter)
    {
        private UiTimer _timer = timer;

        public void Start()
        {
            if (runOnEnter)
                _timer.MakeDue();
            else
                _timer.Reset();
        }

        public void Update(float deltaSeconds, bool visible)
        {
            if (_timer.Tick(deltaSeconds, visible || !whenVisible))
                action();
        }
    }

    private sealed class FrameCallback(Action<float> action, bool whenVisible)
    {
        public void Update(float deltaSeconds, bool visible)
        {
            if (visible || !whenVisible)
                action(deltaSeconds);
        }
    }

    private sealed class DeferredCall(Action action, bool whenVisible)
    {
        private bool _requested;

        public void Request()
        {
            _requested = true;
        }

        public void Update(bool visible)
        {
            if (!_requested || (whenVisible && !visible))
                return;

            // Cleared first: the action may ask for itself again, and that run is the next frame's.
            _requested = false;
            action();
        }
    }
}
