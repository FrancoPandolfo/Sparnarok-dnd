using Sparnarok.Api.Domain.Entities;
using Sparnarok.Api.Domain.Enums;

namespace Sparnarok.Api.Application.Interfaces;

/// <summary>
/// Contrato de repositorio para la consulta de propiedades de maestría de armas.
/// </summary>
public interface IWeaponMasteryRepository
{
    /// <summary>
    /// Obtiene todas las maestrías de armas disponibles.
    /// </summary>
    Task<IReadOnlyList<WeaponMastery>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene una maestría por su identificador único.
    /// </summary>
    Task<WeaponMastery?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene una maestría por su propiedad formal (2024).
    /// </summary>
    Task<WeaponMastery?> GetByPropertyAsync(MasteryProperty property, CancellationToken cancellationToken = default);
}
