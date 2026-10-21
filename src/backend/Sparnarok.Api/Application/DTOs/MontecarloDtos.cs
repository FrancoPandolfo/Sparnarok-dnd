namespace Sparnarok.Api.Application.DTOs;

/// <summary>
/// Solicitud para ejecutar una simulación estocástica masiva de combate Monte Carlo.
/// </summary>
/// <param name="CombatantAId">Identificador de la primera criatura.</param>
/// <param name="WeaponAId">Identificador del arma de la primera criatura (opcional si la criatura tiene ExpectedDpr).</param>
/// <param name="CombatantBId">Identificador de la segunda criatura.</param>
/// <param name="WeaponBId">Identificador del arma de la segunda criatura (opcional si la criatura tiene ExpectedDpr).</param>
/// <param name="Iterations">Número de iteraciones del duelo a simular concurrentemente (por defecto 10,000).</param>
public record MontecarloRequestDto(
    Guid CombatantAId,
    Guid? WeaponAId,
    Guid CombatantBId,
    Guid? WeaponBId,
    int Iterations = 10000);


/// <summary>
/// Métricas agregadas y estadísticas consolidadas resultantes de la simulación de Monte Carlo.
/// </summary>
/// <param name="TotalIterations">Número total de iteraciones efectivamente ejecutadas.</param>
/// <param name="CombatantAWins">Victorias totales del Combatiente A.</param>
/// <param name="CombatantBWins">Victorias totales del Combatiente B.</param>
/// <param name="Draws">Cantidad de enfrentamientos terminados en empate (límite de rondas alcanzado).</param>
/// <param name="CombatantAWinRate">Porcentaje de victoria del Combatiente A (ej. 62.4%).</param>
/// <param name="CombatantBWinRate">Porcentaje de victoria del Combatiente B (ej. 37.6%).</param>
/// <param name="DrawRate">Porcentaje de duelos finalizados en empate (ej. 0.0%).</param>
/// <param name="AverageRounds">Promedio de rondas que duró el combate hasta la derrota de un combatiente.</param>
/// <param name="CombatantAAverageDpr">Promedio de daño por turno/ronda infligido por el Combatiente A.</param>
/// <param name="CombatantBAverageDpr">Promedio de daño por turno/ronda infligido por el Combatiente B.</param>
/// <param name="CombatantAName">Nombre de la primera criatura.</param>
/// <param name="CombatantBName">Nombre de la segunda criatura.</param>
/// <param name="WeaponAName">Nombre del arma equipada por el primer combatiente.</param>
/// <param name="WeaponBName">Nombre del arma equipada por el segundo combatiente.</param>
/// <param name="ExecutionTimeMs">Tiempo de cómputo en milisegundos consumido en la ejecución paralela.</param>
public record MontecarloResultDto(
    int TotalIterations,
    int CombatantAWins,
    int CombatantBWins,
    int Draws,
    double CombatantAWinRate,
    double CombatantBWinRate,
    double DrawRate,
    double AverageRounds,
    double CombatantAAverageDpr,
    double CombatantBAverageDpr,
    string CombatantAName,
    string CombatantBName,
    string WeaponAName,
    string WeaponBName,
    long ExecutionTimeMs);
