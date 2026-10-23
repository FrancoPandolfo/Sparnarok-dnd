using Sparnarok.Api.Application.DTOs;
using Sparnarok.Api.Domain.Models;

namespace Sparnarok.Api.Application.Interfaces;

/// <summary>
/// Gestor de estado en memoria para combates activos en vivo por Party,
/// con soporte para proyecciones asimétricas por rol (DM vs Jugador).
/// </summary>
public interface ILiveCombatManager
{
    /// <summary>
    /// Obtiene o inicializa el estado de combate en memoria para la Party especificada.
    /// </summary>
    LiveCombatState GetOrCreateState(Guid partyId, string partyName = "");

    /// <summary>
    /// Obtiene el estado actual de combate si existe.
    /// </summary>
    LiveCombatState? GetState(Guid partyId);

    /// <summary>
    /// Inicializa un nuevo combate con la lista de combatientes dada en estado WaitingForInitiative.
    /// </summary>
    LiveCombatState InitializeCombat(Guid partyId, string partyName, IEnumerable<LiveCombatant> combatants);

    /// <summary>
    /// Asigna la iniciativa de un combatiente específico (opcional, puede ser nula).
    /// </summary>
    bool SetInitiative(Guid partyId, Guid combatantId, decimal? initiative);

    /// <summary>
    /// Reordena un combatiente moviéndolo a una nueva posición en la lista (Drag and Drop en vivo).
    /// </summary>
    bool ReorderCombatant(Guid partyId, Guid combatantId, int newIndex);

    /// <summary>
    /// Adjunta un nuevo combatiente al final de la lista respetando el orden posicional existente.
    /// </summary>
    LiveCombatant AddCombatant(Guid partyId, LiveCombatant combatant);

    /// <summary>
    /// Tira automáticamente la iniciativa para todos los monstruos/NPCs que aún no tengan valor asignado.
    /// </summary>
    void AutoRollMonsters(Guid partyId, Func<int>? d20Roller = null);

    /// <summary>
    /// Inicia formalmente el combate respetando el orden posicional de los combatientes y fijando la ronda en 1.
    /// </summary>
    bool StartCombat(Guid partyId);

    /// <summary>
    /// Avanza al siguiente turno de combate, incrementando la ronda si se completa el ciclo.
    /// </summary>
    bool NextTurn(Guid partyId);

    /// <summary>
    /// Retrocede al turno de combate anterior.
    /// </summary>
    bool PreviousTurn(Guid partyId);

    /// <summary>
    /// Modifica los puntos de golpe (HP) de un combatiente mediante un delta (positivo para curación, negativo para daño).
    /// </summary>
    bool ModifyCombatantHp(Guid partyId, Guid combatantId, int deltaHp);

    /// <summary>
    /// Conmuta o establece la visibilidad de un combatiente ante los jugadores.
    /// </summary>
    bool SetCombatantVisibility(Guid partyId, Guid combatantId, bool isVisible);

    /// <summary>
    /// Finaliza el combate activo.
    /// </summary>
    bool EndCombat(Guid partyId);

    /// <summary>
    /// Genera la proyección completa del estado para el Dungeon Master.
    /// </summary>
    LiveCombatDmProjectionDto CreateDmProjection(Guid partyId);

    /// <summary>
    /// Genera la proyección censurada del estado para los Jugadores (sin monstruos ocultos ni CA/HP exactos).
    /// </summary>
    LiveCombatPlayerProjectionDto CreatePlayerProjection(Guid partyId);
}
