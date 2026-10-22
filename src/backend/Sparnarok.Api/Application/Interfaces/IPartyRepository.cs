using Sparnarok.Api.Domain.Entities;

namespace Sparnarok.Api.Application.Interfaces;

/// <summary>
/// Contrato de repositorio para la persistencia y consulta de entidades <see cref="Party"/>.
/// </summary>
public interface IPartyRepository
{
    /// <summary>
    /// Obtiene todas las partys pertenecientes al usuario especificado con sus integrantes cargados.
    /// </summary>
    Task<IReadOnlyList<Party>> GetAllByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene una party por su identificador único junto a sus integrantes.
    /// </summary>
    Task<Party?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Agrega una nueva party al repositorio.
    /// </summary>
    Task AddAsync(Party party, CancellationToken cancellationToken = default);

    /// <summary>
    /// Actualiza una party existente.
    /// </summary>
    Task UpdateAsync(Party party, CancellationToken cancellationToken = default);

    /// <summary>
    /// Elimina una party del repositorio.
    /// </summary>
    Task DeleteAsync(Party party, CancellationToken cancellationToken = default);

    /// <summary>
    /// Agrega un nuevo objeto al alijo compartido de la Party.
    /// </summary>
    Task AddStashItemAsync(StashItem item, CancellationToken cancellationToken = default);

    /// <summary>
    /// Remueve un objeto del alijo compartido de la Party.
    /// </summary>
    Task RemoveStashItemAsync(StashItem item, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene una party por su ID incluyendo miembros, alijo, cola de fabricación y registros de actividad.
    /// </summary>
    Task<Party?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Agrega un nuevo trabajo a la cola de fabricación de la Party.
    /// </summary>
    Task AddCraftingItemAsync(CraftingQueueItem item, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene los trabajos de fabricación de una Party ordenados por fecha de creación.
    /// </summary>
    Task<IReadOnlyList<CraftingQueueItem>> GetCraftingQueueAsync(Guid partyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registra un nuevo evento en el historial de auditoría de la Party.
    /// </summary>
    Task AddActivityLogAsync(StashActivityLog log, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene los registros de auditoría de una Party ordenados cronológicamente de forma descendente.
    /// </summary>
    Task<IReadOnlyList<StashActivityLog>> GetActivityLogsAsync(Guid partyId, int limit = 50, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persiste los cambios pendientes en el almacenamiento.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
