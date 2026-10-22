using Microsoft.EntityFrameworkCore;
using Sparnarok.Api.Application.Interfaces;
using Sparnarok.Api.Domain.Entities;
using Sparnarok.Api.Domain.Enums;
using Sparnarok.Api.Domain.Services.Combat;
using Sparnarok.Api.Domain.Services.Dice;
using Sparnarok.Api.Domain.Services.Rng;
using Sparnarok.Api.Infrastructure.Persistence;

namespace Sparnarok.Api.Infrastructure.Services;

/// <summary>
/// Implementación del servicio de simulación de combate de D&amp;D 5e (2024).
/// Orquesta la resolución estocástica y determinista de iniciativa global,
/// ataques d20 contra CA, daño directo por DPR de arquetipo, impactos críticos,
/// targeting AI heurístico y bucle de turnos en encuentros individuales (1v1) y grupales (N vs M).
/// </summary>
public class CombatSimulatorService : ICombatSimulatorService
{
    private readonly IRandomProvider _random;
    private readonly IDiceRoller _diceRoller;
    private readonly SparnarokDbContext? _context;
    private const int MaxRounds = 100;

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="CombatSimulatorService"/>.
    /// </summary>
    /// <param name="random">Proveedor de números aleatorios (producción o determinista para tests).</param>
    /// <param name="diceRoller">Servicio evaluador de fórmulas de dados.</param>
    /// <param name="context">Contexto de base de datos relacional (opcional para ejecución pura en memoria).</param>
    public CombatSimulatorService(
        IRandomProvider random,
        IDiceRoller diceRoller,
        SparnarokDbContext? context = null)
    {
        _random = random ?? throw new ArgumentNullException(nameof(random));
        _diceRoller = diceRoller ?? throw new ArgumentNullException(nameof(diceRoller));
        _context = context;
    }

    /// <inheritdoc />
    public CombatSimulationResult SimulateEncounter(
        IReadOnlyList<(Creature Creature, Weapon? Weapon)> teamA,
        IReadOnlyList<(Creature Creature, Weapon? Weapon)> teamB,
        TargetingStrategy targetingStrategy = TargetingStrategy.LowestHp)
    {
        ValidateTeam(teamA, nameof(teamA));
        ValidateTeam(teamB, nameof(teamB));

        var simulationId = Guid.NewGuid();
        var logs = new List<CombatEventLog>();

        // 1. Instanciación de CombatFighters para cada combatiente
        var teamAFighters = teamA.Select(t => new CombatFighter(t.Creature, t.Weapon, TeamSide.TeamA)).ToList();
        var teamBFighters = teamB.Select(t => new CombatFighter(t.Creature, t.Weapon, TeamSide.TeamB)).ToList();

        var allFighters = new List<CombatFighter>(teamAFighters.Count + teamBFighters.Count);
        allFighters.AddRange(teamAFighters);
        allFighters.AddRange(teamBFighters);

        // 2. Tirada de Iniciativa Global y ordenación de la cola de turnos
        var turnOrder = InitiativeQueue.RollAndOrder(allFighters, _random);

        var strategy = TargetingStrategyFactory.GetStrategy(targetingStrategy);
        int currentRound = 0;

        // 3. Bucle de Combate Ronda a Ronda
        while (teamAFighters.Any(f => f.IsAlive) && teamBFighters.Any(f => f.IsAlive) && currentRound < MaxRounds)
        {
            currentRound++;

            foreach (var attacker in turnOrder)
            {
                if (!attacker.IsAlive) continue;

                // Verificar si el equipo enemigo ya fue completamente aniquilado antes del turno
                var livingEnemies = (attacker.Team == TeamSide.TeamA ? teamBFighters : teamAFighters)
                    .Where(e => e.IsAlive)
                    .ToList();

                if (livingEnemies.Count == 0) break;

                // Selección de objetivo mediante IA de Targeting
                var target = strategy.SelectTarget(attacker, livingEnemies, _random);
                if (target == null) break;

                // Ejecución del ataque
                ExecuteAttack(currentRound, attacker, target, logs);

                // Fin inmediato si el equipo opuesto cayó por completo
                if (attacker.Team == TeamSide.TeamA && !teamBFighters.Any(f => f.IsAlive)) break;
                if (attacker.Team == TeamSide.TeamB && !teamAFighters.Any(f => f.IsAlive)) break;
            }
        }

        // 4. Determinación del Vencedor y Resumen
        bool teamAWon = teamAFighters.Any(f => f.IsAlive) && !teamBFighters.Any(f => f.IsAlive);
        bool teamBWon = teamBFighters.Any(f => f.IsAlive) && !teamAFighters.Any(f => f.IsAlive);

        CombatWinner winnerTeam;
        string winnerName;
        Guid? winnerId;

        if (teamAWon)
        {
            winnerTeam = CombatWinner.TeamA;
            winnerName = teamAFighters.Count == 1 ? teamAFighters[0].Creature.Name : "Equipo A (La Party)";
            winnerId = teamAFighters.Count == 1 ? teamAFighters[0].Creature.Id : null;
        }
        else if (teamBWon)
        {
            winnerTeam = CombatWinner.TeamB;
            winnerName = teamBFighters.Count == 1 ? teamBFighters[0].Creature.Name : "Equipo B (Los Monstruos)";
            winnerId = teamBFighters.Count == 1 ? teamBFighters[0].Creature.Id : null;
        }
        else
        {
            winnerTeam = CombatWinner.Draw;
            winnerName = "Empate";
            winnerId = null;
        }

        var summariesA = teamAFighters.Select(f => CreateSummary(f)).ToList();
        var summariesB = teamBFighters.Select(f => CreateSummary(f)).ToList();

        var primaryA = summariesA[0];
        var primaryB = summariesB[0];
        var teamASurvivors = teamAFighters.Count(f => f.IsAlive);

        return new CombatSimulationResult(
            simulationId,
            primaryA,
            primaryB,
            winnerName,
            winnerId,
            currentRound,
            logs.AsReadOnly(),
            DateTime.UtcNow,
            winnerTeam,
            teamASurvivors,
            summariesA.AsReadOnly(),
            summariesB.AsReadOnly());
    }

    /// <inheritdoc />
    public FastEncounterResult SimulateFastEncounter(
        IReadOnlyList<(Creature Creature, Weapon? Weapon)> teamA,
        IReadOnlyList<(Creature Creature, Weapon? Weapon)> teamB,
        TargetingStrategy targetingStrategy = TargetingStrategy.LowestHp)
    {
        ValidateTeam(teamA, nameof(teamA));
        ValidateTeam(teamB, nameof(teamB));

        var teamAFighters = teamA.Select(t => new CombatFighter(t.Creature, t.Weapon, TeamSide.TeamA)).ToList();
        var teamBFighters = teamB.Select(t => new CombatFighter(t.Creature, t.Weapon, TeamSide.TeamB)).ToList();

        var allFighters = new List<CombatFighter>(teamAFighters.Count + teamBFighters.Count);
        allFighters.AddRange(teamAFighters);
        allFighters.AddRange(teamBFighters);

        var turnOrder = InitiativeQueue.RollAndOrder(allFighters, _random);
        var strategy = TargetingStrategyFactory.GetStrategy(targetingStrategy);

        int currentRound = 0;
        int totalDamageA = 0;
        int totalDamageB = 0;

        while (teamAFighters.Any(f => f.IsAlive) && teamBFighters.Any(f => f.IsAlive) && currentRound < MaxRounds)
        {
            currentRound++;

            foreach (var attacker in turnOrder)
            {
                if (!attacker.IsAlive) continue;

                var opponents = attacker.Team == TeamSide.TeamA ? teamBFighters : teamAFighters;
                if (!opponents.Any(o => o.IsAlive)) break;

                var target = strategy.SelectTarget(attacker, opponents, _random);
                if (target == null) break;

                var dmg = ExecuteFastAttack(attacker, target);
                if (attacker.Team == TeamSide.TeamA) totalDamageA += dmg;
                else totalDamageB += dmg;

                if (attacker.Team == TeamSide.TeamA && !teamBFighters.Any(f => f.IsAlive)) break;
                if (attacker.Team == TeamSide.TeamB && !teamAFighters.Any(f => f.IsAlive)) break;
            }
        }

        CombatWinner winner;
        if (teamAFighters.Any(f => f.IsAlive) && !teamBFighters.Any(f => f.IsAlive))
        {
            winner = CombatWinner.TeamA;
        }
        else if (teamBFighters.Any(f => f.IsAlive) && !teamAFighters.Any(f => f.IsAlive))
        {
            winner = CombatWinner.TeamB;
        }
        else
        {
            winner = CombatWinner.Draw;
        }

        int teamASurvivors = teamAFighters.Count(f => f.IsAlive);
        return new FastEncounterResult(winner, currentRound, teamASurvivors, totalDamageA, totalDamageB);
    }

    /// <inheritdoc />
    public CombatSimulationResult SimulateDuel(
        Creature combatant1,
        Weapon? weapon1,
        Creature combatant2,
        Weapon? weapon2)
    {
        return SimulateEncounter(
            new[] { (combatant1, weapon1) },
            new[] { (combatant2, weapon2) },
            TargetingStrategy.LowestHp);
    }

    /// <inheritdoc />
    public FastDuelResult SimulateFastDuel(
        Creature combatant1,
        Weapon? weapon1,
        Creature combatant2,
        Weapon? weapon2)
    {
        var result = SimulateFastEncounter(
            new[] { (combatant1, weapon1) },
            new[] { (combatant2, weapon2) },
            TargetingStrategy.LowestHp);

        Guid? winnerId = result.Winner switch
        {
            CombatWinner.TeamA => combatant1.Id,
            CombatWinner.TeamB => combatant2.Id,
            _ => null
        };

        return new FastDuelResult(winnerId, result.Rounds, result.TeamADamageDealt, result.TeamBDamageDealt);
    }

    /// <inheritdoc />
    public async Task<CombatSimulationResult> SimulateDuelAsync(
        Guid creature1Id,
        Guid? weapon1Id,
        Guid creature2Id,
        Guid? weapon2Id,
        CancellationToken cancellationToken = default)
    {
        if (_context == null)
            throw new InvalidOperationException("El contexto de base de datos no fue proporcionado para operaciones asíncronas de persistencia.");

        var creature1 = await _context.Creatures
            .Include(c => c.EquippedArmor)
            .Include(c => c.EquippedShield)
            .FirstOrDefaultAsync(c => c.Id == creature1Id, cancellationToken)
            ?? throw new KeyNotFoundException($"No se encontró la criatura con ID: {creature1Id}");

        Weapon? weapon1 = null;
        if (weapon1Id.HasValue && weapon1Id.Value != Guid.Empty)
        {
            weapon1 = await _context.Weapons
                .FirstOrDefaultAsync(w => w.Id == weapon1Id.Value, cancellationToken)
                ?? throw new KeyNotFoundException($"No se encontró el arma con ID: {weapon1Id}");
        }
        else if (!creature1.IsPlayerCharacter || !creature1.ExpectedDpr.HasValue || creature1.ExpectedDpr.Value <= 0)
        {
            throw new KeyNotFoundException($"Se requiere un arma para la criatura {creature1.Name} al no tener configurado ExpectedDpr.");
        }

        var creature2 = await _context.Creatures
            .Include(c => c.EquippedArmor)
            .Include(c => c.EquippedShield)
            .FirstOrDefaultAsync(c => c.Id == creature2Id, cancellationToken)
            ?? throw new KeyNotFoundException($"No se encontró la criatura con ID: {creature2Id}");

        Weapon? weapon2 = null;
        if (weapon2Id.HasValue && weapon2Id.Value != Guid.Empty)
        {
            weapon2 = await _context.Weapons
                .FirstOrDefaultAsync(w => w.Id == weapon2Id.Value, cancellationToken)
                ?? throw new KeyNotFoundException($"No se encontró el arma con ID: {weapon2Id}");
        }
        else if (!creature2.IsPlayerCharacter || !creature2.ExpectedDpr.HasValue || creature2.ExpectedDpr.Value <= 0)
        {
            throw new KeyNotFoundException($"Se requiere un arma para la criatura {creature2.Name} al no tener configurado ExpectedDpr.");
        }

        return SimulateDuel(creature1, weapon1, creature2, weapon2);
    }

    private int ExecuteFastAttack(CombatFighter attacker, CombatFighter defender)
    {
        // 1. Flujo alternativo de ExpectedDpr para Personajes de Jugador (PCs)
        if (attacker.Creature.IsPlayerCharacter && attacker.Creature.ExpectedDpr.HasValue && attacker.Creature.ExpectedDpr.Value > 0)
        {
            var baseDpr = attacker.Creature.ExpectedDpr.Value;
            var variancePercent = _random.Next(-15, 16);
            var factor = 1.0 + (variancePercent / 100.0);
            var damage = Math.Max(1, (int)Math.Round(baseDpr * factor, MidpointRounding.AwayFromZero));
            defender.CurrentHp = Math.Max(0, defender.CurrentHp - damage);
            return damage;
        }

        if (attacker.Weapon == null)
            return 0;

        var abilityMod = GetWeaponAbilityModifier(attacker.Creature, attacker.Weapon);
        var attackBonus = abilityMod + attacker.Creature.ProficiencyBonus;

        var d20 = _random.RollD20();
        bool isNatural20 = d20 == 20;
        bool isNatural1 = d20 == 1;

        if (isNatural1) return 0;

        if (isNatural20 || (d20 + attackBonus) >= defender.EffectiveAc)
        {
            var damageResult = _diceRoller.Roll(
                attacker.Weapon.Damage,
                additionalModifier: abilityMod,
                isCritical: isNatural20);

            var damage = damageResult.Total;
            defender.CurrentHp = Math.Max(0, defender.CurrentHp - damage);
            return damage;
        }

        return 0;
    }

    private void ExecuteAttack(
        int round,
        CombatFighter attacker,
        CombatFighter defender,
        List<CombatEventLog> logs)
    {
        if (attacker.Creature.IsPlayerCharacter && attacker.Creature.ExpectedDpr.HasValue && attacker.Creature.ExpectedDpr.Value > 0)
        {
            var baseDpr = attacker.Creature.ExpectedDpr.Value;
            var variancePercent = _random.Next(-15, 16);
            var factor = 1.0 + (variancePercent / 100.0);
            var dprDamage = Math.Max(1, (int)Math.Round(baseDpr * factor, MidpointRounding.AwayFromZero));

            defender.CurrentHp = Math.Max(0, defender.CurrentHp - dprDamage);

            var weaponName = attacker.Weapon?.Name ?? $"DPR Baseline ({attacker.Creature.Archetype ?? "Hero"})";
            var sign = variancePercent >= 0 ? "+" : "";
            var dprDescription = $"{attacker.Creature.Name} ejecuta su turno con DPR de arquetipo ({baseDpr:F1} DPR, varianza {sign}{variancePercent}%): inflige {dprDamage} de daño directo a {defender.Creature.Name}. (HP restantes: {defender.CurrentHp})";

            logs.Add(new CombatEventLog(
                Round: round,
                AttackerName: attacker.Creature.Name,
                DefenderName: defender.Creature.Name,
                WeaponName: weaponName,
                D20Roll: 0,
                AttackModifier: 0,
                TotalAttackRoll: 0,
                TargetArmorClass: defender.EffectiveAc,
                IsNatural20: false,
                IsNatural1: false,
                IsHit: true,
                DamageDealt: dprDamage,
                DefenderRemainingHp: defender.CurrentHp,
                Description: dprDescription));

            return;
        }

        if (attacker.Weapon == null)
            throw new InvalidOperationException($"El combatiente {attacker.Creature.Name} no tiene un arma equipada ni ExpectedDpr configurado.");

        var abilityMod = GetWeaponAbilityModifier(attacker.Creature, attacker.Weapon);
        var attackBonus = abilityMod + attacker.Creature.ProficiencyBonus;

        var d20 = _random.RollD20();
        bool isNatural20 = d20 == 20;
        bool isNatural1 = d20 == 1;

        int totalAttack = d20 + attackBonus;
        int targetAc = defender.EffectiveAc;

        bool isHit;
        if (isNatural20)
        {
            isHit = true;
        }
        else if (isNatural1)
        {
            isHit = false;
        }
        else
        {
            isHit = totalAttack >= targetAc;
        }

        int damageDealt = 0;
        string description;

        if (isHit)
        {
            var damageResult = _diceRoller.Roll(
                attacker.Weapon.Damage,
                additionalModifier: abilityMod,
                isCritical: isNatural20);

            damageDealt = damageResult.Total;
            defender.CurrentHp = Math.Max(0, defender.CurrentHp - damageDealt);

            if (isNatural20)
            {
                description = $"{attacker.Creature.Name} obtiene un ¡20 NATURAL! Impacto crítico con {attacker.Weapon.Name} infligiendo {damageDealt} de daño a {defender.Creature.Name}. (HP restantes: {defender.CurrentHp})";
            }
            else
            {
                description = $"{attacker.Creature.Name} ataca con {attacker.Weapon.Name} ({d20} + {attackBonus} = {totalAttack} vs CA {targetAc}): IMPACTO. Inflige {damageDealt} de daño. (HP restantes: {defender.CurrentHp})";
            }
        }
        else
        {
            if (isNatural1)
            {
                description = $"{attacker.Creature.Name} obtiene un 1 NATURAL con {attacker.Weapon.Name}: FALLO AUTOMÁTICO.";
            }
            else
            {
                description = $"{attacker.Creature.Name} ataca con {attacker.Weapon.Name} ({d20} + {attackBonus} = {totalAttack} vs CA {targetAc}): FALLA.";
            }
        }

        logs.Add(new CombatEventLog(
            Round: round,
            AttackerName: attacker.Creature.Name,
            DefenderName: defender.Creature.Name,
            WeaponName: attacker.Weapon.Name,
            D20Roll: d20,
            AttackModifier: attackBonus,
            TotalAttackRoll: totalAttack,
            TargetArmorClass: targetAc,
            IsNatural20: isNatural20,
            IsNatural1: isNatural1,
            IsHit: isHit,
            DamageDealt: damageDealt,
            DefenderRemainingHp: defender.CurrentHp,
            Description: description));
    }

    private static CombatantSummary CreateSummary(CombatFighter fighter)
    {
        var weaponName = fighter.Weapon?.Name ?? (fighter.Creature.ExpectedDpr.HasValue ? $"DPR Baseline ({fighter.Creature.Archetype ?? "Hero"})" : "Unarmed");
        return new CombatantSummary(
            fighter.Creature.Id,
            fighter.Creature.Name,
            weaponName,
            fighter.Initiative,
            fighter.StartingHp,
            fighter.CurrentHp,
            fighter.IsAlive);
    }

    private static void ValidateTeam(IReadOnlyList<(Creature Creature, Weapon? Weapon)> team, string paramName)
    {
        ArgumentNullException.ThrowIfNull(team, paramName);
        if (team.Count == 0)
        {
            throw new ArgumentException("El equipo debe contener al menos un combatiente.", paramName);
        }

        foreach (var (creature, weapon) in team)
        {
            ArgumentNullException.ThrowIfNull(creature, "Combatiente");
            if (weapon == null && (!creature.IsPlayerCharacter || !creature.ExpectedDpr.HasValue || creature.ExpectedDpr.Value <= 0))
            {
                throw new ArgumentNullException(nameof(weapon), $"El combatiente {creature.Name} requiere un arma al no poseer ExpectedDpr.");
            }
        }
    }

    private static int GetWeaponAbilityModifier(Creature creature, Weapon? weapon)
    {
        var strMod = creature.AbilityScores.GetModifier(AbilityScoreType.Strength);
        var dexMod = creature.AbilityScores.GetModifier(AbilityScoreType.Dexterity);

        if (weapon == null) return strMod;

        var props = weapon.Properties ?? string.Empty;

        if (props.Contains("Finesse", StringComparison.OrdinalIgnoreCase))
        {
            return Math.Max(strMod, dexMod);
        }

        if (props.Contains("Ammunition", StringComparison.OrdinalIgnoreCase) &&
            !props.Contains("Thrown", StringComparison.OrdinalIgnoreCase))
        {
            return dexMod;
        }

        return strMod;
    }
}
