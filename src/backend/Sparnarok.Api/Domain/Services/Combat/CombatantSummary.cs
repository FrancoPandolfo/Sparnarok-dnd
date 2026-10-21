namespace Sparnarok.Api.Domain.Services.Combat;

/// <summary>
/// Resumen del estado inicial y final de un combatiente participante en la simulación.
/// </summary>
/// <param name="CreatureId">Identificador de la criatura.</param>
/// <param name="CreatureName">Nombre de la criatura.</param>
/// <param name="WeaponName">Nombre del arma equipada.</param>
/// <param name="Initiative">Resultado de la tirada de iniciativa (d20 + modificador de Destreza).</param>
/// <param name="StartingHp">Puntos de golpe iniciales antes del combate.</param>
/// <param name="FinalHp">Puntos de golpe finales tras la resolución del combate.</param>
/// <param name="IsAlive">Indica si la criatura finalizó el combate con vida (HP > 0).</param>
public record CombatantSummary(
    Guid CreatureId,
    string CreatureName,
    string WeaponName,
    int Initiative,
    int StartingHp,
    int FinalHp,
    bool IsAlive);
