using Sparnarok.Api.Domain.Entities;
using Sparnarok.Api.Domain.Enums;
using Sparnarok.Api.Domain.Services.Combat;

namespace Sparnarok.Api.Application.Interfaces;

/// <summary>
/// Contrato del servicio de simulación matemática y orquestación de combate para D&amp;D 5e (2024).
/// Soporta duelos individuales (1v1) y encuentros tácticos grupales completos Party vs. Encounter (N vs. M).
/// </summary>
public interface ICombatSimulatorService
{
    /// <summary>
    /// Simula un encuentro táctico grupal N vs M entre dos equipos con resolución de iniciativa global,
    /// targeting AI configurable y logging detallado ronda a ronda.
    /// </summary>
    /// <param name="teamA">Combatientes del Equipo A (La Party).</param>
    /// <param name="teamB">Combatientes del Equipo B (Los Monstruos / Encuentro).</param>
    /// <param name="targetingStrategy">Estrategia de selección de objetivos (Random o LowestHP).</param>
    /// <returns>Resultado integral con telemetría, métricas de supervivencia y logs cronológicos.</returns>
    CombatSimulationResult SimulateEncounter(
        IReadOnlyList<(Creature Creature, Weapon? Weapon)> teamA,
        IReadOnlyList<(Creature Creature, Weapon? Weapon)> teamB,
        TargetingStrategy targetingStrategy = TargetingStrategy.LowestHp);

    /// <summary>
    /// Simula un encuentro táctico grupal N vs M de alta velocidad (sin asignaciones de strings/logs)
    /// diseñado para optimizar el rendimiento en simulaciones masivas de Monte Carlo.
    /// </summary>
    /// <param name="teamA">Combatientes del Equipo A.</param>
    /// <param name="teamB">Combatientes del Equipo B.</param>
    /// <param name="targetingStrategy">Estrategia de selección de objetivos.</param>
    /// <returns>Estructura ligera con el bando ganador, rondas, sobrevivientes y daño acumulado.</returns>
    FastEncounterResult SimulateFastEncounter(
        IReadOnlyList<(Creature Creature, Weapon? Weapon)> teamA,
        IReadOnlyList<(Creature Creature, Weapon? Weapon)> teamB,
        TargetingStrategy targetingStrategy = TargetingStrategy.LowestHp);

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
        Weapon? weapon1,
        Creature combatant2,
        Weapon? weapon2);

    /// <summary>
    /// Simula un duelo 1 contra 1 de forma acelerada, sin instanciar logs de texto,
    /// para maximizar el rendimiento en simulaciones Monte Carlo concurrentes.
    /// </summary>
    FastDuelResult SimulateFastDuel(
        Creature combatant1,
        Weapon? weapon1,
        Creature combatant2,
        Weapon? weapon2);

    /// <summary>
    /// Consulta criaturas y armas desde la base de datos relacional y ejecuta la simulación de duelo.
    /// </summary>
    /// <param name="creature1Id">ID de la primera criatura.</param>
    /// <param name="weapon1Id">ID del arma de la primera criatura (opcional si la criatura tiene ExpectedDpr).</param>
    /// <param name="creature2Id">ID de la segunda criatura.</param>
    /// <param name="weapon2Id">ID del arma de la segunda criatura (opcional si la criatura tiene ExpectedDpr).</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Resultado del duelo simulado.</returns>
    Task<CombatSimulationResult> SimulateDuelAsync(
        Guid creature1Id,
        Guid? weapon1Id,
        Guid creature2Id,
        Guid? weapon2Id,
        CancellationToken cancellationToken = default);
}
