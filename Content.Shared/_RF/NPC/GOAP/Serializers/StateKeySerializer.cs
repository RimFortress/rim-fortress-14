using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Validation;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Serialization.TypeSerializers.Interfaces;

namespace Content.Shared._RF.NPC.GOAP.Serializers;

[TypeSerializer]
public sealed class StateKeySerializer<T> : ITypeSerializer<StateKey<T>, ValueDataNode>, ITypeCopyCreator<StateKey<T>> where T : notnull
{
    public StateKey<T> CreateCopy(ISerializationManager serializationManager, StateKey<T> source, IDependencyCollection dependencies, SerializationHookContext hookCtx, ISerializationContext? context = null)
    {
        return source;
    }

    public StateKey<T> Read(ISerializationManager serializationManager, ValueDataNode node, IDependencyCollection dependencies, SerializationHookContext hookCtx, ISerializationContext? context = null, ISerializationManager.InstantiationDelegate<StateKey<T>>? instanceProvider = null)
    {
        return new StateKey<T>(node.Value);
    }

    public ValidationNode Validate(ISerializationManager serializationManager, ValueDataNode node, IDependencyCollection dependencies, ISerializationContext? context = null)
    {
        var parts = GoapState.GetOrParts<object>(node.Value);

        if (parts.Length == 0 && ValidateKey(node.Value) is { } validate)
            return validate;

        foreach (var part in parts)
        {
            if (ValidateKey(part) is { } validatePart)
                return validatePart;
        }

        return new ValidatedValueNode(node);

        ValidationNode? ValidateKey(StateKey<object> key)
        {
            var domains = GoapState.GetDomainParts(key);

            if (domains.Length == 0)
                return null;

            foreach (var domain in GoapState.DomainKeys)
            {
                if (domain.Validator(node, domains, dependencies) is { } valid)
                    return valid;
            }

            return null;
        }
    }

    public DataNode Write(ISerializationManager serializationManager, StateKey<T> value, IDependencyCollection dependencies, bool alwaysWrite = false, ISerializationContext? context = null)
    {
        return new ValueDataNode(value.Id);
    }
}
