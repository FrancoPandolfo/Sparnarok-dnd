using Sparnarok.Api.Domain.Enums;
using Sparnarok.Api.Domain.Services.Rng;

namespace Sparnarok.Api.Domain.Services.Combat;

/// <summary>
/// Estrategia de selección de objetivos orientada a economía de acciones (Focus Fire / Lowest HP).
/// El atacante enfoca al enemigo con menor cantidad de Puntos de Golpe actuales para eliminarlo del combate
/// y reducir las acciones ofensivas del bando rival.
/// </summary>
public class LowestHpTargetingStrategy : ITargetingStrategy
{
    /// <inheritdoc />
    public TargetingStrategy StrategyType => TargetingStrategy.LowestHp;

    /// <inheritdoc />
    public CombatFighter? SelectTarget(CombatFighter attacker, IReadOnlyList<CombatFighter> opponents, IRandomProvider random)
    {
        ArgumentNullException.ThrowIfNull(opponents);

        CombatFighter? target = null;
        int minHp = int.MaxValue;

        for (int i = 0; i < opponents.Count; i++)
        {
            var opponent = opponents[i];
            if (opponent.IsAlive && opponent.CurrentHp < minHp)
            {
                minHp = opponent.CurrentHp;
                target = opponent;
            }
        }

        return target;
    }
}
