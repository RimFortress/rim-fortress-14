using Content.Shared._RF.MathHelpers.MathCurve.Systems;
using Content.Shared.Trigger;
using JetBrains.Annotations;

namespace Content.Shared._RF.MathHelpers.MathVariables;

/// <summary>
/// The variables of the entities that keep numbers, see <see cref="MathVariablesComponent"/>.
/// </summary>
public sealed partial class MathVariablesSystem : EntitySystem
{
    [Dependency] private MathCurvesSystem _curves = null!;
    [Dependency] private EntityQuery<MathVariablesComponent> _query;

    [SubscribeLocalEvent]
    public void OnMapInit(Entity<MathVariablesComponent> ent, ref MapInitEvent args)
    {
        foreach (var (key, curves) in ent.Comp.InitVariables)
        {
            Set(ent.AsNullable(), key, _curves.Get(curves));
        }
    }

    [SubscribeLocalEvent]
    private void OnTrigger(Entity<MathVariablesComponent> ent, ref TriggerEvent args)
    {
        if (ent.Comp.Metrics.Count > 0 && args.Key == ent.Comp.MetricsKey)
            UpdateMetrics(ent.AsNullable());
    }

    /// <summary>
    /// Calculates the metrics of the entity (<see cref="MathVariablesComponent.Metrics"/>) again.
    /// </summary>
    [PublicAPI]
    public void UpdateMetrics(Entity<MathVariablesComponent?> ent)
    {
        if (!_query.Resolve(ent, ref ent.Comp, false))
            return;

        // The metrics that are not calculated yet are 0, and not a variable that is not there.
        foreach (var key in ent.Comp.Metrics.Keys)
        {
            ent.Comp.Variables.TryAdd(key, 0f);
        }

        foreach (var (key, curves) in ent.Comp.Metrics)
        {
            ent.Comp.Variables[key] = _curves.Get(curves, ent.Comp.Variables[key], ent.Owner, GetVariables(ent));
        }
    }

    /// <summary>
    /// Sets a variable.
    /// </summary>
    [PublicAPI]
    public void Set(Entity<MathVariablesComponent?> ent, string key, float value)
    {
        if (_query.Resolve(ent, ref ent.Comp, false))
            ent.Comp.Variables[key] = value;
    }

    /// <summary>
    /// Returns a variable, 0 if it has not been set.
    /// </summary>
    [PublicAPI, Pure]
    public float Get(Entity<MathVariablesComponent?> ent, string key)
        => _query.Resolve(ent, ref ent.Comp, false) ? ent.Comp.Variables.GetValueOrDefault(key) : 0f;

    /// <summary>
    /// Returns a copy of the variables, which a curve is calculated with.
    /// </summary>
    [PublicAPI, Pure]
    public Dictionary<string, float> GetVariables(Entity<MathVariablesComponent?> ent)
        => _query.Resolve(ent, ref ent.Comp, false) ? new Dictionary<string, float>(ent.Comp.Variables) : new();
}
