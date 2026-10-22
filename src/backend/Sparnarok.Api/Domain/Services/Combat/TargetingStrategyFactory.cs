using Sparnarok.Api.Domain.Enums;

namespace Sparnarok.Api.Domain.Services.Combat;

/// <summary>
/// Fábrica y resolvedor de estrategias de selección de objetivos (Targeting AI).
/// </summary>
public static class TargetingStrategyFactory
{
    private static readonly ITargetingStrategy Random = new RandomTargetingStrategy();
    private static readonly ITargetingStrategy LowestHp = new LowestHpTargetingStrategy();

    /// <summary>
    /// Retorna la implementación concreta de estrategia asociada al tipo indicado.
    /// </summary>
    /// <param name="strategy">Tipo de estrategia solicitada.</param>
    /// <returns>Instancia singleton de la estrategia de selección de objetivo.</returns>
    public static ITargetingStrategy GetStrategy(TargetingStrategy strategy) => strategy switch
    {
        TargetingStrategy.Random => Random,
        TargetingStrategy.LowestHp => LowestHp,
        _ => LowestHp
    };
}
