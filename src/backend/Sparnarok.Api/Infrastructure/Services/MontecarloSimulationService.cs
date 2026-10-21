using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Sparnarok.Api.Application.DTOs;
using Sparnarok.Api.Application.Interfaces;
using Sparnarok.Api.Domain.Entities;
using Sparnarok.Api.Infrastructure.Persistence;

namespace Sparnarok.Api.Infrastructure.Services;

/// <summary>
/// Servicio de aplicación que implementa la simulación masiva de Monte Carlo mediante paralelismo en memoria,
/// calculando métricas probabilísticas agregadas (Win Rates, promedio de rondas y DPR).
/// </summary>
public class MontecarloSimulationService : IMontecarloSimulationService
{
    private readonly ICombatSimulatorService _combatSimulator;
    private readonly SparnarokDbContext? _context;

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="MontecarloSimulationService"/>.
    /// </summary>
    /// <param name="combatSimulator">Motor de combate 1v1.</param>
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
        Weapon weaponA,
        Creature combatantB,
        Weapon weaponB,
        int iterations = 10000,
        ICombatSimulatorService? simulatorOverride = null)
    {
        ArgumentNullException.ThrowIfNull(combatantA);
        ArgumentNullException.ThrowIfNull(weaponA);
        ArgumentNullException.ThrowIfNull(combatantB);
        ArgumentNullException.ThrowIfNull(weaponB);

        if (iterations <= 0 || iterations > 50000)
            throw new ArgumentOutOfRangeException(nameof(iterations), "El número de iteraciones de Monte Carlo debe estar entre 1 y 50,000.");

        var stopwatch = Stopwatch.StartNew();

        int totalWinsA = 0;
        int totalWinsB = 0;
        int totalDraws = 0;
        long totalRoundsSum = 0;
        long totalDamageASum = 0;
        long totalDamageBSum = 0;

        var simulator = simulatorOverride ?? _combatSimulator;

        // Procesamiento concurrente de duelos mediante hilos de trabajo paralelos
        Parallel.For(
            0,
            iterations,
            () => new ThreadLocalAccumulator(),
            (i, loopState, local) =>
            {
                var duel = simulator.SimulateFastDuel(combatantA, weaponA, combatantB, weaponB);
                if (duel.WinnerId == combatantA.Id)
                {
                    local.WinsA++;
                }
                else if (duel.WinnerId == combatantB.Id)
                {
                    local.WinsB++;
                }
                else
                {
                    local.Draws++;
                }

                local.Rounds += duel.Rounds;
                local.DamageA += duel.DamageDealtA;
                local.DamageB += duel.DamageDealtB;

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
            });

        stopwatch.Stop();

        var winRateA = Math.Round((double)totalWinsA / iterations * 100.0, 2);
        var winRateB = Math.Round((double)totalWinsB / iterations * 100.0, 2);
        var drawRate = Math.Round((double)totalDraws / iterations * 100.0, 2);

        var avgRounds = Math.Round((double)totalRoundsSum / iterations, 2);
        var avgDprA = totalRoundsSum > 0 ? Math.Round((double)totalDamageASum / totalRoundsSum, 2) : 0;
        var avgDprB = totalRoundsSum > 0 ? Math.Round((double)totalDamageBSum / totalRoundsSum, 2) : 0;

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
            CombatantAName: combatantA.Name,
            CombatantBName: combatantB.Name,
            WeaponAName: weaponA.Name,
            WeaponBName: weaponB.Name,
            ExecutionTimeMs: stopwatch.ElapsedMilliseconds);
    }

    /// <inheritdoc />
    public async Task<MontecarloResultDto> SimulateAsync(
        MontecarloRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_context == null)
            throw new InvalidOperationException("El contexto de base de datos no está disponible.");

        var combatantA = await _context.Creatures
            .Include(c => c.EquippedArmor)
            .Include(c => c.EquippedShield)
            .FirstOrDefaultAsync(c => c.Id == request.CombatantAId, cancellationToken)
            ?? throw new KeyNotFoundException($"No se encontró la criatura A con ID: {request.CombatantAId}");

        var weaponA = await _context.Weapons
            .FirstOrDefaultAsync(w => w.Id == request.WeaponAId, cancellationToken)
            ?? throw new KeyNotFoundException($"No se encontró el arma A con ID: {request.WeaponAId}");

        var combatantB = await _context.Creatures
            .Include(c => c.EquippedArmor)
            .Include(c => c.EquippedShield)
            .FirstOrDefaultAsync(c => c.Id == request.CombatantBId, cancellationToken)
            ?? throw new KeyNotFoundException($"No se encontró la criatura B con ID: {request.CombatantBId}");

        var weaponB = await _context.Weapons
            .FirstOrDefaultAsync(w => w.Id == request.WeaponBId, cancellationToken)
            ?? throw new KeyNotFoundException($"No se encontró el arma B con ID: {request.WeaponBId}");

        return Simulate(combatantA, weaponA, combatantB, weaponB, request.Iterations);
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
    }
}
