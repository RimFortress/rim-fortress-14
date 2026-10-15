using Content.Suil;

namespace Content.Client._RF.Suil.HotReload;

/// <summary>
/// What a reload of a view did.
/// </summary>
/// <param name="View">The view that was reloaded.</param>
/// <param name="Errors">Why the tree was not rebuilt; the old tree is untouched then.</param>
/// <param name="Warnings">What the reload could do only partly, like an id that has no field in the class.</param>
/// <param name="Dependencies">The files the view is built from: its own file and everything it imports.</param>
public sealed record SuilReloadResult(
    ISuilView View,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings,
    IReadOnlySet<string> Dependencies)
{
    public bool Success => Errors.Count == 0;
}

/// <summary>
/// The hot reload of the views: the tree of a <c>[SuilView]</c> is built again from its <c>.suil</c> file while the game
/// runs. XAML does this by compiling the file into a new method, which needs the JIT and reflection; the sandbox allows
/// neither. Here the file is expanded again and its tree is applied to the live controls by the property tables and the
/// field setters that the generator wrote, so the code of the views never changes.
/// </summary>
/// <remarks>
/// <para>
/// What can change: the structure, the classes, the ids and the properties of the elements, and everything the file
/// imports. What can't: the C# code, so an id that the class has no field for stays without one until the project is
/// built (the reload says so), and so does the type of the root.
/// </para>
/// <para>
/// A control that has a field is kept and applied to again, so the handlers that the code subscribed to it still work.
/// The other controls are made new.
/// </para>
/// </remarks>
public interface ISuilHotReload
{
    void Initialize();

    /// <summary>
    /// Builds the tree of the view again from the current files.
    /// </summary>
    SuilReloadResult Reload(ISuilView view);

    /// <summary>
    /// Builds the tree again from the given source of files, the globals of which are the given.
    /// </summary>
    SuilReloadResult Reload(ISuilView view, ISuilFileSource source, SuilGlobals globals);

    /// <summary>
    /// Reloads the styles and then every view in the UI that is built from the file.
    /// </summary>
    /// <param name="path">The path as written in <c>[SuilView]</c>, with or without the extension.</param>
    /// <returns>The results of the views.</returns>
    IReadOnlyList<SuilReloadResult> ReloadPath(string path);

    /// <summary>
    /// Reloads the styles and every view in the UI.
    /// </summary>
    IReadOnlyList<SuilReloadResult> ReloadAll();

    /// <summary>
    /// Finds the files that were edited since they were read, and reloads the styles and the views that are built from them.
    /// This is what happens when the window of the game gets the focus.
    /// </summary>
    SuilFileChanges ReloadChanged();

    /// <summary>
    /// Waits for a click on a control and reloads the view it belongs to.
    /// </summary>
    void StartPicking();

    /// <summary>
    /// Whether <see cref="StartPicking"/> waits for a click now.
    /// </summary>
    bool IsPicking { get; }

    /// <summary>
    /// Stops waiting for the click, if it was waited for.
    /// </summary>
    void StopPicking();

    /// <summary>
    /// Reloads the view the control belongs to: the nearest one, itself or its ancestor.
    /// </summary>
    /// <returns><c>null</c> if the control is not in any view.</returns>
    SuilReloadResult? ReloadViewOf(Robust.Client.UserInterface.Control control);
}
