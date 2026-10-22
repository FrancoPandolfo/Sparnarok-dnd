using Sparnarok.Api.Domain.Entities;

namespace Sparnarok.Api.Application.Interfaces;

/// <summary>
/// Contrato de repositorio para la persistencia y consulta de entidades <see cref="Encounter"/>.
/// </summary>
public interface IEncounterRepository
{
    /// <summary>
    /// Obtiene todos los encuentros tácticos pertenecientes al usuario especificado con sus criaturas cargadas.
    /// </summary>
    Task<IReadOnlyList<Encounter>> GetAllByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene un encuentro por su identificador único junto a sus criaturas asociadas.
    /// </summary>
    Task<Encounter?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Agrega un nuevo encuentro al repositorio.
    /// </summary>
    Task AddAsync(Encounter encounter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Actualiza un encuentro existente.
    /// </summary>
    Task UpdateAsync(Encounter encounter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Elimina un encuentro del repositorio.
    /// </summary>
    Task DeleteAsync(Encounter encounter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persiste los cambios pendientes en el almacenamiento.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
