namespace Content.Suil;

/// <summary>
/// A control built from a <c>@Class =&gt; element</c> declaration of a <c>.suil</c> file. The generator implements it, so the hot reload can
/// build the tree again and bind the fields of the class without reflection, which the sandbox doesn't allow.
/// </summary>
/// <remarks>
/// Fields are named by the id of the element as it is written in the file (<c>submit-button</c>), not by the name of the
/// C# member (<c>SubmitButton</c>).
/// </remarks>
public interface ISuilView
{
    /// <summary>
    /// The path of the file with the declaration of the class, without the extension, like <c>/Lobby/Screen</c>.
    /// </summary>
    string SuilPath { get; }

    /// <summary>
    /// The name of the class exactly as the declaration writes it (<c>Game.Ui.Picker</c>, or just <c>Picker</c>): a file may
    /// declare several views, and the hot reload finds the one of this control by it.
    /// </summary>
    string SuilViewName { get; }

    /// <summary>
    /// Where the children of the root went when the tree was built: its <c>XamlChildren</c> at that moment, as XAML would
    /// have it (a window keeps them in its contents, not among its own parts). The hot reload builds the children again
    /// in the same place. <c>null</c> for a view that was not built by <c>InitializeComponent()</c>: the children of the
    /// control.
    /// </summary>
    object? SuilRootChildren { get; }

    /// <summary>
    /// Where the children given to the class go (its <c>XamlChildren</c>): the one the <c>%content%</c> of the file
    /// marks, <c>null</c> if the file has none.
    /// </summary>
    object? SuilContent { get; }

    /// <summary>
    /// What was in <see cref="SuilContent"/> when the tree was built: the children of the file. The rest of it was given
    /// to the class afterwards, and the hot reload keeps it when it builds the tree again.
    /// </summary>
    object? SuilContentBuilt { get; }

    /// <summary>
    /// Makes the collection the <c>XamlChildren</c> of the class, <c>null</c> for the children of the control.
    /// </summary>
    void SuilSetContent(object? collection);

    /// <summary>
    /// The ids of the elements that have a field in the class.
    /// </summary>
    IReadOnlyList<string> SuilFieldIds { get; }

    /// <summary>
    /// The type of the field of the id, or <c>null</c> if the class has no such field.
    /// </summary>
    Type? SuilFieldType(string id);

    /// <summary>
    /// The control in the field of the id.
    /// </summary>
    object? SuilGetField(string id);

    /// <summary>
    /// Puts a control in the field of the id. Returns <c>false</c> if there is no such field or the control is of
    /// another type.
    /// </summary>
    bool SuilSetField(string id, object? value);

    /// <summary>
    /// Finds a method of the class (or of its base classes) that a call of the file can make, <c>@OnSubmit(args)</c>:
    /// by its name and the number of the arguments.
    /// </summary>
    /// <param name="name">The name of the method, as in the code.</param>
    /// <param name="arity">How many arguments the call has.</param>
    /// <param name="parameters">The types of its parameters.</param>
    bool SuilTryGetMethod(string name, int arity, out Type[]? parameters);

    /// <summary>
    /// Calls the method found by <see cref="SuilTryGetMethod"/>. The arguments are of the types of the parameters. Returns
    /// what the method returns, <c>null</c> for a method that returns nothing.
    /// </summary>
    object? SuilInvoke(string name, object?[] arguments);
}

/// <summary>
/// A view that wants to know when its tree was built again by the hot reload: the controls that the code of the class
/// has not got in fields (and the handlers of the ones it has, if they were replaced) are new.
/// </summary>
public interface ISuilReloadHandler
{
    void OnSuilReloaded();
}

/// <summary>
/// A view that is alive: the weak reference to it and what the hot reload remembers about it.
/// </summary>
public sealed class SuilViewEntry
{
    private readonly WeakReference<ISuilView> _view;

    private IReadOnlyList<Action>? _unsubscribers;

    internal SuilViewEntry(ISuilView view, IReadOnlyList<Action>? unsubscribers)
    {
        _view = new WeakReference<ISuilView>(view);
        _unsubscribers = unsubscribers;
    }

    /// <summary>
    /// Takes the actions that unsubscribe the handlers that the generated code of the view subscribed from the file as
    /// it was compiled. The hot reload subscribes the ones of the new file, so the old ones must go; they are given
    /// once.
    /// </summary>
    public IReadOnlyList<Action> TakeUnsubscribers()
    {
        var result = _unsubscribers ?? Array.Empty<Action>();
        _unsubscribers = null;
        return result;
    }

    /// <summary>
    /// The view, unless it has been collected.
    /// </summary>
    public bool TryGetView(out ISuilView view)
    {
        if (_view.TryGetTarget(out var target))
        {
            view = target;
            return true;
        }

        view = null!;
        return false;
    }

    /// <summary>
    /// Whatever the hot reload keeps for the view between the reloads. The entry lives as long as the view does.
    /// </summary>
    public object? State { get; set; }
}

/// <summary>
/// The views that are alive. Every generated <c>InitializeComponent()</c> adds its control here; the references are weak,
/// so a view that nothing holds goes away.
/// </summary>
public static class SuilViewRegistry
{
    private static readonly object Lock = new();
    private static readonly List<SuilViewEntry> Entries = new();

    /// <param name="view">The view.</param>
    /// <param name="unsubscribers">What takes the handlers of the generated code off the events, for the hot reload.</param>
    public static void Register(ISuilView view, IReadOnlyList<Action>? unsubscribers = null)
    {
        lock (Lock)
        {
            // A long session creates many views, so the dead ones are dropped now and then.
            if (Entries.Count >= 256 && Entries.Count % 256 == 0)
                Entries.RemoveAll(entry => !entry.TryGetView(out _));

            Entries.Add(new SuilViewEntry(view, unsubscribers));
        }
    }

    /// <summary>
    /// The views that are alive now.
    /// </summary>
    public static List<SuilViewEntry> Snapshot()
    {
        lock (Lock)
        {
            Entries.RemoveAll(entry => !entry.TryGetView(out _));
            return new List<SuilViewEntry>(Entries);
        }
    }

    /// <summary>
    /// The entry of the view, <c>null</c> if the view was not built by a generated <c>InitializeComponent()</c>.
    /// </summary>
    public static SuilViewEntry? Find(ISuilView view)
    {
        lock (Lock)
        {
            foreach (var entry in Entries)
            {
                if (entry.TryGetView(out var other) && ReferenceEquals(other, view))
                    return entry;
            }

            return null;
        }
    }
}
