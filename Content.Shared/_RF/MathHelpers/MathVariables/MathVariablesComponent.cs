namespace Content.Shared._RF.MathHelpers.MathVariables;

/// <summary>
/// Numbers that an entity keeps, by name: the variables that its curves see, see <see cref="MathVariablesSystem"/>.
/// </summary>
[RegisterComponent]
[Access(typeof(MathVariablesSystem))]
public sealed partial class MathVariablesComponent : Component
{
    /// <summary>
    /// Variables whose values will be calculated and stored in <see cref="Variables"/> during initialization.
    /// </summary>
    [DataField("variables")]
    public Dictionary<string, List<MathCurve.MathCurve>> InitVariables = new();

    /// <summary>
    /// The metrics: variables that are calculated again every time that the entity is triggered by <see cref="MetricsKey"/>, one
    /// after another in the order they are written, so a metric can use those before it (and the values of the others from the
    /// last time). They are the first to be calculated of what happens on the trigger: the phases and the effects of the
    /// entity see the fresh numbers. A metric does not need a value to start with, until it is calculated it is 0.
    /// </summary>
    [DataField]
    public Dictionary<string, List<MathCurve.MathCurve>> Metrics = new();

    /// <summary>
    /// The key of the trigger that the <see cref="Metrics"/> are calculated by.
    /// </summary>
    [DataField]
    public string MetricsKey = "tick";

    /// <summary>
    /// Current values of the variables.
    /// </summary>
    [DataField("currentVariables")]
    public Dictionary<string, float> Variables = new();
}
