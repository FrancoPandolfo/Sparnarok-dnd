using Sparnarok.Api.Domain.Entities;
using Sparnarok.Api.Domain.Services.Combat;

namespace Sparnarok.Api.Application.Interfaces;

/// <summary>
/// Contrato del servicio de simulación matemática y orquestación de combate para D&amp;D 5e (2024).
/// </summary>
public interface ICombatSimulatorService
{
    /// <summary>
    /// Simula un enfrentamiento táctico 1 contra 1 entre dos criaturas equipadas con armas específicas.
    /// Resuelve iniciativa estocástica, turnos sucesivos, tiradas de ataque d20 vs CA, cálculo de daño crítico y fin del combate.
    /// </summary>
    /// <param name="combatant1">Primera criatura combatiente.</param>
    /// <param name="weapon1">Arma empleada por el primer combatiente.</param>
    /// <param name="combatant2">Segunda criatura combatiente.</param>
    /// <param name="weapon2">Arma empleada por el segundo combatiente.</param>
    /// <returns>Resultado exhaustivo con logs y métricas de la simulación.</returns>
    CombatSimulationResult SimulateDuel(
        Creature combatant1,
        Weapon weapon1,
        Creature combatant2,
        Weapon weapon2);

    /// <summary>
    /// Simula un duelo 1 contra 1 de forma acelerada, sin instanciar logs de texto,
    /// para maximizar el rendimiento en simulaciones Monte Carlo concurrentes.
    /// </summary>
    FastDuelResult SimulateFastDuel(
        Creature combatant1,
        Weapon weapon1,
        Creature combatant2,
        Weapon weapon2);

    /// <summary>
    /// Consulta criaturas y armas desde la base de datos relacional y ejecuta la simulación de duelo.
    /// </summary>
    /// <param name="creature1Id">ID de la primera criatura.</param>
    /// <param name="weapon1Id">ID del arma de la primera criatura.</param>
    /// <param name="creature2Id">ID de la segunda criatura.</param>
    /// <param name="weapon2Id">ID del arma de la segunda criatura.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Resultado del duelo simulado.</returns>
    Task<CombatSimulationResult> SimulateDuelAsync(
        Guid creature1Id,
        Guid weapon1Id,
        Guid creature2Id,
        Guid weapon2Id,
        CancellationToken cancellationToken = default);
}
