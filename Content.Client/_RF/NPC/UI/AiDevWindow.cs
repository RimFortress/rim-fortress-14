using Content.Shared._RF.NPC.GOAP.Components;
using JetBrains.Annotations;
using Content.Client._RF.Suil;
using Content.Client._RF.UserInterface.Controls;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.UtilityAi;
using Content.Client._RF.UserInterface.Scope;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Console;
using Robust.Shared.Timing;

namespace Content.Client._RF.NPC.UI;

/// <summary>
/// The window for the debug of the AI of an NPC: a tab for each of its brains.
/// </summary>
public sealed partial class AiDevWindow : RfBoxContainer
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IEntityManager _entity = default!;

    private bool _goapReady;
    private bool _utilityAiReady;
    private bool _searcherReady;

    private AiDevWindowUiController AiController => Controller<AiDevWindowUiController>();

    public EntityUid? Target { get; private set; }

    public AiDevWindow()
    {
        IoCManager.InjectDependencies(this);
        InitializeComponent();

        RfTabContainer.SetTabTitle(GoapDebugTab, "GOAP");
        RfTabContainer.SetTabTitle(UtilityAiDebugTab, "Utility AI");
        RfTabContainer.SetTabTitle(SearcherDebugTab, "Searcher");

        Scope.ListenController<AiDevWindowUiController, (EntityUid Target, GoapPlanDebugInfo? Plan, GoapStaticGraphDebug Graph)>(
            (c, h) => c.OnGoapDebugInfo += h,
            (c, h) => c.OnGoapDebugInfo -= h,
            args =>
            {
                Target = args.Target;
                Refresh.Disabled = false;
                UpdateInfo();
                GoapDebugTab.Update(args.Graph, args.Plan);
                _goapReady = true;
                UpdateRefresh();
            });

        Scope.ListenController<AiDevWindowUiController, (EntityUid Target, UtilityAiDebugInfo Info)>(
            (c, h) => c.OnUtilityAiDebugInfo += h,
            (c, h) => c.OnUtilityAiDebugInfo -= h,
            args =>
            {
                Target = args.Target;
                UpdateInfo();
                UtilityAiDebugTab.Update(args.Info);
                _utilityAiReady = true;
                UpdateRefresh();
            });

        Scope.ListenController<AiDevWindowUiController, (EntityUid Target, NpcSearchGraph Info)>(
            (c, h) => c.OnNpcSearchDebugInfo += h,
            (c, h) => c.OnNpcSearchDebugInfo -= h,
            args =>
            {
                Target = args.Target;
                UpdateInfo();
                SearcherDebugTab.Update(args.Info);
                _searcherReady = true;
                UpdateRefresh();
            });

        Refresh.OnPressed += _ => SetTarget(Target!.Value);
    }

    public void SetTarget(EntityUid target)
    {
        _goapReady = false;
        _utilityAiReady = false;
        _searcherReady = false;
        UpdateRefresh();
        GoapDebugTab.SetTarget(target);
        AiController.RequestUtilityAiDebug(target);
        AiController.RequestNpcSearchDebug(target);
    }

    private void UpdateInfo()
    {
        TargetLabel.Text = $"Target: {_entity.ToPrettyString(Target)}";
        TimeLabel.Text = $"CurTime: {_timing.CurTime.ToString()}";
        TickLabel.Text = $"CurTick: {_timing.CurTick.Value}";
    }

    private void UpdateRefresh() => Refresh.Disabled = !_goapReady || !_utilityAiReady || !_searcherReady;
}

[UsedImplicitly]
internal sealed partial class AiWindowCommand : LocalizedCommands
{
    [Dependency] private IEntityManager _entManager = default!;

    public override string Command => "aidevwindow";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError(Loc.GetString($"shell-need-exactly-one-argument"));
            return;
        }

        if (!_entManager.TryParseNetEntity(args[0], out var uid))
        {
            shell.WriteError(Loc.GetString("shell-entity-uid-must-be-number"));
            return;
        }

        var window = new OSWindow { Title = "AI Debug Window" };
        var control = new AiDevWindow();

        window.AddChild(control);
        window.Show();

        control.SetTarget(uid.Value);
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length != 1
            ? CompletionResult.Empty
            : CompletionResult.FromHintOptions(CompletionHelper.Components<GoapComponent>(args[0]), "NPC uid");
    }
}
