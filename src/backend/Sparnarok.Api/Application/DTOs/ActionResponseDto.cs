using Sparnarok.Api.Domain.Enums;
using Action = Sparnarok.Api.Domain.Entities.Action;

namespace Sparnarok.Api.Application.DTOs;

/// <summary>
/// Modelo de transferencia que describe una acción o ataque táctico de una criatura.
/// </summary>
/// <param name="Id">Identificador único de la acción.</param>
/// <param name="CreatureId">Identificador de la criatura poseedora.</param>
/// <param name="Name">Nombre descriptivo de la acción.</param>
/// <param name="Description">Descripción del efecto.</param>
/// <param name="Type">Tipo dentro de la economía de combate (Action, BonusAction, etc.).</param>
/// <param name="IsAttack">Indica si realiza tirada de ataque.</param>
/// <param name="AttackModifierAbility">Característica empleada para la tirada de ataque.</param>
/// <param name="ReachOrRangeFeet">Alcance en pies.</param>
/// <param name="DamageFormula">Fórmula de daño (ej. '1d8', '2d6').</param>
/// <param name="DamageType">Tipo de daño infligido.</param>
/// <param name="AssociatedMasteryId">Identificador de la maestría asociada opcional.</param>
/// <param name="AssociatedMasteryName">Nombre de la maestría asociada si existe.</param>
public record ActionResponseDto(
    Guid Id,
    Guid CreatureId,
    string Name,
    string Description,
    ActionType Type,
    bool IsAttack,
    AbilityScoreType? AttackModifierAbility,
    int ReachOrRangeFeet,
    string DamageFormula,
    DamageType? DamageType,
    Guid? AssociatedMasteryId,
    string? AssociatedMasteryName)
{
    /// <summary>
    /// Mapea una entidad <see cref="Action"/> a su DTO correspondiente.
    /// </summary>
    /// <param name="entity">Entidad de dominio.</param>
    /// <returns>Instancia de <see cref="ActionResponseDto"/>.</returns>
    public static ActionResponseDto FromEntity(Action entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return new ActionResponseDto(
            entity.Id,
            entity.CreatureId,
            entity.Name,
            entity.Description,
            entity.Type,
            entity.IsAttack,
            entity.AttackModifierAbility,
            entity.ReachOrRangeFeet,
            entity.DamageFormula,
            entity.DamageType,
            entity.AssociatedMasteryId,
            entity.AssociatedMastery?.Name);
    }
}
