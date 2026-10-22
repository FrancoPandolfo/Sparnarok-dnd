using Sparnarok.Api.Domain.Enums;

namespace Sparnarok.Api.Application.DTOs;

/// <summary>
/// Especificación de un combatiente participante dentro de un equipo en un encuentro grupal.
/// </summary>
/// <param name="CreatureId">Identificador de la criatura.</param>
/// <param name="WeaponId">Identificador del arma portada (opcional si la criatura posee ExpectedDpr).</param>
public record CombatantRequestDto(
    Guid CreatureId,
    Guid? WeaponId = null);

/// <summary>
/// Solicitud para ejecutar una simulación estocástica masiva de combate Monte Carlo (1v1 o Party vs Encounter).
/// </summary>
/// <param name="CombatantAId">Identificador de la primera criatura (1v1 legacy o líder de party).</param>
/// <param name="WeaponAId">Identificador del arma de la primera criatura (opcional si tiene ExpectedDpr).</param>
/// <param name="CombatantBId">Identificador de la segunda criatura (1v1 legacy o líder de monstruos).</param>
/// <param name="WeaponBId">Identificador del arma de la segunda criatura (opcional si tiene ExpectedDpr).</param>
/// <param name="TeamA">Lista de combatientes del Equipo A (La Party). Si se omite, se construye a partir de CombatantAId.</param>
/// <param name="TeamB">Lista de combatientes del Equipo B (Los Monstruos). Si se omite, se construye a partir de CombatantBId.</param>
/// <param name="TargetingStrategy">Estrategia de selección de objetivos (Random o LowestHP).</param>
/// <param name="Iterations">Número de iteraciones del encuentro a simular concurrentemente (por defecto 10,000).</param>
public record MontecarloRequestDto(
    Guid? CombatantAId = null,
    Guid? WeaponAId = null,
    Guid? CombatantBId = null,
    Guid? WeaponBId = null,
    int Iterations = 10000,
    List<CombatantRequestDto>? TeamA = null,
    List<CombatantRequestDto>? TeamB = null,
    TargetingStrategy TargetingStrategy = TargetingStrategy.LowestHp);

/// <summary>
/// Métricas agregadas y estadísticas consolidadas resultantes de la simulación de Monte Carlo.
/// </summary>
/// <param name="TotalIterations">Número total de iteraciones efectivamente ejecutadas.</param>
/// <param name="CombatantAWins">Victorias totales del Equipo A / Combatiente A.</param>
/// <param name="CombatantBWins">Victorias totales del Equipo B / Combatiente B.</param>
/// <param name="Draws">Cantidad de enfrentamientos terminados en empate (límite de rondas alcanzado).</param>
/// <param name="CombatantAWinRate">Porcentaje de victoria del Equipo A (ej. 62.4%).</param>
/// <param name="CombatantBWinRate">Porcentaje de victoria del Equipo B (ej. 37.6%).</param>
/// <param name="DrawRate">Porcentaje de duelos finalizados en empate (ej. 0.0%).</param>
/// <param name="AverageRounds">Promedio de rondas que duró el combate hasta la derrota total de un bando.</param>
/// <param name="CombatantAAverageDpr">Promedio de daño por turno/ronda infligido por el Equipo A.</param>
/// <param name="CombatantBAverageDpr">Promedio de daño por turno/ronda infligido por el Equipo B.</param>
/// <param name="CombatantAName">Nombre del primer combatiente o descripción de la party.</param>
/// <param name="CombatantBName">Nombre del segundo combatiente o descripción del encuentro de monstruos.</param>
/// <param name="WeaponAName">Nombre del arma equipada o resumen de armamento de Team A.</param>
/// <param name="WeaponBName">Nombre del arma equipada o resumen de armamento de Team B.</param>
/// <param name="ExecutionTimeMs">Tiempo de cómputo en milisegundos consumido en la ejecución paralela.</param>
/// <param name="TeamASurvivalRate">Tasa promedio de supervivencia porcentual de los miembros del Equipo A (Party Survival Rate).</param>
/// <param name="TeamANames">Nombres de todos los integrantes del Equipo A.</param>
/// <param name="TeamBNames">Nombres de todos los integrantes del Equipo B.</param>
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
    long ExecutionTimeMs,
    double TeamASurvivalRate = 0.0,
    IReadOnlyList<string>? TeamANames = null,
    IReadOnlyList<string>? TeamBNames = null);
