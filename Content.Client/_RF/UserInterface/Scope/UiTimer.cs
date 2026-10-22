namespace Content.Client._RF.UserInterface.Scope;

/// <summary>
/// Counts frame time and says when an interval has passed: the routine <c>_nextUpdate</c> field of a control or a
/// controller. It fires at most once per <see cref="Tick(float)"/>, so a long frame is not made up for with several updates.
/// </summary>
/// <remarks>
/// A control should use <see cref="UiScope.Every"/> instead, which also stops while the control is out of the tree. The
/// timer is for the places that have no scope, like a UI controller.
/// </remarks>
public struct UiTimer(float interval, bool due = false)
{
    private float _elapsed = due ? interval : 0f;

    /// <summary>
    /// The interval in seconds.
    /// </summary>
    public float Interval { get; set; } = interval;

    /// <summary>
    /// Whether the interval has passed already: the next <see cref="Tick(float)"/> fires.
    /// </summary>
    public readonly bool IsDue => _elapsed >= Interval;

    /// <summary>
    /// Adds the time of the frame and returns <c>true</c> if the interval has passed, starting it over.
    /// </summary>
    public bool Tick(float deltaSeconds)
    {
        _elapsed += deltaSeconds;
        return Fire();
    }

    /// <summary>
    /// Adds the time of the frame, but fires only if <paramref name="ready"/> (a hidden control, say). The time is
    /// counted anyway, so the first frame the timer is ready for fires if the interval has passed.
    /// </summary>
    public bool Tick(float deltaSeconds, bool ready)
    {
        _elapsed += deltaSeconds;
        return ready && Fire();
    }

    /// <summary>
    /// Makes the next tick fire.
    /// </summary>
    public void MakeDue()
    {
        _elapsed = Interval;
    }

    /// <summary>
    /// Starts the interval over.
    /// </summary>
    public void Reset()
    {
        _elapsed = 0f;
    }

    private bool Fire()
    {
        if (_elapsed < Interval)
            return false;

        _elapsed = 0f;
        return true;
    }
}
