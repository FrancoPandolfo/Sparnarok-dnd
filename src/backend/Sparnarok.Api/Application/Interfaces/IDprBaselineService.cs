namespace Sparnarok.Api.Application.Interfaces;

/// <summary>
/// Representa una entrada en la matriz de daño esperado por ronda (Expected DPR) para un arquetipo a un nivel determinado.
/// </summary>
/// <param name="Archetype">Nombre del arquetipo o clase (ej. 'Fighter 2H', 'Ranger Ranged').</param>
/// <param name="Level">Nivel del personaje (1 a 20).</param>
/// <param name="ExpectedDpr">Daño promedio por ronda precalculado.</param>
/// <param name="Description">Descripción táctica o mecánica de la progresión a ese nivel.</param>
public record DprBaselineEntry(
    string Archetype,
    int Level,
    double ExpectedDpr,
    string Description);

/// <summary>
/// Contrato del catálogo y servicio de evaluación de matrices de daño esperado por ronda (DPR Baselines)
/// para la simulación abstracta y probabilística de Personajes de Jugadores (PCs).
/// </summary>
public interface IDprBaselineService
{
    /// <summary>
    /// Obtiene la lista de arquetipos soportados por el catálogo.
    /// </summary>
    IReadOnlyList<string> GetArchetypes();

    /// <summary>
    /// Consulta el daño promedio esperado por ronda (DPR) para un arquetipo y nivel específicos.
    /// </summary>
    /// <param name="archetype">Nombre del arquetipo (insensible a mayúsculas/minúsculas).</param>
    /// <param name="level">Nivel del personaje (1 a 20).</param>
    /// <returns>Valor numérico del DPR esperado, o null si no se encuentra.</returns>
    double? GetExpectedDpr(string archetype, int level);

    /// <summary>
    /// Retorna la matriz completa de baselines de DPR disponibles en el sistema.
    /// </summary>
    IReadOnlyList<DprBaselineEntry> GetAllBaselines();

    /// <summary>
    /// Retorna la progresión por niveles de un arquetipo específico.
    /// </summary>
    IReadOnlyList<DprBaselineEntry> GetBaselinesForArchetype(string archetype);
}
