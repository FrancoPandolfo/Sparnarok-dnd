using Sparnarok.Api.Domain.Enums;

namespace Sparnarok.Api.Domain.Services.Combat;

/// <summary>
/// Resultado integral y telemetría de una simulación de combate (1 contra 1 o Party vs Encounter).
/// </summary>
/// <param name="SimulationId">Identificador único de la simulación.</param>
/// <param name="Combatant1">Resumen del primer combatiente (o líder del Equipo A).</param>
/// <param name="Combatant2">Resumen del segundo combatiente (o líder del Equipo B).</param>
/// <param name="WinnerName">Nombre del vencedor (o "Equipo A", "Equipo B", "Empate").</param>
/// <param name="WinnerId">Identificador del vencedor (o null en caso de empate/equipo).</param>
/// <param name="TotalRounds">Número total de rondas de combate consumidas.</param>
/// <param name="Logs">Registro detallado de todos los ataques y eventos de daño acontecidos.</param>
/// <param name="ExecutedAt">Marca temporal UTC de la ejecución.</param>
/// <param name="WinnerTeam">Bando vencedor del encuentro (TeamA, TeamB o Draw).</param>
/// <param name="TeamASurvivorCount">Cantidad de combatientes del Equipo A que sobrevivieron.</param>
/// <param name="TeamASummaries">Resumen detallado de todos los combatientes del Equipo A.</param>
/// <param name="TeamBSummaries">Resumen detallado de todos los combatientes del Equipo B.</param>
public record CombatSimulationResult(
    Guid SimulationId,
    CombatantSummary Combatant1,
    CombatantSummary Combatant2,
    string WinnerName,
    Guid? WinnerId,
    int TotalRounds,
    IReadOnlyList<CombatEventLog> Logs,
    DateTime ExecutedAt,
    CombatWinner WinnerTeam = CombatWinner.Draw,
    int TeamASurvivorCount = 0,
    IReadOnlyList<CombatantSummary>? TeamASummaries = null,
    IReadOnlyList<CombatantSummary>? TeamBSummaries = null);
