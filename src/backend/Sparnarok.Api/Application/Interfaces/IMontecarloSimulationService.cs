using Sparnarok.Api.Application.DTOs;
using Sparnarok.Api.Domain.Entities;
using Sparnarok.Api.Application.Interfaces;

namespace Sparnarok.Api.Application.Interfaces;

/// <summary>
/// Contrato del servicio de simulación masiva y agregación estadística por método de Monte Carlo.
/// Ejecuta duelos concurrentes para estimar Win Rates, promedio de rondas y Damage per Round (DPR).
/// </summary>
public interface IMontecarloSimulationService
{
    /// <summary>
    /// Ejecuta de forma concurrente una simulación de Monte Carlo en memoria entre dos criaturas y armas.
    /// </summary>
    /// <param name="combatantA">Primera criatura.</param>
    /// <param name="weaponA">Arma de la primera criatura.</param>
    /// <param name="combatantB">Segunda criatura.</param>
    /// <param name="weaponB">Arma de la segunda criatura.</param>
    /// <param name="iterations">Número de iteraciones a ejecutar (1 a 50,000).</param>
    /// <param name="simulatorOverride">Instancia opcional del simulador de combate (para pruebas deterministas).</param>
    /// <returns>Resultados agregados y distribuciones estadísticas.</returns>
    MontecarloResultDto Simulate(
        Creature combatantA,
        Weapon? weaponA,
        Creature combatantB,
        Weapon? weaponB,
        int iterations = 10000,
        ICombatSimulatorService? simulatorOverride = null);


    /// <summary>
    /// Carga criaturas y armas desde la base de datos y ejecuta la simulación masiva de Monte Carlo.
    /// </summary>
    /// <param name="request">Parámetros de la simulación.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Métricas consolidadas de la simulación.</returns>
    Task<MontecarloResultDto> SimulateAsync(
        MontecarloRequestDto request,
        CancellationToken cancellationToken = default);
}
