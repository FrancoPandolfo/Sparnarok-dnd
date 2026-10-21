using FluentAssertions;
using Sparnarok.Api.Domain.Entities;
using Sparnarok.Api.Domain.Enums;
using Sparnarok.Api.Domain.ValueObjects;
using Xunit;
using Action = Sparnarok.Api.Domain.Entities.Action;

namespace Sparnarok.UnitTests;

public class CreatureDomainTests
{
    [Theory]
    [InlineData(0, 2)]
    [InlineData(1, 2)]
    [InlineData(4, 2)]
    [InlineData(5, 3)]
    [InlineData(8, 3)]
    [InlineData(9, 4)]
    [InlineData(12, 4)]
    [InlineData(13, 5)]
    [InlineData(17, 6)]
    [InlineData(21, 7)]
    [InlineData(25, 8)]
    [InlineData(29, 9)]
    [InlineData(30, 9)]
    public void CalculateProficiencyBonus_MapsToOfficialDndScaling(double cr, int expectedProficiency)
    {
        Creature.CalculateProficiencyBonus(cr).Should().Be(expectedProficiency);
    }

    [Fact]
    public void Creature_EffectiveArmorClass_IntegratesDexterityModifier()
    {
        // Arrange: Creature with Dex 16 (+3 modifier) and unarmored defense (Base 10)
        var abilityScores = new AbilityScores(10, 16, 14, 10, 10, 10);
        var creature = new Creature(
            name: "Elven Scout",
            size: CreatureSize.Medium,
            type: CreatureType.Humanoid,
            alignment: "Neutral",
            challengeRating: 1,
            abilityScores: abilityScores,
            armorClass: ArmorClass.Unarmored);

        // Assert
        creature.EffectiveArmorClass.Should().Be(13); // 10 + 3
    }

    [Fact]
    public void CalculateAttackBonus_IntegratesAbilityModifierAndProficiencyBonus()
    {
        // Arrange: Str 18 (+4), CR 5 (+3 PB)
        var scores = new AbilityScores(18, 10, 14, 10, 10, 10);
        var creature = new Creature(
            name: "Veteran Warrior",
            size: CreatureSize.Medium,
            type: CreatureType.Humanoid,
            alignment: "Lawful Neutral",
            challengeRating: 5,
            abilityScores: scores);

        var greatswordAction = new Action(
            creatureId: creature.Id,
            name: "Greatsword Strike",
            type: ActionType.Action,
            isAttack: true,
            attackModifierAbility: AbilityScoreType.Strength,
            damageFormula: "2d6",
            damageType: DamageType.Slashing);

        // Act
        var attackBonus = creature.CalculateAttackBonus(greatswordAction);

        // Assert: +4 (Str) + +3 (PB) = +7
        attackBonus.Should().Be(7);
    }

    [Fact]
    public void CalculateSavingThrowDc_ComputesEightPlusProficiencyPlusModifier()
    {
        // Arrange: CR 9 (+4 PB), Con 16 (+3 modifier)
        var scores = new AbilityScores(16, 12, 16, 10, 10, 10);
        var dragon = new Creature(
            name: "Young Green Dragon",
            size: CreatureSize.Large,
            type: CreatureType.Dragon,
            alignment: "Lawful Evil",
            challengeRating: 9,
            abilityScores: scores);

        // Act
        var dc = dragon.CalculateSavingThrowDc(AbilityScoreType.Constitution);

        // Assert: 8 + 4 (PB) + 3 (Con mod) = 15
        dc.Should().Be(15);
    }

    [Fact]
    public void ActionAndWeaponMastery_CanBeAssociatedAndManagedSeamlessly()
    {
        // Arrange
        var creature = new Creature(
            name: "Weapon Master Fighter",
            size: CreatureSize.Medium,
            type: CreatureType.Humanoid,
            alignment: "Neutral Good",
            challengeRating: 3);

        var toppleMastery = WeaponMastery.Presets.Topple;
        var halberdAction = new Action(
            creatureId: creature.Id,
            name: "Halberd Strike",
            type: ActionType.Action,
            isAttack: true,
            attackModifierAbility: AbilityScoreType.Strength,
            damageFormula: "1d10",
            damageType: DamageType.Slashing);

        // Act: Teach mastery and bind to action
        creature.LearnWeaponMastery(toppleMastery);
        halberdAction.AssociateMastery(toppleMastery);
        creature.AddAction(halberdAction);

        // Assert
        creature.Actions.Should().ContainSingle(a => a.Name == "Halberd Strike");
        creature.KnownMasteries.Should().ContainSingle(m => m.Property == MasteryProperty.Topple);
        halberdAction.AssociatedMastery.Should().NotBeNull();
        halberdAction.AssociatedMastery!.Property.Should().Be(MasteryProperty.Topple);

        // Act: Remove Action
        creature.RemoveAction(halberdAction.Id);
        creature.Actions.Should().BeEmpty();
    }

    [Fact]
    public void TakeDamageAndHeal_UpdatesCreatureHealthStateConsistently()
    {
        // Arrange
        var creature = new Creature(
            name: "Dwarf Defender",
            size: CreatureSize.Medium,
            type: CreatureType.Humanoid,
            alignment: "Lawful Good",
            challengeRating: 2,
            hitPoints: new HitPoints(50));

        // Act: Take 30 damage -> 20 HP remaining (Bloodied!)
        creature.TakeDamage(30);

        // Assert
        creature.HitPoints.Current.Should().Be(20);
        creature.HitPoints.IsBloodied.Should().BeTrue();

        // Act: Heal 15 HP -> 35 HP (No longer Bloodied)
        creature.Heal(15);

        // Assert
        creature.HitPoints.Current.Should().Be(35);
        creature.HitPoints.IsBloodied.Should().BeFalse();
    }
}
