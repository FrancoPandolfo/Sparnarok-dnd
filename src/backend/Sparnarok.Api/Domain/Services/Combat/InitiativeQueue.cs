using Sparnarok.Api.Domain.Enums;
using Sparnarok.Api.Domain.Services.Rng;

namespace Sparnarok.Api.Domain.Services.Combat;

/// <summary>
/// Administrador de la Cola Global de Iniciativa para encuentros grupales (Party vs. Encounter).
/// Resuelve la tirada de iniciativa (1d20 + Modificador de Destreza) para todas las criaturas
/// y las ordena de mayor a menor con desempates canónicos de D&amp;D 5e.
/// </summary>
public static class InitiativeQueue
{
    /// <summary>
    /// Tira iniciativa para todos los combatientes proporcionados y los ordena en la cola de turnos.
    /// </summary>
    /// <param name="fighters">Colección de combatientes participantes.</param>
    /// <param name="random">Proveedor de números pseudoaleatorios para la tirada de d20 y desempates.</param>
    /// <returns>Lista ordenada de combatientes según su orden de turno descendente.</returns>
    public static List<CombatFighter> RollAndOrder(IReadOnlyList<CombatFighter> fighters, IRandomProvider random)
    {
        ArgumentNullException.ThrowIfNull(fighters);
        ArgumentNullException.ThrowIfNull(random);

        foreach (var fighter in fighters)
        {
            var dexMod = fighter.Creature.AbilityScores.GetModifier(AbilityScoreType.Dexterity);
            var d20 = random.RollD20();
            fighter.Initiative = d20 + dexMod;
        }

        return Order(fighters, random);
    }

    /// <summary>
    /// Ordena combatientes que ya poseen iniciativa asignada de mayor a menor,
    /// aplicando desempate por Modificador de Destreza y aleatoriedad en caso de persistir el empate.
    /// </summary>
    /// <param name="fighters">Combatientes con iniciativa asignada.</param>
    /// <param name="random">Proveedor de números aleatorios para desempates finales.</param>
    /// <returns>Lista ordenada de mayor a menor iniciativa.</returns>
    public static List<CombatFighter> Order(IReadOnlyList<CombatFighter> fighters, IRandomProvider? random = null)
    {
        ArgumentNullException.ThrowIfNull(fighters);

        var list = new List<CombatFighter>(fighters);

        list.Sort((a, b) =>
        {
            // 1. Mayor iniciativa va primero
            int initComparison = b.Initiative.CompareTo(a.Initiative);
            if (initComparison != 0) return initComparison;

            // 2. Desempate: Mayor Modificador de Destreza
            int aDex = a.Creature.AbilityScores.GetModifier(AbilityScoreType.Dexterity);
            int bDex = b.Creature.AbilityScores.GetModifier(AbilityScoreType.Dexterity);
            int dexComparison = bDex.CompareTo(aDex);
            if (dexComparison != 0) return dexComparison;

            // 3. Desempate final: Comparación determinista o aleatoria
            if (random != null)
            {
                return random.Next(0, 2) == 0 ? -1 : 1;
            }

            return a.InstanceId.CompareTo(b.InstanceId);
        });

        return list;
    }
}
