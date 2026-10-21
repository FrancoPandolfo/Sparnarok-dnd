namespace Sparnarok.Api.Domain.Services.Combat;

/// <summary>
/// Resultado integral y telemetría de una simulación de combate 1 contra 1.
/// </summary>
/// <param name="SimulationId">Identificador único de la simulación.</param>
/// <param name="Combatant1">Resumen del primer combatiente.</param>
/// <param name="Combatant2">Resumen del segundo combatiente.</param>
/// <param name="WinnerName">Nombre del combatiente victorioso (o "Empate" si alcanzara el límite de rondas).</param>
/// <param name="WinnerId">Identificador único del vencedor (o null en caso de empate).</param>
/// <param name="TotalRounds">Número total de rondas de combate consumidas.</param>
/// <param name="Logs">Registro detallado de todos los ataques y eventos de daño acontecidos.</param>
/// <param name="ExecutedAt">Marca temporal UTC de la ejecución.</param>
public record CombatSimulationResult(
    Guid SimulationId,
    CombatantSummary Combatant1,
    CombatantSummary Combatant2,
    string WinnerName,
    Guid? WinnerId,
    int TotalRounds,
    IReadOnlyList<CombatEventLog> Logs,
    DateTime ExecutedAt);
