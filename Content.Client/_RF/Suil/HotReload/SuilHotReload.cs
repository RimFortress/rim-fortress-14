using System.Linq;
using Content.Suil;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._RF.Suil.HotReload;

/// <inheritdoc cref="ISuilHotReload"/>
public sealed partial class SuilHotReload : ISuilHotReload
{
    [Dependency] private ISuilManager _suil = null!;
    [Dependency] private IUserInterfaceManager _ui = null!;
    [Dependency] private IDynamicTypeFactory _factory = null!;
    [Dependency] private IClyde _clyde = null!;
    [Dependency] private IInputManager _input = null!;

    private ISawmill _sawmill = null!;
    private SuilControlPicker? _picker;
    private bool _checking;

    public void Initialize()
    {
        _sawmill = Logger.GetSawmill("suil.hotreload");

#if TOOLS
        // The sandbox has no file watcher, so the files are compared when the window is back: it is where the editor was.
        _clyde.OnWindowFocused += OnWindowFocused;
#endif
    }

    private void OnWindowFocused(WindowFocusedEventArgs args)
    {
        if (args.Focused)
            ReloadChanged();
    }

    /// <inheritdoc/>
    public SuilReloadResult Reload(ISuilView view)
    {
        var source = _suil.Files;
        return Reload(view, source, source.LoadGlobals());
    }

    /// <inheritdoc/>
    public SuilReloadResult Reload(ISuilView view, ISuilFileSource source, SuilGlobals globals)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        var dependencies = new HashSet<string>(StringComparer.Ordinal);

        if (view is not Control control)
        {
            errors.Add("the view is not a control");
            return new SuilReloadResult(view, errors, warnings, dependencies);
        }

        var path = view.SuilPath + SuilPaths.Extension;
        var recording = new SuilRecordingSource(source);
        SuilFile expanded;

        try
        {
            expanded = SuilExpander.Expand(recording.Load(path), recording, globals.Files.Contains(path) ? null : globals.Paths);
        }
        catch (Exception e) when (e is SuilParserError or SuilTokenizeError or SuilExpansionError or System.IO.FileNotFoundException)
        {
            errors.Add(e.Message);
            return new SuilReloadResult(view, errors, warnings, recording.Loaded);
        }

        foreach (var file in recording.Loaded)
        {
            dependencies.Add(file);
        }

        if (expanded.Root == null)
        {
            errors.Add("the file has no root element");
            return new SuilReloadResult(view, errors, warnings, dependencies);
        }

        var entry = SuilViewRegistry.Find(view);
        var state = entry?.State as ViewState ?? new ViewState();

        // The whole tree is checked before a control is touched: a mistake in the file leaves the UI as it was.
        var plan = BuildPlan(view, control, expanded.Root, errors, warnings);

        if (plan == null || errors.Count > 0)
        {
            return new SuilReloadResult(view, errors, warnings, dependencies);
        }

        try
        {
            Apply(view, control, plan, state, errors, warnings);
        }
        catch (Exception e)
        {
            errors.Add($"the tree could not be built: {e}");
        }

        state.Plan = plan;
        state.Dependencies = dependencies;

        if (entry != null)
            entry.State = state;

        if (view is ISuilReloadHandler handler)
            handler.OnSuilReloaded();

        return new SuilReloadResult(view, errors, warnings, dependencies);
    }

    /// <summary>
    /// Writes what a reload found to the log. The reload itself doesn't: its result is for whoever asked.
    /// </summary>
    private SuilReloadResult Logged(SuilReloadResult result)
    {
        foreach (var error in result.Errors)
        {
            _sawmill.Error($"{result.View.SuilPath}: {error}");
        }

        foreach (var warning in result.Warnings)
        {
            _sawmill.Warning($"{result.View.SuilPath}: {warning}");
        }

        return result;
    }

    /// <inheritdoc/>
    public IReadOnlyList<SuilReloadResult> ReloadPath(string path)
    {
        var wanted = NormalizePath(path);
        return ReloadViews(view => NormalizePath(view.SuilPath) == wanted);
    }

    /// <inheritdoc/>
    public IReadOnlyList<SuilReloadResult> ReloadAll() => ReloadViews(_ => true);

    private IReadOnlyList<SuilReloadResult> ReloadViews(Func<ISuilView, bool> filter)
    {
        // The styles are files of the same folder: what a view imports may be a style as well.
        _suil.Reload();

        var source = _suil.Files;
        var globals = source.LoadGlobals();
        var results = new List<SuilReloadResult>();

        foreach (var view in AttachedViews())
        {
            if (filter(view))
                results.Add(Reload(view, source, globals));
        }

        return results;
    }

    /// <inheritdoc/>
    public SuilFileChanges ReloadChanged()
    {
        if (_checking)
            return new SuilFileChanges(Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());

        _checking = true;

        try
        {
            var changes = _suil.Files.FindChanges();

            if (!changes.Any)
                return changes;

            _sawmill.Info($"files changed: {string.Join(", ", changes.Changed.Concat(changes.Removed).Concat(changes.Added))}");

            var touched = new HashSet<string>(changes.Changed.Concat(changes.Removed), StringComparer.Ordinal);
            var directories = new HashSet<string>(changes.Added.Select(SuilPaths.ParentOf), StringComparer.Ordinal);

            // Every file is a style file too, so the styles are always loaded again; a view only when its files changed.
            _suil.Reload();

            var source = _suil.Files;
            var globals = source.LoadGlobals();

            foreach (var view in AttachedViews())
            {
                var entry = SuilViewRegistry.Find(view);
                var dependencies = (entry?.State as ViewState)?.Dependencies;

                // A view that was never reloaded doesn't know its files: the file of its own is all that is known.
                var affected = dependencies == null
                    ? touched.Contains(view.SuilPath + SuilPaths.Extension)
                    : dependencies.Overlaps(touched) || dependencies.Any(d => directories.Contains(SuilPaths.ParentOf(d)));

                if (affected)
                    Logged(Reload(view, source, globals));
            }

            return changes;
        }
        finally
        {
            _checking = false;
        }
    }

    /// <inheritdoc/>
    public SuilReloadResult? ReloadViewOf(Control control)
    {
        for (var current = control; current != null; current = current.Parent)
        {
            if (current is ISuilView view)
                return Reload(view);
        }

        return null;
    }

    /// <inheritdoc/>
    public bool IsPicking => _picker is { IsActive: true };

    /// <inheritdoc/>
    public void StartPicking()
    {
        if (IsPicking)
            return;

        _picker = new SuilControlPicker(_ui, _input, control =>
        {
            _picker = null;

            if (control == null)
                return;

            var result = ReloadViewOf(control);

            if (result == null)
                _sawmill.Warning($"{control.GetType().Name} is not in a SUIL view");
            else if (Logged(result).Success)
                _sawmill.Info($"reloaded {result.View.SuilPath}");
        });
    }

    /// <inheritdoc/>
    public void StopPicking()
    {
        _picker?.Cancel();
        _picker = null;
    }

    /// <summary>
    /// The views that are in the UI of this client: the registry is shared by every client of the process.
    /// </summary>
    private List<ISuilView> AttachedViews()
    {
        var roots = new HashSet<UIRoot>(_ui.AllRoots);
        var views = new List<ISuilView>();

        foreach (var entry in SuilViewRegistry.Snapshot())
        {
            if (entry.TryGetView(out var view) && view is Control { Root: { } root } && roots.Contains(root))
                views.Add(view);
        }

        return views;
    }

    private static string NormalizePath(string path)
    {
        if (!path.StartsWith("/", StringComparison.Ordinal))
            path = "/" + path;

        return path.EndsWith(SuilPaths.Extension, StringComparison.Ordinal)
            ? path.Substring(0, path.Length - SuilPaths.Extension.Length)
            : path;
    }

    /// <summary>
    /// What the reload keeps for a view between two reloads.
    /// </summary>
    private sealed class ViewState
    {
        /// <summary>The tree that was applied last time, to find what the file doesn't have any more.</summary>
        public PlannedElement? Plan;

        public HashSet<string>? Dependencies;

        /// <summary>The values the controls had before the file set the properties, by id ("" is the root).</summary>
        public readonly Dictionary<string, Dictionary<string, object?>> Originals = new(StringComparer.Ordinal);
    }
}
