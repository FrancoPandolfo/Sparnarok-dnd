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
    /// Persiste los cambios pendientes en el almacenamiento.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
