using Sparnarok.Api.Domain.Entities;
using Sparnarok.Api.Domain.Enums;

namespace Sparnarok.Api.Application.DTOs;

/// <summary>
/// Modelo de transferencia que representa una propiedad de maestría de armas de D&amp;D 5e (2024).
/// </summary>
/// <param name="Id">Identificador único de la maestría.</param>
/// <param name="Property">Propiedad formal (ej. Cleave, Graze, Topple).</param>
/// <param name="Name">Nombre representativo.</param>
/// <param name="Description">Descripción operativa del efecto.</param>
/// <param name="PrerequisiteWeapon">Requisito de tipo de arma.</param>
public record WeaponMasteryResponseDto(
    Guid Id,
    MasteryProperty Property,
    string Name,
    string Description,
    string PrerequisiteWeapon)
{
    /// <summary>
    /// Mapea una entidad <see cref="WeaponMastery"/> a su DTO de respuesta.
    /// </summary>
    /// <param name="entity">Entidad de dominio.</param>
    /// <returns>Instancia de <see cref="WeaponMasteryResponseDto"/>.</returns>
    public static WeaponMasteryResponseDto FromEntity(WeaponMastery entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return new WeaponMasteryResponseDto(
            entity.Id,
            entity.Property,
            entity.Name,
            entity.Description,
            entity.PrerequisiteWeapon);
    }
}
