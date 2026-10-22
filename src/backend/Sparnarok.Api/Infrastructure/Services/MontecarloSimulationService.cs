using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Sparnarok.Api.Application.DTOs;
using Sparnarok.Api.Application.Interfaces;
using Sparnarok.Api.Domain.Entities;
using Sparnarok.Api.Domain.Enums;
using Sparnarok.Api.Infrastructure.Persistence;

namespace Sparnarok.Api.Infrastructure.Services;

/// <summary>
/// Servicio de aplicación que implementa la simulación masiva de Monte Carlo mediante paralelismo en memoria,
/// calculando métricas probabilísticas agregadas (Win Rates, promedio de rondas, DPR y Party Survival Rate).
/// Soporta tanto enfrentamientos 1v1 como encuentros grupales Party vs Encounter (N vs M).
/// </summary>
public class MontecarloSimulationService : IMontecarloSimulationService
{
    private readonly ICombatSimulatorService _combatSimulator;
    private readonly SparnarokDbContext? _context;

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="MontecarloSimulationService"/>.
    /// </summary>
    /// <param name="combatSimulator">Motor de combate de D&amp;D 5e.</param>
    /// <param name="context">Contexto relacional EF Core (opcional para pruebas puras de dominio).</param>
    public MontecarloSimulationService(
        ICombatSimulatorService combatSimulator,
        SparnarokDbContext? context = null)
    {
        _combatSimulator = combatSimulator ?? throw new ArgumentNullException(nameof(combatSimulator));
        _context = context;
    }

    /// <inheritdoc />
    public MontecarloResultDto Simulate(
        Creature combatantA,
        Weapon? weaponA,
        Creature combatantB,
        Weapon? weaponB,
        int iterations = 10000,
        ICombatSimulatorService? simulatorOverride = null)
    {
        ArgumentNullException.ThrowIfNull(combatantA);
        ArgumentNullException.ThrowIfNull(combatantB);

        return SimulateEncounter(
            new[] { (combatantA, weaponA) },
            new[] { (combatantB, weaponB) },
            iterations,
            TargetingStrategy.LowestHp,
            simulatorOverride);
    }

    /// <inheritdoc />
    public MontecarloResultDto SimulateEncounter(
        IReadOnlyList<(Creature Creature, Weapon? Weapon)> teamA,
        IReadOnlyList<(Creature Creature, Weapon? Weapon)> teamB,
        int iterations = 10000,
        TargetingStrategy targetingStrategy = TargetingStrategy.LowestHp,
        ICombatSimulatorService? simulatorOverride = null)
    {
        ArgumentNullException.ThrowIfNull(teamA);
        ArgumentNullException.ThrowIfNull(teamB);

        if (teamA.Count == 0)
            throw new ArgumentException("El Equipo A (La Party) debe contener al menos un combatiente.", nameof(teamA));

        if (teamB.Count == 0)
            throw new ArgumentException("El Equipo B (Los Monstruos) debe contener al menos un combatiente.", nameof(teamB));

        foreach (var (creature, weapon) in teamA)
        {
            if (weapon == null && (!creature.IsPlayerCharacter || !creature.ExpectedDpr.HasValue || creature.ExpectedDpr <= 0))
                throw new ArgumentNullException(nameof(weapon), $"El combatiente {creature.Name} requiere un arma al no poseer ExpectedDpr.");
        }

        foreach (var (creature, weapon) in teamB)
        {
            if (weapon == null && (!creature.IsPlayerCharacter || !creature.ExpectedDpr.HasValue || creature.ExpectedDpr <= 0))
                throw new ArgumentNullException(nameof(weapon), $"El combatiente {creature.Name} requiere un arma al no poseer ExpectedDpr.");
        }

        if (iterations <= 0 || iterations > 50000)
            throw new ArgumentOutOfRangeException(nameof(iterations), "El número de iteraciones de Monte Carlo debe estar entre 1 y 50,000.");

        var stopwatch = Stopwatch.StartNew();

        int totalWinsA = 0;
        int totalWinsB = 0;
        int totalDraws = 0;
        long totalRoundsSum = 0;
        long totalDamageASum = 0;
        long totalDamageBSum = 0;
        long totalTeamASurvivorsSum = 0;

        var simulator = simulatorOverride ?? _combatSimulator;

        // Procesamiento concurrente de encuentros mediante hilos de trabajo paralelos y acumuladores locales
        Parallel.For(
            0,
            iterations,
            () => new ThreadLocalAccumulator(),
            (i, loopState, local) =>
            {
                var encounter = simulator.SimulateFastEncounter(teamA, teamB, targetingStrategy);
                if (encounter.Winner == CombatWinner.TeamA)
                {
                    local.WinsA++;
                }
                else if (encounter.Winner == CombatWinner.TeamB)
                {
                    local.WinsB++;
                }
                else
                {
                    local.Draws++;
                }

                local.Rounds += encounter.Rounds;
                local.DamageA += encounter.TeamADamageDealt;
                local.DamageB += encounter.TeamBDamageDealt;
                local.TeamASurvivors += encounter.TeamASurvivorCount;

                return local;
            },
            local =>
            {
                Interlocked.Add(ref totalWinsA, local.WinsA);
                Interlocked.Add(ref totalWinsB, local.WinsB);
                Interlocked.Add(ref totalDraws, local.Draws);
                Interlocked.Add(ref totalRoundsSum, local.Rounds);
                Interlocked.Add(ref totalDamageASum, local.DamageA);
                Interlocked.Add(ref totalDamageBSum, local.DamageB);
                Interlocked.Add(ref totalTeamASurvivorsSum, local.TeamASurvivors);
            });

        stopwatch.Stop();

        var winRateA = Math.Round((double)totalWinsA / iterations * 100.0, 2);
        var winRateB = Math.Round((double)totalWinsB / iterations * 100.0, 2);
        var drawRate = Math.Round((double)totalDraws / iterations * 100.0, 2);

        var avgRounds = Math.Round((double)totalRoundsSum / iterations, 2);
        var avgDprA = totalRoundsSum > 0 ? Math.Round((double)totalDamageASum / totalRoundsSum, 2) : 0;
        var avgDprB = totalRoundsSum > 0 ? Math.Round((double)totalDamageBSum / totalRoundsSum, 2) : 0;

        int initialTeamACount = teamA.Count;
        double survivalRate = initialTeamACount > 0
            ? Math.Round((double)totalTeamASurvivorsSum / (iterations * initialTeamACount) * 100.0, 2)
            : 0.0;

        var nameA = teamA.Count == 1
            ? teamA[0].Creature.Name
            : $"Party ({string.Join(", ", teamA.Select(t => t.Creature.Name))})";

        var nameB = teamB.Count == 1
            ? teamB[0].Creature.Name
            : $"Encuentro ({string.Join(", ", teamB.Select(t => t.Creature.Name))})";

        var weaponNameA = teamA.Count == 1
            ? (teamA[0].Weapon?.Name ?? (teamA[0].Creature.ExpectedDpr.HasValue ? $"DPR Baseline ({teamA[0].Creature.Archetype ?? "Hero"})" : "Unarmed"))
            : "Arsenal de Party";

        var weaponNameB = teamB.Count == 1
            ? (teamB[0].Weapon?.Name ?? (teamB[0].Creature.ExpectedDpr.HasValue ? $"DPR Baseline ({teamB[0].Creature.Archetype ?? "Hero"})" : "Unarmed"))
            : "Arsenal de Monstruos";

        return new MontecarloResultDto(
            TotalIterations: iterations,
            CombatantAWins: totalWinsA,
            CombatantBWins: totalWinsB,
            Draws: totalDraws,
            CombatantAWinRate: winRateA,
            CombatantBWinRate: winRateB,
            DrawRate: drawRate,
            AverageRounds: avgRounds,
            CombatantAAverageDpr: avgDprA,
            CombatantBAverageDpr: avgDprB,
            CombatantAName: nameA,
            CombatantBName: nameB,
            WeaponAName: weaponNameA,
            WeaponBName: weaponNameB,
            ExecutionTimeMs: stopwatch.ElapsedMilliseconds,
            TeamASurvivalRate: survivalRate,
            TeamANames: teamA.Select(t => t.Creature.Name).ToList().AsReadOnly(),
            TeamBNames: teamB.Select(t => t.Creature.Name).ToList().AsReadOnly());
    }

    /// <inheritdoc />
    public async Task<MontecarloResultDto> SimulateAsync(
        MontecarloRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_context == null)
            throw new InvalidOperationException("El contexto de base de datos no está disponible.");

        // Construir listas de requerimientos de combate para Team A y Team B (compatibilidad dual 1v1 y N vs M)
        var teamARequests = new List<CombatantRequestDto>();
        if (request.TeamA != null && request.TeamA.Count > 0)
        {
            teamARequests.AddRange(request.TeamA);
        }
        else if (request.CombatantAId.HasValue && request.CombatantAId.Value != Guid.Empty)
        {
            teamARequests.Add(new CombatantRequestDto(request.CombatantAId.Value, request.WeaponAId));
        }
        else
        {
            throw new ArgumentException("Se requiere especificar al menos un combatiente para el Equipo A.");
        }

        var teamBRequests = new List<CombatantRequestDto>();
        if (request.TeamB != null && request.TeamB.Count > 0)
        {
            teamBRequests.AddRange(request.TeamB);
        }
        else if (request.CombatantBId.HasValue && request.CombatantBId.Value != Guid.Empty)
        {
            teamBRequests.Add(new CombatantRequestDto(request.CombatantBId.Value, request.WeaponBId));
        }
        else
        {
            throw new ArgumentException("Se requiere especificar al menos un combatiente para el Equipo B.");
        }

        // Carga eficiente por lotes de criaturas y armas únicas desde la base de datos
        var uniqueCreatureIds = teamARequests.Select(r => r.CreatureId)
            .Concat(teamBRequests.Select(r => r.CreatureId))
            .Distinct()
            .ToList();

        var creaturesDict = await _context.Creatures
            .Include(c => c.EquippedArmor)
            .Include(c => c.EquippedShield)
            .Where(c => uniqueCreatureIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, cancellationToken);

        var uniqueWeaponIds = teamARequests.Where(r => r.WeaponId.HasValue && r.WeaponId.Value != Guid.Empty)
            .Select(r => r.WeaponId!.Value)
            .Concat(teamBRequests.Where(r => r.WeaponId.HasValue && r.WeaponId.Value != Guid.Empty).Select(r => r.WeaponId!.Value))
            .Distinct()
            .ToList();

        var weaponsDict = uniqueWeaponIds.Count > 0
            ? await _context.Weapons.Where(w => uniqueWeaponIds.Contains(w.Id)).ToDictionaryAsync(w => w.Id, cancellationToken)
            : new Dictionary<Guid, Weapon>();

        // Resolver combatientes de Team A
        var teamA = new List<(Creature Creature, Weapon? Weapon)>();
        foreach (var req in teamARequests)
        {
            if (!creaturesDict.TryGetValue(req.CreatureId, out var creature))
            {
                throw new KeyNotFoundException($"No se encontró la criatura con ID: {req.CreatureId}");
            }

            Weapon? weapon = null;
            if (req.WeaponId.HasValue && req.WeaponId.Value != Guid.Empty)
            {
                if (!weaponsDict.TryGetValue(req.WeaponId.Value, out weapon))
                {
                    throw new KeyNotFoundException($"No se encontró el arma con ID: {req.WeaponId.Value}");
                }
            }
            else if (!creature.IsPlayerCharacter || !creature.ExpectedDpr.HasValue || creature.ExpectedDpr.Value <= 0)
            {
                throw new KeyNotFoundException($"Se requiere un arma para el combatiente {creature.Name} al no poseer ExpectedDpr.");
            }

            teamA.Add((creature, weapon));
        }

        // Resolver combatientes de Team B
        var teamB = new List<(Creature Creature, Weapon? Weapon)>();
        foreach (var req in teamBRequests)
        {
            if (!creaturesDict.TryGetValue(req.CreatureId, out var creature))
            {
                throw new KeyNotFoundException($"No se encontró la criatura con ID: {req.CreatureId}");
            }

            Weapon? weapon = null;
            if (req.WeaponId.HasValue && req.WeaponId.Value != Guid.Empty)
            {
                if (!weaponsDict.TryGetValue(req.WeaponId.Value, out weapon))
                {
                    throw new KeyNotFoundException($"No se encontró el arma con ID: {req.WeaponId.Value}");
                }
            }
            else if (!creature.IsPlayerCharacter || !creature.ExpectedDpr.HasValue || creature.ExpectedDpr.Value <= 0)
            {
                throw new KeyNotFoundException($"Se requiere un arma para el combatiente {creature.Name} al no poseer ExpectedDpr.");
            }

            teamB.Add((creature, weapon));
        }

        return SimulateEncounter(teamA, teamB, request.Iterations, request.TargetingStrategy);
    }

    /// <summary>
    /// Acumulador local por hilo para eliminar la contención entre núcleos de procesador.
    /// </summary>
    private class ThreadLocalAccumulator
    {
        public int WinsA { get; set; }
        public int WinsB { get; set; }
        public int Draws { get; set; }
        public long Rounds { get; set; }
        public long DamageA { get; set; }
        public long DamageB { get; set; }
        public long TeamASurvivors { get; set; }
    }
}
