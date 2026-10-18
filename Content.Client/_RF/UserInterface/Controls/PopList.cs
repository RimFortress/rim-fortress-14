using Content.Client._RF.NPC.Executable.Systems;
using Content.Client._RF.Selection;
using Content.Client._RF.World;
using Content.Client.CombatMode;
using Content.Shared._RF.World.Components;
using Content.Shared.CombatMode;
using Robust.Client.Player;
using Content.Client._RF.Suil;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client._RF.UserInterface.Controls;

public sealed partial class PopList : UIWidget
{
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IResourceCache _resources = default!;

    private const string CombatModeIcon = "/Textures/Interface/Actions/harm.png";
    private const string NoCombatModeIcon = "/Textures/Interface/Actions/harmOff.png";

    private SelectionSystem _selection = default!;
    private ExecutableGoalSystem _executable = default!;
    private CombatModeSystem _combatMode = default!;
    private RimFortressWorldSystem _world = default!;
    private bool _resolved;

    private readonly Dictionary<EntityUid, PopIcon> _icons = new();

    private bool CombatMode
    {
        set
        {
            SetCombatIcon(value ? CombatModeIcon : NoCombatModeIcon);
            CombatButton.Pressed = value;
        }
    }

    public PopList()
    {
        IoCManager.InjectDependencies(this);
        InitializeComponent();
        SetCombatIcon(NoCombatModeIcon);
    }

    private void SetCombatIcon(string path)
        => CombatIcon.BackgroundImage = _resources.GetResource<TextureResource>(new ResPath(path)).Texture;

    private void AddPop(EntityUid uid)
    {
        if (_icons.ContainsKey(uid) || !_entityManager.EntityExists(uid))
            return;

        _entityManager.TryGetComponent(uid, out MetaDataComponent? meta);

        var icon = new PopIcon(uid, meta?.EntityName);
        _icons.Add(uid, icon);
        Icons.AddChild(icon);
    }

    private void RemovePop(EntityUid uid)
    {
        if (!_icons.TryGetValue(uid, out var icon))
            return;

        Icons.RemoveChild(icon);
        _icons.Remove(uid);
    }

    private void ResolveSystems()
    {
        if (_resolved)
            return;

        _resolved = true;
        _selection = _entityManager.System<SelectionSystem>();
        _executable = _entityManager.System<ExecutableGoalSystem>();
        _combatMode = _entityManager.System<CombatModeSystem>();
        _world = _entityManager.System<RimFortressWorldSystem>();
    }

    public void EnsureSetup()
    {
        ResolveSystems();

        if (_entityManager.TryGetComponent(_player.LocalEntity, out RimFortressPlayerComponent? comp))
            OnPlayerUpdate(new(_player.LocalEntity.Value, comp));
    }

    private void UpdateSelection()
    {
        ResolveSystems();

        var selected = _selection.Selected<EntityUid>();

        foreach (var (uid, icon) in _icons)
        {
            icon.Pressed = selected.Contains(uid);
        }

        UpdateCombat();
    }

    private void OnCombatModeChanged(Entity<CombatModeComponent> ent)
    {
        if (!_entityManager.TryGetComponent(_player.LocalEntity, out RimFortressPlayerComponent? player)
            || !player.Pops.Contains(ent))
            return;

        UpdateCombat();
    }

    private void UpdateCombat()
    {
        ResolveSystems();

        CombatMode = true;
        var selected = _selection.Selected<EntityUid>();

        foreach (var (uid, icon) in _icons)
        {
            var select = selected.Contains(uid);
            var combat = _combatMode.IsInCombatMode(uid);

            icon.CombatMode = combat;

            if ((select || selected.Count == 0) && !combat)
                CombatMode = false;
        }
    }

    private void OnPlayerUpdate(Entity<RimFortressPlayerComponent> ent)
    {
        foreach (var pop in ent.Comp.Pops)
        {
            if (!_icons.ContainsKey(pop))
                AddPop(pop);
            else if (!_entityManager.EntityExists(pop))
                RemovePop(pop);
        }

        foreach (var (uid, _) in _icons)
        {
            if (!ent.Comp.Pops.Contains(uid))
                RemovePop(uid);
        }

        UpdateCombat();
    }

    public void Clear()
    {
        Icons.RemoveAllChildren();
        _icons.Clear();
    }
}
