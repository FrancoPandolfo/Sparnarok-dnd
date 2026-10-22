using FluentAssertions;
using Sparnarok.Api.Application.Interfaces;
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
/// Pruebas unitarias para la agregación matemática, cálculo de DPR y concurrencia del servicio de Monte Carlo.
/// </summary>
public class MontecarloSimulationServiceTests
{
    private class FakeCombatSimulator : ICombatSimulatorService
    {
        private readonly Func<FastDuelResult> _resultFactory;

        public FakeCombatSimulator(Func<FastDuelResult> resultFactory)
        {
            _resultFactory = resultFactory;
        }

        public FastDuelResult SimulateFastDuel(Creature combatant1, Weapon? weapon1, Creature combatant2, Weapon? weapon2) =>
            _resultFactory();

        public CombatSimulationResult SimulateDuel(Creature combatant1, Weapon? weapon1, Creature combatant2, Weapon? weapon2) =>
            throw new NotImplementedException();

        public FastEncounterResult SimulateFastEncounter(
            IReadOnlyList<(Creature Creature, Weapon? Weapon)> teamA,
            IReadOnlyList<(Creature Creature, Weapon? Weapon)> teamB,
            TargetingStrategy targetingStrategy = TargetingStrategy.LowestHp)
        {
            var duel = _resultFactory();
            CombatWinner winner = CombatWinner.Draw;
            if (duel.WinnerId == teamA[0].Creature.Id) winner = CombatWinner.TeamA;
            else if (duel.WinnerId == teamB[0].Creature.Id) winner = CombatWinner.TeamB;
            int survivors = winner == CombatWinner.TeamA ? teamA.Count : 0;
            return new FastEncounterResult(winner, duel.Rounds, survivors, duel.DamageDealtA, duel.DamageDealtB);
        }

        public CombatSimulationResult SimulateEncounter(
            IReadOnlyList<(Creature Creature, Weapon? Weapon)> teamA,
            IReadOnlyList<(Creature Creature, Weapon? Weapon)> teamB,
            TargetingStrategy targetingStrategy = TargetingStrategy.LowestHp) =>
            throw new NotImplementedException();

        public Task<CombatSimulationResult> SimulateDuelAsync(Guid creature1Id, Guid? weapon1Id, Guid creature2Id, Guid? weapon2Id, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }


    private static Creature CreateCreature(string name, int hp, int ac)
    {
        return new Creature(
            name: name,
            size: CreatureSize.Medium,
            type: CreatureType.Humanoid,
            alignment: "Neutral",
            challengeRating: 1.0,
            speedFeet: 30,
            abilityScores: new AbilityScores(16, 14, 14, 10, 10, 10),
            hitPoints: new HitPoints(hp),
            armorClass: new ArmorClass(ac, 0, allowsDexterityModifier: false));
    }

    private static Weapon CreateWeapon(string name, string damage) =>
        new(name, damage, DamageType.Slashing);

    [Fact]
    public void Simulate_WithMockedCombatEngine_AggregatesWinsAndPercentagesCorrectly()
    {
        // Arrange
        var combatantA = CreateCreature("Hero", hp: 30, ac: 15);
        var weaponA = CreateWeapon("Greatsword", "2d6");
        var combatantB = CreateCreature("Villain", hp: 30, ac: 15);
        var weaponB = CreateWeapon("Warhammer", "1d8");

        int callCount = 0;

        // Simular 70 victorias de A y 30 victorias de B en 100 iteraciones
        var fakeSimulator = new FakeCombatSimulator(() =>
        {
            var count = Interlocked.Increment(ref callCount);
            var isAWin = count <= 70;
            return new FastDuelResult(
                WinnerId: isAWin ? combatantA.Id : combatantB.Id,
                Rounds: 3,
                DamageDealtA: isAWin ? 30 : 15,
                DamageDealtB: isAWin ? 10 : 30);
        });

        var service = new MontecarloSimulationService(fakeSimulator);

        // Act
        var result = service.Simulate(combatantA, weaponA, combatantB, weaponB, iterations: 100, fakeSimulator);

        // Assert
        result.TotalIterations.Should().Be(100);
        result.CombatantAWins.Should().Be(70);
        result.CombatantBWins.Should().Be(30);
        result.Draws.Should().Be(0);
        result.CombatantAWinRate.Should().Be(70.0);
        result.CombatantBWinRate.Should().Be(30.0);
        result.DrawRate.Should().Be(0.0);
        result.AverageRounds.Should().Be(3.0);
    }

    [Fact]
    public void Simulate_CalculatesAverageRoundsAndDprAccurately()
    {
        // Arrange
        var combatantA = CreateCreature("Archer", hp: 20, ac: 14);
        var weaponA = CreateWeapon("Longbow", "1d8");
        var combatantB = CreateCreature("Knight", hp: 25, ac: 16);
        var weaponB = CreateWeapon("Halberd", "1d10");

        // Cada combate dura exactamente 4 rondas. A inflige 20 de daño total (5 DPR), B inflige 12 de daño total (3 DPR).
        var fakeSimulator = new FakeCombatSimulator(() => new FastDuelResult(
            WinnerId: combatantA.Id,
            Rounds: 4,
            DamageDealtA: 20,
            DamageDealtB: 12));

        var service = new MontecarloSimulationService(fakeSimulator);

        // Act
        var result = service.Simulate(combatantA, weaponA, combatantB, weaponB, iterations: 50, fakeSimulator);

        // Assert
        result.TotalIterations.Should().Be(50);
        result.AverageRounds.Should().Be(4.0);
        result.CombatantAAverageDpr.Should().Be(5.0); // 20 daño / 4 rondas
        result.CombatantBAverageDpr.Should().Be(3.0); // 12 daño / 4 rondas
    }

    [Fact]
    public void Simulate_WithDraws_CalculatesDrawRateCorrectly()
    {
        // Arrange
        var combatantA = CreateCreature("Fighter A", hp: 100, ac: 25);
        var weaponA = CreateWeapon("Dagger", "1d4");
        var combatantB = CreateCreature("Fighter B", hp: 100, ac: 25);
        var weaponB = CreateWeapon("Dagger", "1d4");

        int callCount = 0;

        // 40 victorias A, 40 victorias B, 20 empates
        var fakeSimulator = new FakeCombatSimulator(() =>
        {
            var count = Interlocked.Increment(ref callCount);
            if (count <= 40) return new FastDuelResult(combatantA.Id, 5, 20, 10);
            if (count <= 80) return new FastDuelResult(combatantB.Id, 5, 10, 20);
            return new FastDuelResult(null, 100, 5, 5); // Empate
        });

        var service = new MontecarloSimulationService(fakeSimulator);

        // Act
        var result = service.Simulate(combatantA, weaponA, combatantB, weaponB, iterations: 100, fakeSimulator);

        // Assert
        result.CombatantAWins.Should().Be(40);
        result.CombatantBWins.Should().Be(40);
        result.Draws.Should().Be(20);
        result.CombatantAWinRate.Should().Be(40.0);
        result.CombatantBWinRate.Should().Be(40.0);
        result.DrawRate.Should().Be(20.0);
    }

    [Fact]
    public void Simulate_WithRealConcurrentEngine_ExecutesThousandsOfDuelsWithoutErrors()
    {
        // Arrange: Duelo real con el motor matemático y RNG real en 1,000 iteraciones concurrentes
        var rng = new DefaultRandomProvider();
        var diceRoller = new DiceRoller(rng);
        var simulator = new CombatSimulatorService(rng, diceRoller);
        var service = new MontecarloSimulationService(simulator);

        var barbarian = CreateCreature("Berserker", hp: 35, ac: 13);
        var berserkerAxe = CreateWeapon("Greataxe", "1d12");

        var paladin = CreateCreature("Paladin", hp: 30, ac: 16);
        var paladinSword = CreateWeapon("Longsword", "1d8");

        // Act: 1,000 iteraciones en paralelo
        var result = service.Simulate(barbarian, berserkerAxe, paladin, paladinSword, iterations: 1000);

        // Assert
        result.TotalIterations.Should().Be(1000);
        (result.CombatantAWins + result.CombatantBWins + result.Draws).Should().Be(1000);
        (result.CombatantAWinRate + result.CombatantBWinRate + result.DrawRate).Should().BeInRange(99.9, 100.1);
        result.AverageRounds.Should().BeGreaterThan(0);
        result.CombatantAAverageDpr.Should().BeGreaterThan(0);
        result.CombatantBAverageDpr.Should().BeGreaterThan(0);
        result.ExecutionTimeMs.Should().BeGreaterThanOrEqualTo(0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(50001)]
    public void Simulate_WithInvalidIterationCount_ThrowsArgumentOutOfRangeException(int iterations)
    {
        var fakeSimulator = new FakeCombatSimulator(() => default);
        var service = new MontecarloSimulationService(fakeSimulator);
        var creature = CreateCreature("Test", hp: 10, ac: 10);
        var weapon = CreateWeapon("Sword", "1d8");

        var act = () => service.Simulate(creature, weapon, creature, weapon, iterations);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
