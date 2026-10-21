using Sparnarok.Api.Domain.Entities;
using Action = Sparnarok.Api.Domain.Entities.Action;

namespace Sparnarok.Api.Application.Interfaces;

/// <summary>
/// Contrato de repositorio para la gestión y persistencia del agregado <see cref="Creature"/>.
/// </summary>
public interface ICreatureRepository
{
    /// <summary>
    /// Obtiene todas las criaturas registradas con sus acciones y maestrías.
    /// </summary>
    Task<IReadOnlyList<Creature>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene una criatura por su identificador único incluyendo sus acciones y maestrías asociadas.
    /// </summary>
    Task<Creature?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Agrega una nueva criatura al almacén de persistencia.
    /// </summary>
    Task AddAsync(Creature creature, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registra una nueva acción táctica para una criatura en el almacén de persistencia.
    /// </summary>
    Task AddActionAsync(Action action, CancellationToken cancellationToken = default);

    /// <summary>
    /// Actualiza el estado persistido de una criatura.
    /// </summary>
    Task UpdateAsync(Creature creature, CancellationToken cancellationToken = default);

    /// <summary>
    /// Elimina una criatura del almacén de persistencia.
    /// </summary>
    Task DeleteAsync(Creature creature, CancellationToken cancellationToken = default);

    /// <summary>
    /// Guarda los cambios pendientes en el contexto de persistencia.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
