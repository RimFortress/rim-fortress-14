using Content.Suil;
using System.Collections;
using System.Linq;
using Content.Client._RF.Suil;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._RF.UserInterface.Controls;

/// <summary>
/// A container that shows one of its pages at a time and a bar of buttons to switch between them, the
/// <see cref="UiElement"/> version of <see cref="Robust.Client.UserInterface.Controls.TabContainer"/>.
/// </summary>
/// <remarks>
/// <para>
/// The structure is in <c>elements/common/tab-container.suil</c>: the container is a column of <see cref="TabBar"/>
/// (<c>.tab-bar</c>, a row of <see cref="RfButton"/>s with the class <c>.tab</c>) and <see cref="Panel"/>
/// (<c>.tab-panel</c>) that holds the pages. As the container is a <see cref="RfBoxContainer"/>, the position of
/// the bar is SUIL: <c>flex-direction: column-reverse</c> puts it below the pages, <c>flex-direction: row</c> and
/// <c>.tab-bar { flex-direction: column }</c> make it a column on the left, <c>gap</c> is the space between the bar
/// and the panel, <c>justify-content</c> of <c>.tab-bar</c> aligns the tabs.
/// </para>
/// <para>
/// In XAML every child of the container is a page, and its tab is set by the attached properties:
/// <code>
/// &lt;controls:RfTabContainer&gt;
///     &lt;MyPage controls:RfTabContainer.TabTitle="Skills"/&gt;
///     &lt;MyPage controls:RfTabContainer.TabTitle="Jobs" controls:RfTabContainer.TabDisabled="True"/&gt;
/// &lt;/controls:RfTabContainer&gt;
/// </code>
/// In code, <see cref="AddTab"/> returns the <see cref="RfTab"/>, which stands for the tab from then on:
/// <code>
/// var tab = tabs.AddTab("Skills", page, SkillsTab.Skills);
/// tabs.OnTabChanged += args => Use((SkillsTab) args.Value!);
/// tabs.SelectValue(SkillsTab.Jobs);
/// </code>
/// Use <see cref="AddTab"/> and not <c>AddChild</c> in code: <c>AddChild</c> adds a child to the container
/// itself, beside the bar and the panel.
/// </para>
/// <para>
/// The selected tab button is <c>:checked</c> (and has the class <c>.selected</c>), a hidden tab has no button, a
/// disabled one is <c>:disabled</c>. The pages are the children of <see cref="Panel"/>, only the selected one is
/// <see cref="Control.Visible"/>. Select the parts with <c>tab > .tab-bar > .tab</c>, the child
/// combinators keep a container inside a page from taking the rules of the outer one.
/// </para>
/// </remarks>
[Virtual, UsedImplicitly]
[SuilTag("tab")]
public partial class RfTabContainer : RfBoxContainer
{
    /// <summary>
    /// The title of the tab of the page. The name of the page is the title if there is none.
    /// </summary>
    public static readonly AttachedProperty<string?> TabTitleProperty
        = AttachedProperty<string?>.CreateNull("TabTitle", typeof(RfTabContainer), changed: OnTabPropertyChanged);

    /// <summary>
    /// Is the tab of the page in the bar. A page of a tab that is not visible can't be selected.
    /// </summary>
    public static readonly AttachedProperty<bool> TabVisibleProperty
        = AttachedProperty<bool>.Create("TabVisible", typeof(RfTabContainer), true, changed: OnTabPropertyChanged);

    /// <summary>
    /// Is the tab of the page disabled: it stays in the bar, but the player can't choose it.
    /// </summary>
    public static readonly AttachedProperty<bool> TabDisabledProperty
        = AttachedProperty<bool>.Create("TabDisabled", typeof(RfTabContainer), changed: OnTabPropertyChanged);

    /// <summary>
    /// The SUIL style classes of the tab button, separated by spaces.
    /// </summary>
    public static readonly AttachedProperty<string?> TabSuilStyleClassesProperty
        = AttachedProperty<string?>.CreateNull("TabSuilStyleClasses", typeof(RfTabContainer), changed: OnTabPropertyChanged);

    // The accessors that XAML looks for to set the attached properties.
    public static string? GetTabTitle(Control page) => page.GetValue(TabTitleProperty);

    public static void SetTabTitle(Control page, string? title) => page.SetValue(TabTitleProperty, title);

    public static bool GetTabVisible(Control page) => page.GetValue(TabVisibleProperty);

    public static void SetTabVisible(Control page, bool visible) => page.SetValue(TabVisibleProperty, visible);

    public static bool GetTabDisabled(Control page) => page.GetValue(TabDisabledProperty);

    public static void SetTabDisabled(Control page, bool disabled) => page.SetValue(TabDisabledProperty, disabled);

    public static string? GetTabSuilStyleClasses(Control page) => page.GetValue(TabSuilStyleClassesProperty);

    public static void SetTabSuilStyleClasses(Control page, string? classes)
        => page.SetValue(TabSuilStyleClassesProperty, classes);

    private readonly List<RfTab> _tabs = new();
    private readonly RfButtonGroup _group = new();

    /// <summary>
    /// The tabs, in the order they are shown.
    /// </summary>
    [PublicAPI, ViewVariables]
    public IReadOnlyList<RfTab> Tabs => _tabs;

    /// <summary>
    /// The chosen tab, <c>null</c> when there are no tabs to choose.
    /// </summary>
    [PublicAPI, ViewVariables]
    public RfTab? Selected { get; private set; }

    /// <summary>
    /// The page of the <see cref="Selected"/> tab.
    /// </summary>
    [PublicAPI, ViewVariables]
    public Control? SelectedPage => Selected?.Page;

    /// <summary>
    /// <see cref="RfTab.Value"/> of the <see cref="Selected"/> tab.
    /// </summary>
    [PublicAPI, ViewVariables]
    public object? SelectedValue => Selected?.Value;

    /// <summary>
    /// The index of the <see cref="Selected"/> tab in <see cref="Tabs"/>, <c>-1</c> if nothing is selected.
    /// Setting is the same as <see cref="Select(RfTab)"/>.
    /// </summary>
    [PublicAPI, ViewVariables]
    public int CurrentTab
    {
        get => Selected == null ? -1 : _tabs.IndexOf(Selected);
        set => Select(_tabs[value]);
    }

    /// <summary>
    /// If false, the bar of the tabs is hidden (the player can still be moved between the pages from code).
    /// </summary>
    [PublicAPI]
    public bool TabBarVisible
    {
        get => TabBar.Visible;
        set => TabBar.Visible = value;
    }

    /// <summary>
    /// SUIL style classes of the buttons of all tabs, the existing and the added afterwards (<c>tab</c> is always
    /// there). Assign a new collection to change them, in XAML: <c>TabSuilStyleClasses="gold"</c>.
    /// The classes of one tab are <see cref="RfTab.SuilStyleClasses"/>.
    /// </summary>
    [PublicAPI]
    public SuilStyleClassCollection TabSuilStyleClasses
    {
        get;
        set
        {
            var old = field;
            field = value;

            foreach (var tab in _tabs)
            {
                foreach (var styleClass in old)
                {
                    tab.Button.RemoveSuilStyleClass(styleClass);
                }

                foreach (var styleClass in value)
                {
                    tab.Button.AddSuilStyleClass(styleClass);
                }
            }
        }
    } = new();

    /// <summary>
    /// Fired when another tab becomes selected, also from code (<see cref="Select(RfTab)"/>, adding the first tab,
    /// hiding the selected one), so that the content of a page can be made when the page is needed.
    /// </summary>
    [PublicAPI]
    public event Action<RfTabChangedEventArgs>? OnTabChanged;

    public RfTabContainer()
    {
        InitializeComponent();

        // What the XAML of the users of the container adds goes to the panel, as pages.
        XamlChildren = new PageCollection(this);
    }

    /// <summary>
    /// Adds a page with its tab to the end of the bar. The first tab added becomes selected.
    /// </summary>
    /// <param name="title">The text of the tab, it is not markup.</param>
    /// <param name="page">What is shown while the tab is selected.</param>
    /// <param name="value">What the tab stands for, see <see cref="RfTab.Value"/>.</param>
    [PublicAPI]
    public RfTab AddTab(string title, Control page, object? value = null)
        => InsertTab(_tabs.Count, title, page, value);

    /// <summary>
    /// Adds a page with its tab at the position.
    /// </summary>
    /// <inheritdoc cref="AddTab"/>
    [PublicAPI]
    public RfTab InsertTab(int index, string title, Control page, object? value = null)
    {
        page.SetValue(TabTitleProperty, title);
        var tab = CreateTab(index, page);
        tab.Value = value;
        return tab;
    }

    /// <summary>
    /// Removes the tab and its page from the container; the page is not disposed and can be added again. If the tab
    /// was selected, the next visible tab is.
    /// </summary>
    [PublicAPI]
    public void RemoveTab(RfTab tab)
    {
        if (tab.Owner != this)
            throw new ArgumentException("The tab belongs to another container.", nameof(tab));

        var index = _tabs.IndexOf(tab);
        _tabs.RemoveAt(index);
        tab.Owner = null;

        tab.Button.Group = null;
        TabBar.RemoveChild(tab.Button);
        Panel.RemoveChild(tab.Page);

        if (Selected == tab)
            SelectNeighbour(index);

        tab.Page.Visible = true;
    }

    /// <summary>
    /// Removes all tabs with their pages.
    /// </summary>
    [PublicAPI]
    public void Clear()
    {
        foreach (var tab in _tabs.ToArray())
        {
            RemoveTab(tab);
        }
    }

    /// <summary>
    /// Moves the tab to the position in the bar. The selection stays.
    /// </summary>
    [PublicAPI]
    public void MoveTab(RfTab tab, int index)
    {
        if (tab.Owner != this)
            throw new ArgumentException("The tab belongs to another container.", nameof(tab));

        _tabs.Remove(tab);
        _tabs.Insert(Math.Clamp(index, 0, _tabs.Count), tab);
        tab.Button.SetPositionInParent(_tabs.IndexOf(tab));
    }

    /// <summary>
    /// Selects the tab and shows its page. Fires <see cref="OnTabChanged"/> if the tab was not selected already.
    /// </summary>
    [PublicAPI]
    public void Select(RfTab tab)
    {
        if (tab.Owner != this)
            throw new ArgumentException("The tab belongs to another container.", nameof(tab));

        if (Selected == tab)
            return;

        var previous = Selected;
        Selected = tab;

        if (previous != null)
        {
            previous.Page.Visible = false;
            previous.Button.RemoveSuilStyleClass(SuilClass.Selected);
        }

        tab.Page.Visible = true;
        tab.Button.Pressed = true;
        tab.Button.AddSuilStyleClass(SuilClass.Selected);

        OnTabChanged?.Invoke(new RfTabChangedEventArgs(this, tab, previous));
    }

    /// <summary>
    /// Selects the tab of the page.
    /// </summary>
    [PublicAPI]
    public void Select(Control page)
    {
        Select(FindByPage(page) ?? throw new ArgumentException("The page is not in the container.", nameof(page)));
    }

    /// <summary>
    /// Selects the first tab whose <see cref="RfTab.Value"/> equals <paramref name="value"/>.
    /// </summary>
    /// <returns>false if there is no such tab.</returns>
    [PublicAPI]
    public bool TrySelectValue(object? value)
    {
        var tab = FindByValue(value);
        if (tab == null)
            return false;

        Select(tab);
        return true;
    }

    /// <summary>
    /// Same as <see cref="TrySelectValue"/>, but throws when there is no such tab.
    /// </summary>
    [PublicAPI]
    public void SelectValue(object? value)
    {
        if (!TrySelectValue(value))
            throw new ArgumentException($"No tab with the value {value}.", nameof(value));
    }

    /// <summary>
    /// The first tab whose <see cref="RfTab.Value"/> equals <paramref name="value"/>.
    /// </summary>
    [PublicAPI]
    public RfTab? FindByValue(object? value)
        => _tabs.Find(tab => Equals(tab.Value, value));

    /// <summary>
    /// The tab the page belongs to.
    /// </summary>
    [PublicAPI]
    public RfTab? FindByPage(Control page)
        => _tabs.Find(tab => tab.Page == page);

    /// <summary>
    /// The tab the page belongs to, whichever container it is in.
    /// </summary>
    [PublicAPI]
    public static RfTab? GetTab(Control page)
        => page.Parent?.Parent is RfTabContainer container ? container.FindByPage(page) : null;

    private RfTab CreateTab(int index, Control page)
    {
        if (page.Parent != null)
            throw new ArgumentException("The page already has a parent.", nameof(page));

        index = Math.Clamp(index, 0, _tabs.Count);

        var button = new RfButton { ToggleMode = true };
        button.AddSuilStyleClass(SuilClass.Tab);
        foreach (var styleClass in TabSuilStyleClasses)
        {
            button.AddSuilStyleClass(styleClass);
        }

        var tab = new RfTab(this, page, button);
        button.OnPressed += _ => ButtonPressed(tab);

        _tabs.Insert(index, tab);
        page.Visible = false;
        Panel.AddChild(page);
        TabBar.AddChild(button);
        button.SetPositionInParent(index);
        button.Group = _group;

        tab.Sync();

        // The first tab, or the one that was selected, is the one that shows.
        if (Selected == null && tab.Visible)
            Select(tab);

        return tab;
    }

    private void ButtonPressed(RfTab tab)
    {
        Select(tab);
    }

    // The selected tab went away or got hidden: the next visible one, then the previous.
    private void SelectNeighbour(int index)
    {
        RfTab? next = null;

        for (var i = 0; i < _tabs.Count && next == null; i++)
        {
            var candidate = _tabs[(index + i) % _tabs.Count];
            if (candidate.Visible && candidate != Selected)
                next = candidate;
        }

        if (next != null)
        {
            Select(next);
            return;
        }

        var previous = Selected;
        Selected = null;
        if (previous is { Owner: not null })
        {
            previous.Page.Visible = false;
            previous.Button.RemoveSuilStyleClass(SuilClass.Selected);
        }

        if (previous != null)
            OnTabChanged?.Invoke(new RfTabChangedEventArgs(this, null, previous));
    }

    internal void TabVisibilityChanged(RfTab tab)
    {
        if (Selected == tab && !tab.Visible)
            SelectNeighbour(_tabs.IndexOf(tab));
        else if (Selected == null && tab.Visible)
            Select(tab);
    }

    internal int IndexOf(RfTab tab) => _tabs.IndexOf(tab);

    private static void OnTabPropertyChanged<T>(Control page, AttachedPropertyChangedEventArgs<T> args)
    {
        GetTab(page)?.Sync();
    }

    // The XAML of a user of the container adds its children here.
    private sealed class PageCollection(RfTabContainer owner) : ICollection<Control>
    {
        public int Count => owner._tabs.Count;

        public bool IsReadOnly => false;

        public void Add(Control item) => owner.CreateTab(owner._tabs.Count, item);

        public bool Remove(Control item)
        {
            var tab = owner.FindByPage(item);
            if (tab == null)
                return false;

            owner.RemoveTab(tab);
            return true;
        }

        public void Clear() => owner.Clear();

        public bool Contains(Control item) => owner.FindByPage(item) != null;

        public void CopyTo(Control[] array, int arrayIndex)
        {
            foreach (var tab in owner._tabs)
            {
                array[arrayIndex++] = tab.Page;
            }
        }

        public IEnumerator<Control> GetEnumerator() => owner._tabs.Select(tab => tab.Page).ToList().GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}

/// <summary>
/// A tab of a <see cref="RfTabContainer"/>: the button in the bar and the page it shows.
/// </summary>
public sealed class RfTab
{
    internal RfTabContainer? Owner;

    private bool _syncing;

    /// <summary>
    /// What is shown while the tab is selected.
    /// </summary>
    public Control Page { get; }

    /// <summary>
    /// The button of the tab in the bar, for what <see cref="SuilStyleClasses"/> and the like don't cover.
    /// </summary>
    public RfButton Button { get; }

    internal RfTab(RfTabContainer owner, Control page, RfButton button)
    {
        Owner = owner;
        Page = page;
        Button = button;
    }

    /// <summary>
    /// The container this tab belongs to, <c>null</c> after it was removed.
    /// </summary>
    [PublicAPI]
    public RfTabContainer? Parent => Owner;

    /// <summary>
    /// What the tab stands for. Not shown to the player.
    /// </summary>
    [PublicAPI]
    public object? Value { get; set; }

    /// <summary>
    /// The text of the tab, it is not markup. Without <see cref="RfTabContainer.TabTitleProperty"/> it is the name
    /// of the page.
    /// </summary>
    [PublicAPI]
    public string Title
    {
        get => Page.GetValue(RfTabContainer.TabTitleProperty)
               ?? Page.Name
               ?? Loc.GetString("tab-container-not-tab-title-provided");
        set => Page.SetValue(RfTabContainer.TabTitleProperty, value);
    }

    /// <summary>
    /// Is the tab in the bar. Hiding the selected tab selects another one.
    /// </summary>
    [PublicAPI]
    public bool Visible
    {
        get => Page.GetValue(RfTabContainer.TabVisibleProperty);
        set => Page.SetValue(RfTabContainer.TabVisibleProperty, value);
    }

    /// <summary>
    /// A disabled tab can't be chosen by the player.
    /// </summary>
    [PublicAPI]
    public bool Disabled
    {
        get => Page.GetValue(RfTabContainer.TabDisabledProperty);
        set => Page.SetValue(RfTabContainer.TabDisabledProperty, value);
    }

    [PublicAPI]
    public bool IsSelected => Owner?.Selected == this;

    /// <summary>
    /// The place of the tab in <see cref="RfTabContainer.Tabs"/>, <c>-1</c> after it was removed.
    /// </summary>
    [PublicAPI]
    public int Index => Owner?.IndexOf(this) ?? -1;

    /// <summary>
    /// SUIL style classes of the button of the tab, in addition to <see cref="RfTabContainer.TabSuilStyleClasses"/>.
    /// </summary>
    [PublicAPI]
    public SuilStyleClassCollection SuilStyleClasses => Button.SuilStyleClasses;

    /// <summary>
    /// SUIL style identifier (<c>#id</c>) of the button of the tab.
    /// </summary>
    [PublicAPI]
    public string? SuilStyleIdentifier
    {
        get => Button.SuilStyleIdentifier;
        set => Button.SuilStyleIdentifier = value;
    }

    /// <summary>
    /// <see cref="Value"/> as <typeparamref name="T"/>, <c>default</c> if it is of another type.
    /// </summary>
    [PublicAPI]
    public T? ValueAs<T>() => Value is T value ? value : default;

    /// <summary>
    /// Brings the button to the state of the attached properties of the page.
    /// </summary>
    internal void Sync()
    {
        if (Owner == null || _syncing)
            return;

        _syncing = true;
        try
        {
            Button.SetPlainText(Title);
            Button.Disabled = Disabled;

            var wasVisible = Button.Visible;
            Button.Visible = Visible;

            var classes = Page.GetValue(RfTabContainer.TabSuilStyleClassesProperty);
            if (classes != _appliedClasses)
            {
                foreach (var styleClass in _appliedClasses?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? [])
                {
                    Button.RemoveSuilStyleClass(styleClass);
                }

                foreach (var styleClass in classes?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? [])
                {
                    Button.AddSuilStyleClass(styleClass);
                }

                _appliedClasses = classes;
            }

            if (wasVisible != Button.Visible)
                Owner.TabVisibilityChanged(this);
        }
        finally
        {
            _syncing = false;
        }
    }

    private string? _appliedClasses;
}

/// <summary>
/// Fired when another tab of a <see cref="RfTabContainer"/> becomes selected.
/// </summary>
public sealed class RfTabChangedEventArgs(RfTabContainer container, RfTab? tab, RfTab? previous) : EventArgs
{
    public RfTabContainer Container { get; } = container;

    /// <summary>
    /// The tab that is selected now, <c>null</c> if there is none left to select.
    /// </summary>
    public RfTab? Tab { get; } = tab;

    /// <summary>
    /// The tab that was selected, <c>null</c> if there was none.
    /// </summary>
    public RfTab? Previous { get; } = previous;

    /// <summary>
    /// <see cref="RfTab.Value"/> of the <see cref="Tab"/>.
    /// </summary>
    public object? Value => Tab?.Value;

    /// <summary>
    /// The page of the <see cref="Tab"/>.
    /// </summary>
    public Control? Page => Tab?.Page;
}
