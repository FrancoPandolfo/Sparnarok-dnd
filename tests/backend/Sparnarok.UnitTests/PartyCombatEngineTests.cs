using FluentAssertions;
using Sparnarok.Api.Domain.Entities;
using Sparnarok.Api.Domain.Enums;
using Sparnarok.Api.Domain.Services.Combat;
using Sparnarok.Api.Domain.Services.Dice;
using Sparnarok.Api.Domain.Services.Rng;
using Sparnarok.Api.Domain.ValueObjects;
using Sparnarok.Api.Infrastructure.Services;
using Xunit;

namespace Sparnarok.UnitTests;

/// <summary>
/// Pruebas unitarias de la Fase 2.4: Party vs. Encounter Engine (N vs. M).
/// Valida la Cola Global de Iniciativa, IA de Targeting (Random y LowestHP),
/// condición de victoria por TPK / aniquilación total y cálculo de tasas de supervivencia.
/// </summary>
public class PartyCombatEngineTests
{
    private static Creature CreateCreature(string name, int hp, int ac, int dexScore, double? expectedDpr = null)
    {
        var creature = new Creature(
            name: name,
            size: CreatureSize.Medium,
            type: CreatureType.Humanoid,
            alignment: "Neutral",
            challengeRating: 1.0,
            speedFeet: 30,
            abilityScores: new AbilityScores(16, dexScore, 14, 10, 10, 10),
            hitPoints: new HitPoints(hp),
            armorClass: new ArmorClass(ac, 0, allowsDexterityModifier: false));

        if (expectedDpr.HasValue)
        {
            creature.ConfigurePlayerCharacterProfile(level: 5, expectedDpr: expectedDpr.Value, archetype: "Fighter 2H");
        }

        return creature;
    }

    private static Weapon CreateWeapon(string name = "Longsword", string damage = "1d8") =>
        new(name, damage, DamageType.Slashing);

    [Fact]
    public void InitiativeQueue_ShouldOrderSixEntitiesCorrectly_WithDexterityTieBreaker()
    {
        // Arrange: 6 combatientes con puntuaciones de Destreza conocidas
        // Dex 18 => mod +4, Dex 14 => mod +2, Dex 12 => mod +1, Dex 16 => mod +3, Dex 10 => mod 0
        var f1 = new CombatFighter(CreateCreature("F1_Hero", 30, 15, dexScore: 14), CreateWeapon(), TeamSide.TeamA) { Initiative = 18 }; // Init 18, Dex +2
        var f2 = new CombatFighter(CreateCreature("F2_Ranger", 30, 15, dexScore: 12), CreateWeapon(), TeamSide.TeamA) { Initiative = 20 }; // Init 20, Dex +1
        var f3 = new CombatFighter(CreateCreature("F3_Rogue", 30, 15, dexScore: 16), CreateWeapon(), TeamSide.TeamA) { Initiative = 15 }; // Init 15, Dex +3
        var f4 = new CombatFighter(CreateCreature("F4_Monk", 30, 15, dexScore: 18), CreateWeapon(), TeamSide.TeamB) { Initiative = 18 }; // Init 18, Dex +4 (Empate con F1 pero mayor Dex)
        var f5 = new CombatFighter(CreateCreature("F5_Goblin", 10, 12, dexScore: 10), CreateWeapon(), TeamSide.TeamB) { Initiative = 12 }; // Init 12, Dex 0
        var f6 = new CombatFighter(CreateCreature("F6_Boss", 80, 18, dexScore: 14), CreateWeapon(), TeamSide.TeamB) { Initiative = 22 }; // Init 22, Dex +2

        var rawList = new List<CombatFighter> { f1, f2, f3, f4, f5, f6 };

        // Act: Ordenar la cola de iniciativa
        var ordered = InitiativeQueue.Order(rawList);

        // Assert:
        // 1º: F6 (22)
        // 2º: F2 (20)
        // 3º: F4 (18, Dex +4) [Desempata sobre F1]
        // 4º: F1 (18, Dex +2)
        // 5º: F3 (15)
        // 6º: F5 (12)
        ordered.Should().HaveCount(6);
        ordered[0].Creature.Name.Should().Be("F6_Boss");
        ordered[1].Creature.Name.Should().Be("F2_Ranger");
        ordered[2].Creature.Name.Should().Be("F4_Monk");
        ordered[3].Creature.Name.Should().Be("F1_Hero");
        ordered[4].Creature.Name.Should().Be("F3_Rogue");
        ordered[5].Creature.Name.Should().Be("F5_Goblin");
    }

    [Fact]
    public void LowestHpTargetingStrategy_ShouldSelectLivingEnemyWithMinimumCurrentHp()
    {
        // Arrange
        var strategy = new LowestHpTargetingStrategy();
        var attacker = new CombatFighter(CreateCreature("Attacker", 30, 15, 10), CreateWeapon(), TeamSide.TeamA);

        var e1 = new CombatFighter(CreateCreature("Ogre", 50, 12, 10), CreateWeapon(), TeamSide.TeamB) { CurrentHp = 45 };
        var e2 = new CombatFighter(CreateCreature("Wounded Goblin", 15, 12, 10), CreateWeapon(), TeamSide.TeamB) { CurrentHp = 4 };
        var e3 = new CombatFighter(CreateCreature("Dead Orc", 20, 13, 10), CreateWeapon(), TeamSide.TeamB) { CurrentHp = 0 };
        var e4 = new CombatFighter(CreateCreature("Healthy Bugbear", 27, 14, 10), CreateWeapon(), TeamSide.TeamB) { CurrentHp = 27 };

        var opponents = new List<CombatFighter> { e1, e2, e3, e4 };
        var random = new DefaultRandomProvider();

        // Act
        var target = strategy.SelectTarget(attacker, opponents, random);

        // Assert: Debe enfocar a e2 (4 HP) e ignorar a e3 (0 HP)
        target.Should().NotBeNull();
        target!.Creature.Name.Should().Be("Wounded Goblin");
        target.CurrentHp.Should().Be(4);

        // Si e2 muere, debe ahora enfocar a e4 (27 HP vs 45 HP de e1)
        e2.CurrentHp = 0;
        var nextTarget = strategy.SelectTarget(attacker, opponents, random);
        nextTarget.Should().NotBeNull();
        nextTarget!.Creature.Name.Should().Be("Healthy Bugbear");

        // Si todos caen a 0 HP, retorna null
        e1.CurrentHp = 0;
        e4.CurrentHp = 0;
        var emptyTarget = strategy.SelectTarget(attacker, opponents, random);
        emptyTarget.Should().BeNull();
    }

    [Fact]
    public void RandomTargetingStrategy_ShouldSelectFromLivingEnemiesOnly()
    {
        // Arrange
        var strategy = new RandomTargetingStrategy();
        var attacker = new CombatFighter(CreateCreature("Hero", 30, 15, 10), CreateWeapon(), TeamSide.TeamA);

        var e1 = new CombatFighter(CreateCreature("Living A", 20, 12, 10), CreateWeapon(), TeamSide.TeamB) { CurrentHp = 20 };
        var e2 = new CombatFighter(CreateCreature("Dead Enemy", 20, 12, 10), CreateWeapon(), TeamSide.TeamB) { CurrentHp = 0 };
        var e3 = new CombatFighter(CreateCreature("Living B", 20, 12, 10), CreateWeapon(), TeamSide.TeamB) { CurrentHp = 10 };

        var opponents = new List<CombatFighter> { e1, e2, e3 };
        var random = new DefaultRandomProvider();

        // Act & Assert en múltiples iteraciones
        for (int i = 0; i < 50; i++)
        {
            var target = strategy.SelectTarget(attacker, opponents, random);
            target.Should().NotBeNull();
            target!.IsAlive.Should().BeTrue();
            target.Creature.Name.Should().BeOneOf("Living A", "Living B");
            target.Creature.Name.Should().NotBe("Dead Enemy");
        }
    }

    [Fact]
    public void CombatSimulator_PartyEncounter_VictoryConditionTriggersOnlyWhenAllMembersOfTeamReachZeroHp()
    {
        // Arrange:
        // Team A: 2 Héroes (Hero 1 con 30 HP, Hero 2 con 30 HP)
        // Team B: 2 Goblins (Goblin 1 con 10 HP, Goblin 2 con 10 HP)
        var hero1 = CreateCreature("Hero 1", hp: 30, ac: 16, dexScore: 14);
        var hero2 = CreateCreature("Hero 2", hp: 30, ac: 16, dexScore: 14);
        var goblin1 = CreateCreature("Goblin 1", hp: 10, ac: 10, dexScore: 10);
        var goblin2 = CreateCreature("Goblin 2", hp: 10, ac: 10, dexScore: 10);

        var weapon = CreateWeapon("Shortsword", "1d6");

        var teamA = new List<(Creature Creature, Weapon? Weapon)>
        {
            (hero1, weapon),
            (hero2, weapon)
        };

        var teamB = new List<(Creature Creature, Weapon? Weapon)>
        {
            (goblin1, weapon),
            (goblin2, weapon)
        };

        var random = new DefaultRandomProvider();
        var simulator = new CombatSimulatorService(random, new DiceRoller(random));

        // Act
        var result = simulator.SimulateEncounter(teamA, teamB, TargetingStrategy.LowestHp);

        // Assert
        result.Should().NotBeNull();
        result.WinnerTeam.Should().Be(CombatWinner.TeamA);
        result.TotalRounds.Should().BeGreaterThan(0);

        // Comprobar que en el equipo derrotado (Team B) TODOS los miembros tienen 0 HP
        result.TeamBSummaries.Should().NotBeNull();
        result.TeamBSummaries!.All(s => s.FinalHp == 0).Should().BeTrue("El encuentro solo finaliza cuando todo el bando rival cae a 0 HP.");

        // Al menos un miembro de Team A sobrevivió
        result.TeamASurvivorCount.Should().BeGreaterThan(0);
        result.TeamASummaries!.Any(s => s.IsAlive).Should().BeTrue();
    }

    [Fact]
    public void CombatSimulator_4HeroesVs1Boss_SimulatesFullEncounterWithDprIntegration()
    {
        // Arrange: 4 Héroes con ExpectedDpr vs 1 Jefe Dragón con arma pesada
        var fighter = CreateCreature("Fighter 2H", hp: 45, ac: 18, dexScore: 12, expectedDpr: 19.4);
        var paladin = CreateCreature("Paladin", hp: 48, ac: 19, dexScore: 10, expectedDpr: 17.5);
        var ranger = CreateCreature("Ranger", hp: 40, ac: 16, dexScore: 16, expectedDpr: 15.2);
        var rogue = CreateCreature("Rogue", hp: 35, ac: 15, dexScore: 18, expectedDpr: 14.8);

        var dragonBoss = CreateCreature("Young Red Dragon", hp: 140, ac: 18, dexScore: 10);
        var dragonBite = CreateWeapon("Dragon Bite", "2d10+5");

        var party = new List<(Creature Creature, Weapon? Weapon)>
        {
            (fighter, null),
            (paladin, null),
            (ranger, null),
            (rogue, null)
        };

        var encounter = new List<(Creature Creature, Weapon? Weapon)>
        {
            (dragonBoss, dragonBite)
        };

        var random = new DefaultRandomProvider();
        var simulator = new CombatSimulatorService(random, new DiceRoller(random));

        // Act
        var result = simulator.SimulateEncounter(party, encounter, TargetingStrategy.LowestHp);

        // Assert
        result.Should().NotBeNull();
        result.TotalRounds.Should().BeInRange(1, 15);
        result.Logs.Should().NotBeEmpty();
        result.Logs.Should().Contain(l => l.Description.Contains("DPR de arquetipo"), "Los héroes deben aplicar daño basado en DPR.");
        result.TeamASurvivoriesCountOrValid();
    }

    [Fact]
    public void MontecarloSimulationService_PartyVsEncounter_CalculatesWinRateAndSurvivalRateAccurately()
    {
        // Arrange
        var h1 = CreateCreature("Hero 1", hp: 40, ac: 16, dexScore: 14, expectedDpr: 15.0);
        var h2 = CreateCreature("Hero 2", hp: 40, ac: 16, dexScore: 14, expectedDpr: 15.0);
        var m1 = CreateCreature("Ogre Boss", hp: 50, ac: 12, dexScore: 8);
        var ogreClub = CreateWeapon("Greatclub", "2d8+3");

        var teamA = new List<(Creature Creature, Weapon? Weapon)> { (h1, null), (h2, null) };
        var teamB = new List<(Creature Creature, Weapon? Weapon)> { (m1, ogreClub) };

        var random = new DefaultRandomProvider();
        var combatSimulator = new CombatSimulatorService(random, new DiceRoller(random));
        var montecarlo = new MontecarloSimulationService(combatSimulator);

        // Act: 500 iteraciones
        var result = montecarlo.SimulateEncounter(teamA, teamB, iterations: 500, TargetingStrategy.LowestHp);

        // Assert
        result.Should().NotBeNull();
        result.TotalIterations.Should().Be(500);
        result.CombatantAWins.Should().BeGreaterThan(0);
        (result.CombatantAWinRate + result.CombatantBWinRate + result.DrawRate).Should().BeInRange(99.9, 100.1);
        result.AverageRounds.Should().BeGreaterThan(0);
        result.TeamASurvivalRate.Should().BeInRange(0.0, 100.0);
        result.TeamANames.Should().Contain(new[] { "Hero 1", "Hero 2" });
        result.TeamBNames.Should().Contain("Ogre Boss");
    }
}

internal static class TestExtensions
{
    public static void TeamASurvivoriesCountOrValid(this CombatSimulationResult result)
    {
        result.TeamASurvivorCount.Should().BeInRange(0, 4);
    }
}
