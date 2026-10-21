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
/// Suite de pruebas unitarias matemáticas para el motor de combate de D&amp;D 5e (2024).
/// Emplea <see cref="DeterministicRandomProvider"/> para asegurar determinismo matemático en impactos críticos,
/// fallos automáticos, umbrales de CA y condiciones de victoria.
/// </summary>
public class CombatSimulatorServiceTests
{
    private static Creature CreateWarrior(
        string name,
        int hp,
        int ac,
        int str = 16,
        int dex = 14,
        double cr = 1.0)
    {
        return new Creature(
            name: name,
            size: CreatureSize.Medium,
            type: CreatureType.Humanoid,
            alignment: "Neutral",
            challengeRating: cr,
            speedFeet: 30,
            abilityScores: new AbilityScores(str, dex, 14, 10, 10, 10),
            hitPoints: new HitPoints(hp),
            armorClass: new ArmorClass(ac, 0, allowsDexterityModifier: false));
    }

    private static Weapon CreateLongsword() =>
        new("Longsword", "1d8", DamageType.Slashing, properties: "Versatile (1d10)");

    private static Weapon CreateRapier() =>
        new("Rapier", "1d8", DamageType.Piercing, properties: "Finesse");

    [Fact]
    public void SimulateDuel_WhenAttackerRollsNatural20_ScoresCriticalHitAndDuplicatesDiceDamage()
    {
        // Arrange
        // Fighter 1 (Barbarian): HP = 30, AC = 10, STR = 16 (+3 mod)
        // Fighter 2 (Knight): HP = 20, AC = 25 (CA imposible de superar normalmente)
        var barbarian = CreateWarrior("Barbarian", hp: 30, ac: 10, str: 16, dex: 12);
        var knight = CreateWarrior("Knight", hp: 20, ac: 25, str: 10, dex: 10);

        var weapon = CreateLongsword();

        // Secuencia Determinista:
        // 1. Iniciativa Barbarian: 18 (+1 Dex = 19)
        // 2. Iniciativa Knight: 10 (+0 Dex = 10) -> Barbarian ataca primero
        // 3. Ronda 1 - Barbarian attack roll: 20 (¡20 NATURAL!)
        // 4. Daño de Longsword en crítico: tira 2 dados d8: [8, 8] -> 16 + 3 (STR) = 19 daño
        // 5. Ronda 1 - Knight attack roll: 12 (12 + 0 vs CA 10 -> Impacto)
        // 6. Daño Knight: [4] -> 4 daño
        // 7. Ronda 2 - Barbarian attack: 15 (15 + 5 = 20 vs CA 25 -> Falla)
        var rng = new DeterministicRandomProvider(new[] { 18, 10, 20, 8, 8, 12, 4, 15 });
        var diceRoller = new DiceRoller(rng);
        var simulator = new CombatSimulatorService(rng, diceRoller);

        // Act
        var result = simulator.SimulateDuel(barbarian, weapon, knight, weapon);

        // Assert
        result.Logs.Should().NotBeEmpty();
        var critLog = result.Logs.First();

        critLog.AttackerName.Should().Be("Barbarian");
        critLog.D20Roll.Should().Be(20);
        critLog.IsNatural20.Should().BeTrue();
        critLog.IsHit.Should().BeTrue();
        critLog.DamageDealt.Should().Be(19); // (8 + 8) + 3
        critLog.DefenderRemainingHp.Should().Be(1); // 20 - 19
        critLog.Description.Should().Contain("20 NATURAL");
    }

    [Fact]
    public void SimulateDuel_WhenAttackerRollsNatural1_FailsAutomaticallyRegardlessOfBonuses()
    {
        // Arrange:
        // Attacker con bono descomunal (+15) vs Defensor con CA muy baja (10)
        // 1 + 15 = 16 supera la CA 10, pero por regla de 1 Natural DEBE fallar automáticamente
        var attacker = CreateWarrior("Master", hp: 50, ac: 10, str: 20, dex: 20, cr: 20); // Prof = +6, STR = +5
        var defender = CreateWarrior("Peasant", hp: 10, ac: 10);

        var weapon = CreateLongsword();

        // Secuencia Determinista:
        // 1. Iniciativa Master: 20
        // 2. Iniciativa Peasant: 5 -> Master va primero
        // 3. Ataque Master: 1 (¡1 NATURAL! -> Fallo automático)
        // 4. Ataque Peasant: 2 (Falla)
        // 5. Ataque Master: 20 (Crítico final para terminar)
        // 6. Daño: 8, 8 -> Muere Peasant
        var rng = new DeterministicRandomProvider(new[] { 20, 5, 1, 2, 20, 8, 8 });
        var diceRoller = new DiceRoller(rng);
        var simulator = new CombatSimulatorService(rng, diceRoller);

        // Act
        var result = simulator.SimulateDuel(attacker, weapon, defender, weapon);

        // Assert
        var firstAttack = result.Logs.First();
        firstAttack.D20Roll.Should().Be(1);
        firstAttack.IsNatural1.Should().BeTrue();
        firstAttack.IsHit.Should().BeFalse();
        firstAttack.DamageDealt.Should().Be(0);
        firstAttack.DefenderRemainingHp.Should().Be(10);
        firstAttack.Description.Should().Contain("1 NATURAL");
    }

    [Fact]
    public void SimulateDuel_WhenAttackRollBelowArmorClass_MissesAndDealsZeroDamage()
    {
        // Arrange:
        // Attacker: STR = 10 (+0), Prof = +2. Ataque total = d20 + 2
        // Defender: CA = 18
        var attacker = CreateWarrior("Goblin", hp: 20, ac: 12, str: 10, dex: 14);
        var defender = CreateWarrior("Ironclad", hp: 30, ac: 18, str: 16, dex: 10);

        var weapon = CreateLongsword();

        // Secuencia:
        // 1. Init Goblin: 15 (+2 = 17)
        // 2. Init Ironclad: 10 (+0 = 10)
        // 3. Ataque Goblin: saca 10 (10 + 2 = 12 < 18 CA -> Falla)
        // 4. Ataque Ironclad: saca 20 (Crítico)
        // 5. Daño Ironclad: [8, 8] + 3 = 19
        // 6. Ataque Goblin: 5 (Falla)
        // 7. Ataque Ironclad: 18 (Impacta) -> Daño [8] + 3 = 11 -> Goblin eliminado
        var rng = new DeterministicRandomProvider(new[] { 15, 10, 10, 20, 8, 8, 5, 18, 8 });
        var diceRoller = new DiceRoller(rng);
        var simulator = new CombatSimulatorService(rng, diceRoller);

        // Act
        var result = simulator.SimulateDuel(attacker, weapon, defender, weapon);

        // Assert
        var missLog = result.Logs.First();
        missLog.TotalAttackRoll.Should().Be(12);
        missLog.TargetArmorClass.Should().Be(18);
        missLog.IsHit.Should().BeFalse();
        missLog.DamageDealt.Should().Be(0);
        missLog.DefenderRemainingHp.Should().Be(30);
    }

    [Fact]
    public void SimulateDuel_FinesseWeapon_ChoosesHighestModifierBetweenStrengthAndDexterity()
    {
        // Arrange:
        // Rogue: STR = 10 (+0), DEX = 18 (+4), Prof = +2.
        // Arma: Rapier (Finesse). Debe usar Destreza (+4) en ataque (+6 total) y daño (+4).
        var rogue = CreateWarrior("Rogue", hp: 20, ac: 14, str: 10, dex: 18);
        var target = CreateWarrior("Target", hp: 10, ac: 10);

        var rapier = CreateRapier();

        // Secuencia:
        // 1. Init Rogue: 15 (+4 = 19)
        // 2. Init Target: 10
        // 3. Ataque Rogue: saca 10 -> (10 + 6 bono = 16 vs CA 10) -> IMPACTA
        // 4. Daño Rapier: saca 6 -> 6 + 4 (DEX mod) = 10 daño -> Target eliminado
        var rng = new DeterministicRandomProvider(new[] { 15, 10, 10, 6 });
        var diceRoller = new DiceRoller(rng);
        var simulator = new CombatSimulatorService(rng, diceRoller);

        // Act
        var result = simulator.SimulateDuel(rogue, rapier, target, rapier);

        // Assert
        var attackLog = result.Logs.First();
        attackLog.AttackModifier.Should().Be(6); // 4 (Dex) + 2 (Prof)
        attackLog.TotalAttackRoll.Should().Be(16);
        attackLog.DamageDealt.Should().Be(10); // 6 + 4 (Dex)
        result.WinnerName.Should().Be("Rogue");
        result.WinnerId.Should().Be(rogue.Id);
    }

    [Fact]
    public void SimulateDuel_DeclaresWinnerImmediatelyWhenHpReachesZero()
    {
        // Arrange:
        // Fighter A (10 HP) vs Fighter B (10 HP)
        var fighterA = CreateWarrior("Gladiator A", hp: 10, ac: 10, str: 16);
        var fighterB = CreateWarrior("Gladiator B", hp: 10, ac: 10, str: 16);

        var weapon = CreateLongsword();

        // Secuencia:
        // 1. Init A: 20 -> A va primero
        // 2. Init B: 10
        // 3. Ataque A: saca 15 (15 + 5 = 20 vs CA 10 -> IMPACTO)
        // 4. Daño A: saca 7 + 3 (STR) = 10 daño -> Fighter B llega a 0 HP
        var rng = new DeterministicRandomProvider(new[] { 20, 10, 15, 7 });
        var diceRoller = new DiceRoller(rng);
        var simulator = new CombatSimulatorService(rng, diceRoller);

        // Act
        var result = simulator.SimulateDuel(fighterA, weapon, fighterB, weapon);

        // Assert
        result.TotalRounds.Should().Be(1);
        result.WinnerName.Should().Be("Gladiator A");
        result.WinnerId.Should().Be(fighterA.Id);
        result.Combatant1.FinalHp.Should().Be(10);
        result.Combatant1.IsAlive.Should().BeTrue();
        result.Combatant2.FinalHp.Should().Be(0);
        result.Combatant2.IsAlive.Should().BeFalse();

        // Solo debe haber 1 ataque registrado, ya que B cayó antes de su turno
        result.Logs.Should().HaveCount(1);
    }

    [Fact]
    public void SimulateDuel_WithNullParameters_ThrowsArgumentNullException()
    {
        var creature = CreateWarrior("Test", hp: 10, ac: 10);
        var weapon = CreateLongsword();
        var simulator = new CombatSimulatorService(new DefaultRandomProvider(), new DiceRoller(new DefaultRandomProvider()));

        var act1 = () => simulator.SimulateDuel(null!, weapon, creature, weapon);
        var act2 = () => simulator.SimulateDuel(creature, null!, creature, weapon);
        var act3 = () => simulator.SimulateDuel(creature, weapon, null!, weapon);
        var act4 = () => simulator.SimulateDuel(creature, weapon, creature, null!);

        act1.Should().Throw<ArgumentNullException>();
        act2.Should().Throw<ArgumentNullException>();
        act3.Should().Throw<ArgumentNullException>();
        act4.Should().Throw<ArgumentNullException>();
    }
}
