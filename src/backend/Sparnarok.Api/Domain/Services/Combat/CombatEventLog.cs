namespace Sparnarok.Api.Domain.Services.Combat;

/// <summary>
/// Registro de auditoría y telemetría de un evento individual dentro de una ronda de combate de D&amp;D 5e.
/// </summary>
/// <param name="Round">Número de ronda de combate.</param>
/// <param name="AttackerName">Nombre del combatiente atacante.</param>
/// <param name="DefenderName">Nombre del combatiente defensor.</param>
/// <param name="WeaponName">Nombre del arma empleada para el ataque.</param>
/// <param name="D20Roll">Tirada pura del d20 (1 al 20).</param>
/// <param name="AttackModifier">Bono de ataque total sumado al d20 (competencia + característica).</param>
/// <param name="TotalAttackRoll">Suma total de la tirada de ataque contra la CA.</param>
/// <param name="TargetArmorClass">Clase de Armadura efectiva del defensor.</param>
/// <param name="IsNatural20">Indica si la tirada fue un 20 natural (Impacto Crítico automático).</param>
/// <param name="IsNatural1">Indica si la tirada fue un 1 natural (Fallo automático).</param>
/// <param name="IsHit">Indica si el ataque impactó exitosamente.</param>
/// <param name="DamageDealt">Puntos de daño totales infligidos (0 en caso de fallo).</param>
/// <param name="DefenderRemainingHp">Puntos de golpe restantes del defensor tras el impacto.</param>
/// <param name="Description">Descripción narrativa y matemática del evento de combate.</param>
public record CombatEventLog(
    int Round,
    string AttackerName,
    string DefenderName,
    string WeaponName,
    int D20Roll,
    int AttackModifier,
    int TotalAttackRoll,
    int TargetArmorClass,
    bool IsNatural20,
    bool IsNatural1,
    bool IsHit,
    int DamageDealt,
    int DefenderRemainingHp,
    string Description);
