#nullable enable
using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared._RF.NPC.Engagement.Systems;
using Content.Shared._RF.NPC.Systems;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Whitelist;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._RF.NPC;

/// <summary>
/// What is owned has a life that is tied to its owner: everything that an entity carries is owned with it (as far as the lists let it), and when the owner lets go of
/// what it owns, or is gone itself, what lives for the sake of its owner is gone with it.
/// </summary>
public sealed class OwnershipLifecycleTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: TestOwnerDespawning
  components:
  - type: DespawnWithOwner
";

    [SidedDependency(Side.Server)] private readonly IEntityManager _entMan = default!;
    [SidedDependency(Side.Server)] private readonly OwnershipSystem _ownership = default!;
    [SidedDependency(Side.Server)] private readonly EngagementSystem _engagement = default!;
    [SidedDependency(Side.Server)] private readonly InventorySystem _inventory = default!;

    private EntityCoordinates _coords;

    private async Task Setup()
    {
        var map = await Pair.CreateTestMap();
        _coords = new EntityCoordinates(map.Grid, 0.5f, 0.5f);
    }

    private new EntityUid Spawn(string? proto = "MobHuman") => _entMan.SpawnEntity(proto, _coords);

    [Test]
    public async Task TestOwnerOwnsWhatTheOwnedCarries()
    {
        await Setup();

        await Pair.Server.WaitAssertion(() =>
        {
            var owner = Spawn(null);
            var mob = Spawn();
            var hat = Spawn("ClothingHeadHelmetBasic");
            Assert.That(_inventory.TryEquip(mob, hat, "head", force: true), Is.True);

            _ownership.AddOwnershipRecursively(owner, mob);

            Assert.That(_ownership.HasOwned(owner, mob), Is.True);
            Assert.That(_ownership.HasOwned(owner, hat), Is.True, "what it wears is owned too");
        });
    }

    [Test]
    public async Task TestListsTellWhatOfTheCarriedIsOwned()
    {
        await Setup();

        await Pair.Server.WaitAssertion(() =>
        {
            var owner = Spawn(null);
            var mob = Spawn();
            var hat = Spawn("ClothingHeadHelmetBasic");
            Assert.That(_inventory.TryEquip(mob, hat, "head", force: true), Is.True);

            // Only the items: the body is owned whatever the list says, and its parts and solutions are not.
            var items = new EntityWhitelist { Components = new[] { "Item" } };
            _ownership.AddOwnershipRecursively(owner, mob, items);

            Assert.That(_ownership.HasOwned(owner, mob), Is.True, "the entity itself is owned whatever the lists say");
            Assert.That(_ownership.HasOwned(owner, hat), Is.True);
            Assert.That(_ownership.GetOwned(owner).All(x => x == mob || _entMan.HasComponent<ItemComponent>(x)), Is.True,
                "nothing but the items are owned of what it carries");

            // And what the black list lets pass is not.
            var other = Spawn();
            var helmet = Spawn("ClothingHeadHelmetBasic");
            Assert.That(_inventory.TryEquip(other, helmet, "head", force: true), Is.True);
            _ownership.AddOwnershipRecursively(owner, other, blacklist: items);

            Assert.That(_ownership.HasOwned(owner, helmet), Is.False);
        });
    }

    [Test]
    public async Task TestOwnershipIsTakenAwayFromWhatIsCarriedToo()
    {
        await Setup();

        await Pair.Server.WaitAssertion(() =>
        {
            var owner = Spawn(null);
            var mob = Spawn();
            var hat = Spawn("ClothingHeadHelmetBasic");
            Assert.That(_inventory.TryEquip(mob, hat, "head", force: true), Is.True);

            _ownership.AddOwnershipRecursively(owner, mob);
            _ownership.RemoveOwnershipRecursively(owner, mob);

            Assert.That(_ownership.HasOwned(owner, mob), Is.False);
            Assert.That(_ownership.HasOwned(owner, hat), Is.False);
            Assert.That(_ownership.GetOwned(owner), Is.Empty);
            Assert.That(_entMan.Deleted(mob), Is.False, "it is only no longer owned");
        });
    }

    private bool Gone(EntityUid uid) => _entMan.Deleted(uid) || _entMan.GetComponent<MetaDataComponent>(uid).EntityLifeStage >= EntityLifeStage.Terminating;

    [Test]
    public async Task TestWhatLivesForTheSakeOfItsOwnerIsGoneWithIt()
    {
        await Setup();
        EntityUid owner = default, other = default, bound = default, sharedBound = default, never = default;

        await Pair.Server.WaitPost(() =>
        {
            owner = Spawn(null);
            other = Spawn(null);
            bound = Spawn("TestOwnerDespawning");
            sharedBound = Spawn("TestOwnerDespawning");
            never = Spawn("TestOwnerDespawning");

            _ownership.AddOwnership(owner, owned: new[] { bound, sharedBound });
            _ownership.AddOwnership(other, owned: new[] { sharedBound });

            _ownership.RemoveOwnership(owner, owned: bound);
            _ownership.RemoveOwnership(owner, owned: sharedBound);
        });
        await Pair.RunTicksSync(3);

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(Gone(bound), Is.True, "it has lost its owner");
            Assert.That(Gone(sharedBound), Is.False, "it has another one");
            Assert.That(Gone(never), Is.False, "it never had an owner: it has not lost any");
        });

        // And with the end of the last owner.
        await Pair.Server.WaitPost(() => _entMan.DeleteEntity(other));
        await Pair.RunTicksSync(3);
        await Pair.Server.WaitAssertion(() => Assert.That(Gone(sharedBound), Is.True));
    }

    [Test]
    public async Task TestSituationsOfAnEventAreGoneWhenItLetsGo()
    {
        await Setup();
        EntityUid squad = default;

        await Pair.Server.WaitPost(() =>
        {
            var event_ = Spawn(null);
            var member = Spawn();

            Assert.That(_engagement.TryStartEngagement("RaidSquad", member, out var started), Is.True);
            squad = started!.Value.Owner;
            _ownership.AddOwnership(squad, owner: event_);

            _ownership.RemoveOwnershipRecursively(event_, squad);
        });
        await Pair.RunTicksSync(3);

        await Pair.Server.WaitAssertion(() =>
            Assert.That(Gone(squad), Is.True, "the situations live for the sake of the one who owns them"));
    }
}
