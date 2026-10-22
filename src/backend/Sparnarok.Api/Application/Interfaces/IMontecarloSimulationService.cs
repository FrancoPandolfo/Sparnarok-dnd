using Sparnarok.Api.Application.DTOs;
using Sparnarok.Api.Domain.Entities;
using Sparnarok.Api.Domain.Enums;

namespace Sparnarok.Api.Application.Interfaces;

/// <summary>
/// Contrato del servicio de simulación masiva y agregación estadística por método de Monte Carlo.
/// Ejecuta enfrentamientos concurrentes para estimar Win Rates, promedio de rondas, DPR y tasas de supervivencia de la party.
/// </summary>
public interface IMontecarloSimulationService
{
    /// <summary>
    /// Ejecuta de forma concurrente una simulación de Monte Carlo en memoria para un duelo 1v1.
    /// </summary>
    MontecarloResultDto Simulate(
        Creature combatantA,
        Weapon? weaponA,
        Creature combatantB,
        Weapon? weaponB,
        int iterations = 10000,
        ICombatSimulatorService? simulatorOverride = null);

    /// <summary>
    /// Ejecuta de forma concurrente una simulación de Monte Carlo en memoria para un encuentro grupal Party vs Encounter (N vs M).
    /// </summary>
    /// <param name="teamA">Combatientes del Equipo A (La Party).</param>
    /// <param name="teamB">Combatientes del Equipo B (Los Monstruos).</param>
    /// <param name="iterations">Número de iteraciones a ejecutar (1 a 50,000).</param>
    /// <param name="targetingStrategy">Estrategia de selección de objetivos (Random o LowestHP).</param>
    /// <param name="simulatorOverride">Instancia opcional del simulador de combate (para pruebas deterministas).</param>
    /// <returns>Resultados agregados con Win Rates y tasa de supervivencia de la party.</returns>
    MontecarloResultDto SimulateEncounter(
        IReadOnlyList<(Creature Creature, Weapon? Weapon)> teamA,
        IReadOnlyList<(Creature Creature, Weapon? Weapon)> teamB,
        int iterations = 10000,
        TargetingStrategy targetingStrategy = TargetingStrategy.LowestHp,
        ICombatSimulatorService? simulatorOverride = null);

    /// <summary>
    /// Carga criaturas y armas desde la base de datos relacional y ejecuta la simulación masiva de Monte Carlo.
    /// </summary>
    /// <param name="request">Parámetros de la simulación (individual 1v1 o grupal Party vs Encounter).</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Métricas consolidadas de la simulación.</returns>
    Task<MontecarloResultDto> SimulateAsync(
        MontecarloRequestDto request,
        CancellationToken cancellationToken = default);
}
