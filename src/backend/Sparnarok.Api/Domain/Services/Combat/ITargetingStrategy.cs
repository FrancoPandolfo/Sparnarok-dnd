using Sparnarok.Api.Domain.Enums;
using Sparnarok.Api.Domain.Services.Rng;

namespace Sparnarok.Api.Domain.Services.Combat;

/// <summary>
/// Contrato de estrategia para la Inteligencia Artificial de Selección de Objetivos (Targeting AI).
/// Diseñada de forma extensible para incorporar a futuro heurísticas complejas
/// (ej. Enfoque al Sanador, Mayor Amenaza/DPR, Venganza contra el último atacante).
/// </summary>
public interface ITargetingStrategy
{
    /// <summary>
    /// Tipo de estrategia implementada.
    /// </summary>
    TargetingStrategy StrategyType { get; }

    /// <summary>
    /// Selecciona el objetivo enemigo a atacar entre los combatientes vivos del equipo rival.
    /// </summary>
    /// <param name="attacker">Combatiente que ejecuta la acción ofensiva.</param>
    /// <param name="opponents">Colección de combatientes del bando rival.</param>
    /// <param name="random">Proveedor de números aleatorios para desempates o dispersión estocástica.</param>
    /// <returns>Combatiente enemigo seleccionado, o null si todos los oponentes han caído.</returns>
    CombatFighter? SelectTarget(CombatFighter attacker, IReadOnlyList<CombatFighter> opponents, IRandomProvider random);
}
