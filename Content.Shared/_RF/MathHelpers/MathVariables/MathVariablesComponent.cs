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
    /// Current values of the variables.
    /// </summary>
    [DataField("currentVariables")]
    public readonly Dictionary<string, float> Variables = new();
}
