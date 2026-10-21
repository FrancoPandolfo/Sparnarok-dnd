using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Sparnarok.Api.Domain.Entities;
using Sparnarok.Api.Domain.Enums;
using Sparnarok.Api.Domain.ValueObjects;
using Xunit;
using Action = Sparnarok.Api.Domain.Entities.Action;

namespace Sparnarok.IntegrationTests;

public class CreaturePersistenceIntegrationTests : IClassFixture<PostgreSqlContainerFixture>
{
    private readonly PostgreSqlContainerFixture _fixture;

    public CreaturePersistenceIntegrationTests(PostgreSqlContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SparnarokDbContext_CanPersistAndRetrieveCreatureWithFullDomainGraph()
    {
        // Arrange
        await using var initContext = _fixture.CreateDbContext();
        await initContext.Database.EnsureCreatedAsync();

        // Verify seeded Weapon Masteries exist
        var seededMasteries = await initContext.WeaponMasteries.ToListAsync();
        seededMasteries.Should().HaveCount(8);
        var toppleMastery = seededMasteries.First(m => m.Property == MasteryProperty.Topple);

        // Create Creature aggregate with value objects
        var abilities = new AbilityScores(strength: 18, dexterity: 14, constitution: 16, intelligence: 10, wisdom: 12, charisma: 8);
        var hp = new HitPoints(max: 45, current: 40, temporary: 5);
        var ac = new ArmorClass(baseValue: 16, shieldBonus: 2, allowsDexterityModifier: false);

        var creature = new Creature(
            name: "Gorgon Vanguard",
            size: CreatureSize.Large,
            type: CreatureType.Monstrosity,
            alignment: "Neutral Evil",
            challengeRating: 5,
            speedFeet: 30,
            abilityScores: abilities,
            hitPoints: hp,
            armorClass: ac);

        var strikeAction = new Action(
            creatureId: creature.Id,
            name: "Iron Gore Strike",
            type: ActionType.Action,
            description: "Gores target with petrifying iron horns.",
            isAttack: true,
            reachOrRangeFeet: 5,
            damageFormula: "2d8",
            damageType: DamageType.Piercing,
            attackModifierAbility: AbilityScoreType.Strength);

        strikeAction.AssociateMastery(toppleMastery);
        creature.AddAction(strikeAction);
        creature.LearnWeaponMastery(toppleMastery);

        // Act: Persist
        initContext.Creatures.Add(creature);
        await initContext.SaveChangesAsync();

        // Assert: Read back in an isolated context
        await using var queryContext = _fixture.CreateDbContext();
        var retrieved = await queryContext.Creatures
            .Include(c => c.Actions)
                .ThenInclude(a => a.AssociatedMastery)
            .Include(c => c.KnownMasteries)
            .FirstOrDefaultAsync(c => c.Id == creature.Id);

        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("Gorgon Vanguard");
        retrieved.Size.Should().Be(CreatureSize.Large);
        retrieved.Type.Should().Be(CreatureType.Monstrosity);
        retrieved.ChallengeRating.Should().Be(5);
        retrieved.ProficiencyBonus.Should().Be(3);

        // Value Objects fidelity
        retrieved.AbilityScores.Strength.Should().Be(18);
        retrieved.AbilityScores.GetModifier(AbilityScoreType.Strength).Should().Be(4);
        retrieved.HitPoints.Max.Should().Be(45);
        retrieved.HitPoints.Current.Should().Be(40);
        retrieved.HitPoints.Temporary.Should().Be(5);
        retrieved.ArmorClass.Base.Should().Be(16);
        retrieved.ArmorClass.ShieldBonus.Should().Be(2);

        // Relationships fidelity
        retrieved.Actions.Should().HaveCount(1);
        var retrievedAction = retrieved.Actions.First();
        retrievedAction.Name.Should().Be("Iron Gore Strike");
        retrievedAction.DamageFormula.Should().Be("2d8");
        retrievedAction.DamageType.Should().Be(DamageType.Piercing);
        retrievedAction.AssociatedMastery.Should().NotBeNull();
        retrievedAction.AssociatedMastery!.Property.Should().Be(MasteryProperty.Topple);

        retrieved.KnownMasteries.Should().HaveCount(1);
        retrieved.KnownMasteries.First().Property.Should().Be(MasteryProperty.Topple);

        // Verify Cascade Delete
        queryContext.Creatures.Remove(retrieved);
        await queryContext.SaveChangesAsync();

        await using var verificationContext = _fixture.CreateDbContext();
        var actionOrphanCheck = await verificationContext.Actions.FirstOrDefaultAsync(a => a.Id == retrievedAction.Id);
        actionOrphanCheck.Should().BeNull("Deleting a creature must cascade delete its actions");
    }
}
