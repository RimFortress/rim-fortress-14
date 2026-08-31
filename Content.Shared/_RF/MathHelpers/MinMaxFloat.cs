using Robust.Shared.Random;

namespace Content.Shared._RF.MathHelpers;

[DataDefinition, Serializable]
public partial struct MinMaxFloat
{
    [DataField]
    public float Min;

    [DataField]
    public float Max;

    public MinMaxFloat(float min, float max)
    {
        Min = min;
        Max = max;
    }

    public readonly float Next(IRobustRandom random)
    {
        return random.NextFloat(Min, Max);
    }
}
