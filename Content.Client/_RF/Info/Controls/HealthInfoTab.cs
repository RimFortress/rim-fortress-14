using System.Linq;
using Content.Shared._RF.ColorHelper;
using Content.Shared.Atmos;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.IdentityManagement;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Client._RF.UserInterface.Scope;
using Content.Shared._RF.Info;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._RF.Info.Controls;

public sealed partial class HealthInfoTab : InfoTab
{
    [Dependency] private IEntityManager _entity = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    /// <summary>
    /// The color with which the temperature will be displayed if it has
    /// reached the lowest possible temperature for the life of the entity
    /// </summary>
    public Color ColdTempColor { get; set; } = Color.FromHex("#0000CD");

    /// <summary>
    /// The color with which the temperature will be displayed if it has
    /// reached the maximum temperature possible for the life of the entity
    /// </summary>
    public Color HotTempColor { get; set; } = Color.FromHex("#FF4500");

    /// <summary>
    /// The color with which the amount of creature blood will be displayed if it is minimum
    /// </summary>
    public Color MinBloodLevelColor { get; set; } = Color.FromHex("#DC143C");

    /// <summary>
    /// The color that the creature's amount of blood will be displayed with if it is maximum
    /// </summary>
    public Color MaxBloodLevelColor { get; set; } = Color.FromHex("#44944A");

    /// <summary>
    /// The color that will be displayed by the damage text at the minimum value
    /// </summary>
    public Color MinDamageColor { get; set; } = Color.White;

    /// <summary>
    /// The color that will be displayed by the damage text at the maximum value
    /// </summary>
    public Color MaxDamageColor { get; set; } = Color.FromHex("#F80000");

    private static string NaNText => Loc.GetString("health-info-tab-unknown-value");

    public HealthInfoTab()
    {
        IoCManager.InjectDependencies(this);
        InitializeComponent();

        Scope.ListenController<PopInfoUIController, EntityHealthInfoResponse>(
            (c, h) => c.OnHealthInfo += h,
            (c, h) => c.OnHealthInfo -= h,
            OnHealthInfo);
    }

    private void OnHealthInfo(EntityHealthInfoResponse args)
    {
        if (float.IsNaN(args.Temperature))
            TemperatureLabel.Text = NaNText;
        else
        {
            var msg = new FormattedMessage();

            msg.PushColor(ColorHelper.InterpolatedColor(
                ColdTempColor,
                HotTempColor,
                args.Temperature,
                260f,
                360f));
            msg.AddText($"{args.Temperature - Atmospherics.T0C:F1}°C ({args.Temperature:F1}K)");
            msg.Pop();

            TemperatureLabel.SetMessage(msg);
        }

        if (float.IsNaN(args.BloodLevel))
            BloodLabel.Text = NaNText;
        else
        {
            var msg = new FormattedMessage();

            msg.PushColor(ColorHelper.InterpolatedColor(
                MinBloodLevelColor,
                MaxBloodLevelColor,
                args.BloodLevel,
                0f,
                1f));
            msg.AddText($"{args.BloodLevel * 100:F1}%");
            msg.Pop();

            BloodLabel.SetMessage(msg);
        }

        BleedingAlert.Visible = args.Bleeding;
    }

    protected override void Update(EntityUid uid)
    {
        GroupContainer.RemoveAllChildren();

        Controller<PopInfoUIController>().HealthInfoRequest(uid);

        var state = _entity.TryGetComponent(uid, out MobStateComponent? comp)
            ? comp.CurrentState
            : MobState.Invalid;

        StatusLabel.Text = Loc.GetString("health-info-tab-status",
            ("status", state.ToString().ToLowerInvariant()));

        BleedingAlert.Text = Loc.GetString("health-info-tab-bleeding-alert",
            ("name", Identity.Name(uid, _entity)));

        if (!_entity.TryGetComponent(uid, out DamageableComponent? damageable))
            return;

        var msg = new FormattedMessage();
        var system = _entity.System<DamageableSystem>();
        var ent = new Entity<DamageableComponent?>(uid, damageable);
        var total = system.GetTotalDamage(ent);
        var damage = system.GetAllDamage(ent);

        msg.PushColor(ColorHelper.InterpolatedColor(
            MinDamageColor,
            MaxDamageColor,
            total.Float(),
            0f,
            100f));
        msg.AddText(total.ToString());
        msg.Pop();

        DamageLabel.SetMessage(msg);

        foreach (var (groupId, groupDamage) in damage.GetDamagePerGroup(_proto).OrderByDescending(x => x.Value))
        {
            if (groupDamage <= 0)
                continue;

            GroupContainer.AddChild(new DamageGroupInfo(groupId, groupDamage, damage.DamageDict));
        }
    }
}
