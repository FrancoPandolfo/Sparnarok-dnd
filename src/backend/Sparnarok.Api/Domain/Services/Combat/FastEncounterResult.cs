using Sparnarok.Api.Domain.Enums;

namespace Sparnarok.Api.Domain.Services.Combat;

/// <summary>
/// Resultado optimizado en memoria y libre de asignaciones en heap para simulaciones masivas de encuentros grupales N vs M.
/// </summary>
/// <param name="Winner">Bando ganador (TeamA, TeamB o Draw).</param>
/// <param name="Rounds">Rondas transcurridas hasta la victoria de un equipo o límite de combate.</param>
/// <param name="TeamASurvivorCount">Cantidad de miembros del Equipo A que sobrevivieron con vida al encuentro.</param>
/// <param name="TeamADamageDealt">Daño acumulado infligido por los miembros del Equipo A.</param>
/// <param name="TeamBDamageDealt">Daño acumulado infligido por los miembros del Equipo B.</param>
public readonly record struct FastEncounterResult(
    CombatWinner Winner,
    int Rounds,
    int TeamASurvivorCount,
    int TeamADamageDealt,
    int TeamBDamageDealt);
