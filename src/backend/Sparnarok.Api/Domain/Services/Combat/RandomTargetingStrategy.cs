using Sparnarok.Api.Domain.Enums;
using Sparnarok.Api.Domain.Services.Rng;

namespace Sparnarok.Api.Domain.Services.Combat;

/// <summary>
/// Estrategia de selección de objetivos aleatoria (Random Aggro).
/// El atacante elige a cualquier enemigo vivo del equipo contrario con distribución uniforme.
/// </summary>
public class RandomTargetingStrategy : ITargetingStrategy
{
    /// <inheritdoc />
    public TargetingStrategy StrategyType => TargetingStrategy.Random;

    /// <inheritdoc />
    public CombatFighter? SelectTarget(CombatFighter attacker, IReadOnlyList<CombatFighter> opponents, IRandomProvider random)
    {
        ArgumentNullException.ThrowIfNull(opponents);
        ArgumentNullException.ThrowIfNull(random);

        var livingOpponents = opponents.Where(o => o.IsAlive).ToList();
        if (livingOpponents.Count == 0)
        {
            return null;
        }

        if (livingOpponents.Count == 1)
        {
            return livingOpponents[0];
        }

        var index = random.Next(0, livingOpponents.Count);
        return livingOpponents[index];
    }
}
