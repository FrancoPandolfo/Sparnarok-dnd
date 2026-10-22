using Sparnarok.Api.Domain.Entities;
using Sparnarok.Api.Domain.Enums;

namespace Sparnarok.Api.Domain.Services.Combat;

/// <summary>
/// Representa el estado mutable en tiempo de ejecución de un combatiente participante en un combate (1v1 o N vs M).
/// Soporta múltiples instancias de la misma criatura (ej. 4 Goblins) con identificadores únicos e independientes.
/// </summary>
public class CombatFighter
{
    /// <summary>
    /// Identificador único de esta instancia en el combate.
    /// </summary>
    public Guid InstanceId { get; }

    /// <summary>
    /// Criatura de origen con sus estadísticas base (atributos, CA, HP, ExpectedDpr).
    /// </summary>
    public Creature Creature { get; }

    /// <summary>
    /// Arma portada para el encuentro (opcional si la criatura tiene ExpectedDpr).
    /// </summary>
    public Weapon? Weapon { get; }

    /// <summary>
    /// Bando o facción a la que pertenece el combatiente (Team A o Team B).
    /// </summary>
    public TeamSide Team { get; }

    /// <summary>
    /// Valor final de iniciativa obtenido para el combate.
    /// </summary>
    public int Initiative { get; set; }

    /// <summary>
    /// Puntos de golpe actuales durante el desarrollo del encuentro.
    /// </summary>
    public int CurrentHp { get; set; }

    /// <summary>
    /// Puntos de golpe con los que inició el encuentro.
    /// </summary>
    public int StartingHp { get; }

    /// <summary>
    /// Clase de armadura efectiva calculada dinámicamente.
    /// </summary>
    public int EffectiveAc { get; }

    /// <summary>
    /// Determina si el combatiente permanece con vida y operativo en la batalla.
    /// </summary>
    public bool IsAlive => CurrentHp > 0;

    /// <summary>
    /// Inicializa una nueva instancia de combatiente en el cuadrilátero táctico.
    /// </summary>
    public CombatFighter(Creature creature, Weapon? weapon, TeamSide team, Guid? instanceId = null)
    {
        Creature = creature ?? throw new ArgumentNullException(nameof(creature));
        Weapon = weapon;
        Team = team;
        InstanceId = instanceId ?? Guid.NewGuid();
        StartingHp = creature.HitPoints.Current;
        CurrentHp = StartingHp;
        EffectiveAc = creature.EffectiveArmorClass;
    }
}
