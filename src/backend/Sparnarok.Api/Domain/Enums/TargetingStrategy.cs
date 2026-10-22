namespace Sparnarok.Api.Domain.Enums;

/// <summary>
/// Estrategia heurística de selección de objetivos (Targeting AI) para combatientes en encuentros grupales.
/// </summary>
public enum TargetingStrategy
{
    /// <summary>
    /// Selecciona un enemigo con vida al azar mediante distribución uniforme.
    /// </summary>
    Random = 0,

    /// <summary>
    /// Selecciona prioritariamente al enemigo con vida que tenga la menor cantidad de puntos de golpe (HP) actuales,
    /// buscando maximizar la economía de acciones eliminando combatientes del bando rival.
    /// </summary>
    LowestHp = 1
}
