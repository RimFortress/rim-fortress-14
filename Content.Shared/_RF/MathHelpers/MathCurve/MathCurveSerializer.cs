
using Content.Shared._RF.MathHelpers.MathCurve.Curves;
using Robust.Shared.Reflection;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Validation;
using Robust.Shared.Serialization.TypeSerializers.Interfaces;

namespace Content.Shared._RF.MathHelpers.MathCurve;

[TypeSerializer]
public sealed class MathCurveSerializer : ITypeReader<MathCurve, MappingDataNode>
{
    private Type? GetType(MappingDataNode node)
    {
        if (node.Has("float"))
            return typeof(FloatCurve);

        if (node.Has("div"))
            return typeof(DivideCurve);

        if (node.Has("mul"))
            return typeof(MultiplyCurve);

        if (node.Has("add"))
            return typeof(AddCurve);

        if (node.Has("minus"))
            return typeof(MinusCurve);

        if (node.Has("pow"))
            return typeof(PowCurve);

        if (node.Has("moreThan") || node.Has("lessThan"))
            return typeof(ConditionCurve);

        if (node.Has("clamp"))
            return typeof(ClampCurve);

        if (node.Has("random"))
            return typeof(RandomCurve);

        if (node.Has("preset"))
            return typeof(PrototypeCurve);

        return null;
    }

    public MathCurve Read(
        ISerializationManager serializationManager,
        MappingDataNode node,
        IDependencyCollection dependencies,
        SerializationHookContext hookCtx,
        ISerializationContext? context = null,
        ISerializationManager.InstantiationDelegate<MathCurve>? instanceProvider = null)
    {
        if (GetType(node) is { } type)
            return (MathCurve)serializationManager.Read(type, node, hookCtx, context)!;

        var reflection = dependencies.Resolve<IReflectionManager>();

        if (node.Tag == null)
            throw new NullReferenceException($"Found null tag for MathCurve");

        var typeString = node.Tag[6..];

        if (!reflection.TryLooseGetType(typeString, out type))
            throw new NullReferenceException($"Unable to find type for {typeString}");

        return (MathCurve)serializationManager.Read(type, node, hookCtx, context)!;
    }

    public ValidationNode Validate(
        ISerializationManager serializationManager,
        MappingDataNode node,
        IDependencyCollection dependencies,
        ISerializationContext? context = null)
    {
        if (GetType(node) is { } type)
            return serializationManager.ValidateNode(type, node, context);

        var reflection = dependencies.Resolve<IReflectionManager>();

        if (node.Tag == null)
            return new ErrorNode(node, $"Unable to validate MathCurve type");

        var typeString = node.Tag[6..];

        if (!reflection.TryLooseGetType(typeString, out type))
            return new ErrorNode(node, $"Unable to find type for {typeString}");

        return serializationManager.ValidateNode(type, node, context);
    }
}
