namespace Sparnarok.Api.Domain.Enums;

/// <summary>
/// Resultado del vencedor de un encuentro de combate.
/// </summary>
public enum CombatWinner
{
    /// <summary>
    /// Victoria para el Equipo A (La Party).
    /// </summary>
    TeamA = 0,

    /// <summary>
    /// Victoria para el Equipo B (Los Monstruos / Encuentro).
    /// </summary>
    TeamB = 1,

    /// <summary>
    /// Empate (límite de rondas alcanzado o aniquilación mutua).
    /// </summary>
    Draw = 2
}
