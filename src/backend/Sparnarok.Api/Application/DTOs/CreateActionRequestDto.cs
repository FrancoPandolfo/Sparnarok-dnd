using Sparnarok.Api.Domain.Enums;

namespace Sparnarok.Api.Application.DTOs;

/// <summary>
/// Modelo de solicitud para registrar una nueva acción en una criatura.
/// </summary>
/// <param name="Name">Nombre descriptivo de la acción.</param>
/// <param name="Type">Tipo de acción dentro de la economía de combate.</param>
/// <param name="Description">Descripción operativa del efecto.</param>
/// <param name="IsAttack">Indica si realiza tirada de ataque contra CA.</param>
/// <param name="ReachOrRangeFeet">Alcance efectivo en pies (mínimo 0).</param>
/// <param name="DamageFormula">Fórmula de dados de daño (ej. '1d8', '2d6').</param>
/// <param name="DamageType">Tipo de daño infligido.</param>
/// <param name="AttackModifierAbility">Característica empleada para la tirada de ataque.</param>
/// <param name="AssociatedMasteryId">Identificador de la maestría de armas opcional a vincular.</param>
public record CreateActionRequestDto(
    string Name,
    ActionType Type,
    string Description = "",
    bool IsAttack = false,
    int ReachOrRangeFeet = 5,
    string DamageFormula = "",
    DamageType? DamageType = null,
    AbilityScoreType? AttackModifierAbility = null,
    Guid? AssociatedMasteryId = null);
