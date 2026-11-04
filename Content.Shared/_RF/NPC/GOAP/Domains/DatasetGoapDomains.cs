using System.Diagnostics.CodeAnalysis;
using Content.Shared.Dataset;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._RF.NPC.GOAP.Domains;

/// <summary>
/// The keys of the domains <c>Dataset/ProtoId/...</c> and <c>LocalizedDataset/ProtoId/...</c>: all the values of a dataset, or one
/// of them at random. They do not change with the world, so nobody has to be told about them.
/// </summary>
public sealed partial class DatasetGoapDomains : EntitySystem, IGoapDomainProvider
{
    [Dependency] private GoapDomainSystem _domains = null!;
    [Dependency] private IPrototypeManager _proto = null!;
    [Dependency] private IRobustRandom _random = null!;

    public override void Initialize()
    {
        base.Initialize();

        _domains.Register(this, "Dataset", "LocalizedDataset");
    }

    public bool TryResolve<T>(
        GoapState state,
        EntityUid owner,
        StateKey<T>[] domains,
        [NotNullWhen(true)] out object? value) where T : notnull
    {
        value = null;

        if (GoapState.DatasetAllDomain.TryGetParams(domains, out ProtoId<DatasetPrototype>? dataset))
        {
            if (!_proto.TryIndex(dataset, out var proto))
                return false;

            value = proto.Values;
            return true;
        }

        if (GoapState.DatasetRandomDomain.TryGetParams(domains, out dataset))
        {
            if (!_proto.TryIndex(dataset, out var proto))
                return false;

            value = _random.Pick(proto.Values);
            return true;
        }

        if (GoapState.LocalizedDatasetAllDomain.TryGetParams(domains, out ProtoId<LocalizedDatasetPrototype>? localeDataset))
        {
            if (!_proto.TryIndex(localeDataset, out var proto))
                return false;

            value = proto.Values;
            return true;
        }

        if (GoapState.LocalizedDatasetRandomDomain.TryGetParams(domains, out localeDataset))
        {
            if (!_proto.TryIndex(localeDataset, out var proto))
                return false;

            value = _random.Pick(proto.Values);
            return true;
        }

        return false;
    }
}
