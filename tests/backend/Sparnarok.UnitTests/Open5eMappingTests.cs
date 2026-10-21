using System.Text.Json;
using FluentAssertions;
using Sparnarok.Api.Application.DTOs;
using Sparnarok.Api.Domain.Entities;
using Sparnarok.Api.Domain.Enums;
using Sparnarok.Api.Domain.ValueObjects;
using Sparnarok.Api.Infrastructure.Services;
using Xunit;

namespace Sparnarok.UnitTests;

/// <summary>
/// Pruebas unitarias para el mapeo semántico y parsing del JSON de Open5e API v1
/// a la entidad de dominio <see cref="Creature"/> de Sparnarok.
/// </summary>
public class Open5eMappingTests
{
    private readonly Open5eSyncService _syncService;

    public Open5eMappingTests()
    {
        // Se instancia con mocks nulos para pruebas de mapeo puro en memoria
        _syncService = new Open5eSyncService(null!, null!, null!);
    }

    [Fact]
    public void MapToCreature_WithSampleOpen5eMonsterJson_MapsCoreDomainCorrectly()
    {
        // Arrange
        const string json = """
        {
            "slug": "a-mi-kuk",
            "name": "A-mi-kuk",
            "size": "Huge",
            "type": "Aberration",
            "subtype": "",
            "alignment": "chaotic evil",
            "armor_class": 14,
            "armor_desc": "natural armor",
            "hit_points": 115,
            "hit_dice": "10d12+50",
            "speed": { "swim": 40, "burrow": 20, "walk": 30 },
            "strength": 21,
            "dexterity": 8,
            "constitution": 20,
            "intelligence": 7,
            "wisdom": 14,
            "charisma": 10,
            "challenge_rating": "7",
            "cr": 7.0,
            "actions": [
                {
                    "name": "Multiattack",
                    "desc": "The a-mi-kuk makes two attacks: one with its bite and one with its grasping claw."
                },
                {
                    "name": "Bite",
                    "desc": "Melee Weapon Attack: +8 to hit, reach 5 ft., one target. Hit: 12 (2d6 + 5) piercing damage.",
                    "attack_bonus": 8,
                    "damage_dice": "2d6+5"
                },
                {
                    "name": "Grasping Claw",
                    "desc": "Melee Weapon Attack: +8 to hit, reach 10 ft., one target. Hit: 18 (3d8 + 5) bludgeoning damage.",
                    "attack_bonus": 8,
                    "damage_dice": "3d8+5"
                }
            ]
        }
        """;

        var dto = JsonSerializer.Deserialize<Open5eMonsterDto>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        })!;

        // Act
        var creature = _syncService.MapToCreature(dto);

        // Assert
        creature.Should().NotBeNull();
        creature.Name.Should().Be("A-mi-kuk");
        creature.Size.Should().Be(CreatureSize.Huge);
        creature.Type.Should().Be(CreatureType.Aberration);
        creature.Alignment.Should().Be("chaotic evil");
        creature.ChallengeRating.Should().Be(7.0);
        creature.ProficiencyBonus.Should().Be(3);
        creature.SpeedFeet.Should().Be(30);
        creature.IsPlayerCharacter.Should().BeFalse();

        // Puntos de Golpe
        creature.HitPoints.Max.Should().Be(115);
        creature.HitPoints.Current.Should().Be(115);

        // Clase de Armadura: Open5e provee la CA final (14)
        creature.EffectiveArmorClass.Should().Be(14);

        // Atributos
        creature.AbilityScores.Strength.Should().Be(21);
        creature.AbilityScores.GetModifier(AbilityScoreType.Strength).Should().Be(5);
        creature.AbilityScores.Dexterity.Should().Be(8);
        creature.AbilityScores.GetModifier(AbilityScoreType.Dexterity).Should().Be(-1);
        creature.AbilityScores.Constitution.Should().Be(20);
        creature.AbilityScores.Intelligence.Should().Be(7);
        creature.AbilityScores.Wisdom.Should().Be(14);
        creature.AbilityScores.Charisma.Should().Be(10);

        // Acciones
        creature.Actions.Should().HaveCount(3);
        var bite = creature.Actions.FirstOrDefault(a => a.Name == "Bite");
        bite.Should().NotBeNull();
        bite!.IsAttack.Should().BeTrue();
        bite.DamageFormula.Should().Be("2d6+5");

        var claw = creature.Actions.FirstOrDefault(a => a.Name == "Grasping Claw");
        claw.Should().NotBeNull();
        claw!.IsAttack.Should().BeTrue();
        claw.DamageFormula.Should().Be("3d8+5");
    }

    [Theory]
    [InlineData("1/2", 0.5)]
    [InlineData("1/4", 0.25)]
    [InlineData("1/8", 0.125)]
    [InlineData("0", 0.0)]
    [InlineData("5", 5.0)]
    [InlineData("15.5", 15.5)]
    public void ParseChallengeRating_ParsesFractionalAndDecimalStringsCorrectly(string crText, double expected)
    {
        var dto = new Open5eMonsterDto { ChallengeRating = crText, Cr = null };
        var cr = Open5eSyncService.ParseChallengeRating(dto);
        cr.Should().Be(expected);
    }

    [Fact]
    public void ParseSpeed_HandlesDictionaryAndStringFormats()
    {
        // 1. Objeto con clave walk numérico
        using var doc1 = JsonDocument.Parse("""{ "walk": 40, "swim": 20 }""");
        var speed1 = Open5eSyncService.ParseSpeed(doc1.RootElement);
        speed1.Should().Be(40);

        // 2. Objeto con walk como string "25 ft."
        using var doc2 = JsonDocument.Parse("""{ "walk": "25 ft." }""");
        var speed2 = Open5eSyncService.ParseSpeed(doc2.RootElement);
        speed2.Should().Be(25);

        // 3. Speed nulo o vacío
        var speed3 = Open5eSyncService.ParseSpeed(null);
        speed3.Should().Be(30);
    }

    [Fact]
    public void CustomHeroCreature_WithExplicitPlayerCharacterFlag_SetsPropertiesAccurately()
    {
        // Arrange
        var abilities = new AbilityScores(18, 14, 16, 10, 12, 8);
        var hp = new HitPoints(45);
        var ac = new ArmorClass(18, 0, false);

        var hero = new Creature(
            "Sir Galahad",
            CreatureSize.Medium,
            CreatureType.Humanoid,
            "Heroic",
            challengeRating: 1,
            speedFeet: 30,
            abilityScores: abilities,
            hitPoints: hp,
            armorClass: ac,
            isPlayerCharacter: true);

        // Assert
        hero.IsPlayerCharacter.Should().BeTrue();
        hero.EffectiveArmorClass.Should().Be(18);
        hero.HitPoints.Max.Should().Be(45);
        hero.AbilityScores.Strength.Should().Be(18);
    }
}
