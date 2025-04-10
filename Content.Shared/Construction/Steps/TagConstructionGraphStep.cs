using Content.Shared.Tag;

namespace Content.Shared.Construction.Steps
{
    [DataDefinition]
    public sealed partial class TagConstructionGraphStep : ArbitraryInsertConstructionGraphStep
    {
        [DataField("tag")]
        private string? _tag;

        public override bool EntityValid(EntityUid uid, IEntityManager entityManager, IComponentFactory compFactory)
        {
            var tagSystem = entityManager.EntitySysManager.GetEntitySystem<TagSystem>();
            return !string.IsNullOrEmpty(_tag) && tagSystem.HasTag(uid, _tag);
        }

        // RimFortress Start
        public string? Tag => _tag; // I'm sorry, but we need this for GetBuildItemOperator
        // RimFortress End
    }
}
