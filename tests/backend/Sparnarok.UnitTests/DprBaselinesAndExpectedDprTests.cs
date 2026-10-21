using FluentAssertions;
using Sparnarok.Api.Domain.Entities;
using Sparnarok.Api.Domain.Enums;
using Sparnarok.Api.Domain.Services.Dice;
using Sparnarok.Api.Domain.Services.Rng;
using Sparnarok.Api.Domain.ValueObjects;
using Sparnarok.Api.Infrastructure.Services;
using Xunit;

namespace Sparnarok.UnitTests;

/// <summary>
/// Suite de pruebas unitarias para la abstracción de arquetipos y matrices de daño esperado por ronda (Expected DPR).
/// Valida el catálogo de baselines, la omisión del d20 vs CA y el rango de varianza de ±15% en el CombatSimulatorService.
/// </summary>
public class DprBaselinesAndExpectedDprTests
{
    [Fact]
    public void DprBaselineService_ContainsFighter2H_AndLevel5HasExpectedDprOf19_4()
    {
        // Arrange
        var service = new DprBaselineService();

        // Act
        var dpr = service.GetExpectedDpr("Fighter 2H", 5);
        var archetypes = service.GetArchetypes();

        // Assert
        dpr.Should().NotBeNull();
        dpr!.Value.Should().Be(19.4);
        archetypes.Should().Contain("Fighter 2H");
        archetypes.Should().Contain("Ranger Ranged");
        archetypes.Should().Contain("Paladin 2H");
    }

    [Fact]
    public void DprBaselineService_InvalidArchetypeOrLevel_ReturnsNull()
    {
        // Arrange
        var service = new DprBaselineService();

        // Act & Assert
        service.GetExpectedDpr("Unknown Class", 5).Should().BeNull();
        service.GetExpectedDpr("Fighter 2H", 0).Should().BeNull();
        service.GetExpectedDpr("Fighter 2H", 25).Should().BeNull();
    }

    [Fact]
    public void DprBaselineService_GetAllBaselines_Returns20LevelsPerArchetype()
    {
        // Arrange
        var service = new DprBaselineService();

        // Act
        var baselines = service.GetAllBaselines();
        var fighterBaselines = service.GetBaselinesForArchetype("Fighter 2H");

        // Assert
        baselines.Should().NotBeEmpty();
        fighterBaselines.Should().HaveCount(20);
        fighterBaselines[4].Level.Should().Be(5);
        fighterBaselines[4].ExpectedDpr.Should().Be(19.4);
    }

    [Fact]
    public void SimulateDuel_WhenAttackerIsPlayerCharacterWithExpectedDpr_BypassesD20AndDamagesDefenderDirectly()
    {
        // Arrange
        // Defensor: Monstruo con CA 35 (imposible de impactar mediante tirada normal de d20) y 50 HP
        var monster = new Creature(
            name: "Golem Impenetrable",
            size: CreatureSize.Large,
            type: CreatureType.Construct,
            alignment: "Neutral",
            challengeRating: 10,
            speedFeet: 20,
            abilityScores: AbilityScores.StandardBaseline,
            hitPoints: new HitPoints(50),
            armorClass: new ArmorClass(35, 0, false));

        // Atacante: Héroe Guerrero Nivel 5 con ExpectedDpr = 20.0 (sin arma requerida)
        var hero = new Creature(
            name: "Fighter Champion",
            size: CreatureSize.Medium,
            type: CreatureType.Humanoid,
            alignment: "Lawful Good",
            challengeRating: 5,
            speedFeet: 30,
            abilityScores: AbilityScores.StandardBaseline,
            hitPoints: new HitPoints(100),
            armorClass: new ArmorClass(16, 0, false),
            isPlayerCharacter: true,
            level: 5,
            expectedDpr: 20.0,
            archetype: "Fighter 2H");

        // RNG Determinista:
        // 1. Iniciativa Héroe: 20 -> total 22 (empieza el héroe)
        // 2. Iniciativa Monstruo: 1 -> total 1
        // 3. Varianza en turno 1: valor 15 en rango [-15, 16) da 0% de varianza -> daño = 20
        // 4. Ataque monstruo: falla
        // 5. Varianza en turno 2: valor 15 da 0% -> daño = 20
        // 6. Ataque monstruo: falla
        // 7. Varianza en turno 3: valor 15 da 0% -> daño = 20 -> Monstruo derrotado (50 HP)
        var deterministicRng = new DeterministicRandomProvider(new[]
        {
            20, 1,      // Iniciativa
            15,         // Varianza turno 1 héroe (-15 + 15 = 0%)
            2,          // Ataque monstruo (falla vs CA 16)
            15,         // Varianza turno 2 héroe (0%)
            2,          // Ataque monstruo (falla)
            15,         // Varianza turno 3 héroe (0%)
        });


        var diceRoller = new DiceRoller(deterministicRng);
        var simulator = new CombatSimulatorService(deterministicRng, diceRoller);

        var monsterWeapon = new Weapon("Puño de Acero", "1d8", DamageType.Bludgeoning);

        // Act - El héroe no requiere arma equipada
        var result = simulator.SimulateDuel(hero, null, monster, monsterWeapon);

        // Assert
        result.WinnerName.Should().Be("Fighter Champion");
        result.WinnerId.Should().Be(hero.Id);
        result.TotalRounds.Should().Be(3);

        // Validar que en los logs, el ataque del héroe tiene D20Roll = 0, IsHit = true y aplica daño directo
        var heroLogs = result.Logs.Where(l => l.AttackerName == "Fighter Champion").ToList();
        heroLogs.Should().NotBeEmpty();
        foreach (var log in heroLogs)
        {
            log.D20Roll.Should().Be(0);
            log.IsHit.Should().BeTrue();
            log.DamageDealt.Should().Be(20);
            log.Description.Should().Contain("DPR de arquetipo");
        }
    }

    [Fact]
    public void SimulateDuel_WhenExpectedDprApplies_DamageHonors15PercentVarianceRange()
    {
        // Arrange
        const double expectedDpr = 20.0;
        // Rango esperado: 20 * 0.85 = 17 hasta 20 * 1.15 = 23
        const int minExpected = 17;
        const int maxExpected = 23;

        var monster = new Creature(
            name: "Saco de Pruebas",
            size: CreatureSize.Medium,
            type: CreatureType.Humanoid,
            alignment: "Neutral",
            challengeRating: 1,
            speedFeet: 30,
            hitPoints: new HitPoints(500),
            armorClass: new ArmorClass(10, 0, false));

        var hero = new Creature(
            name: "Héroe Varianza",
            size: CreatureSize.Medium,
            type: CreatureType.Humanoid,
            alignment: "Neutral Good",
            challengeRating: 5,
            hitPoints: new HitPoints(500),
            armorClass: new ArmorClass(20, 0, false),
            isPlayerCharacter: true,
            level: 5,
            expectedDpr: expectedDpr,
            archetype: "Fighter 2H");

        var rng = new DefaultRandomProvider();
        var diceRoller = new DiceRoller(rng);
        var simulator = new CombatSimulatorService(rng, diceRoller);

        var dummyWeapon = new Weapon("Porra", "1d4", DamageType.Bludgeoning);

        // Act - Ejecutar 50 duelos cortos rápidos
        for (int i = 0; i < 50; i++)
        {
            var result = simulator.SimulateDuel(hero, null, monster, dummyWeapon);
            var heroAttack = result.Logs.First(l => l.AttackerName == "Héroe Varianza");

            // Assert
            heroAttack.DamageDealt.Should().BeInRange(minExpected, maxExpected);
        }
    }

    [Fact]
    public void SimulateFastDuel_PlayerCharacterWithExpectedDpr_RunsWithoutEquippedWeapon()
    {
        // Arrange
        var hero = new Creature(
            name: "Ranger",
            size: CreatureSize.Medium,
            type: CreatureType.Humanoid,
            alignment: "Neutral Good",
            challengeRating: 5,
            hitPoints: new HitPoints(40),
            armorClass: new ArmorClass(15, 0, false),
            isPlayerCharacter: true,
            level: 5,
            expectedDpr: 18.2,
            archetype: "Ranger Ranged");

        var orc = new Creature(
            name: "Orco Guerrero",
            size: CreatureSize.Medium,
            type: CreatureType.Humanoid,
            alignment: "Chaotic Evil",
            challengeRating: 1,
            hitPoints: new HitPoints(30),
            armorClass: new ArmorClass(13, 0, false));

        var orcWeapon = new Weapon("Gran Hacha", "1d12", DamageType.Slashing);
        var rng = new DefaultRandomProvider();
        var diceRoller = new DiceRoller(rng);
        var simulator = new CombatSimulatorService(rng, diceRoller);

        // Act
        var fastDuel = simulator.SimulateFastDuel(hero, null, orc, orcWeapon);

        // Assert
        fastDuel.Rounds.Should().BeGreaterThan(0);
        fastDuel.DamageDealtA.Should().BeGreaterThan(0);
    }
}
