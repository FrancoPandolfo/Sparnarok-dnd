using Sparnarok.Api.Domain.Entities;

namespace Sparnarok.Api.Application.Interfaces;

/// <summary>
/// Contrato de repositorio para la persistencia y consulta de notas de sesión (<see cref="SessionNote"/>).
/// </summary>
public interface ISessionNoteRepository
{
    /// <summary>
    /// Obtiene una nota por su identificador único.
    /// </summary>
    Task<SessionNote?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Obtiene todas las notas asociadas a una Party, ordenadas cronológicamente de forma descendente.
    /// </summary>
    Task<IReadOnlyList<SessionNote>> GetAllByPartyIdAsync(Guid partyId, CancellationToken ct = default);

    /// <summary>
    /// Obtiene todas las notas privadas asociadas a una Party pertenecientes a un usuario específico (Fase 3.7).
    /// </summary>
    Task<IReadOnlyList<SessionNote>> GetAllByPartyAndOwnerAsync(Guid partyId, Guid ownerUserId, CancellationToken ct = default);

    /// <summary>
    /// Agrega una nueva nota de sesión.
    /// </summary>
    Task AddAsync(SessionNote note, CancellationToken ct = default);

    /// <summary>
    /// Actualiza una nota de sesión existente.
    /// </summary>
    Task UpdateAsync(SessionNote note, CancellationToken ct = default);

    /// <summary>
    /// Elimina una nota de sesión.
    /// </summary>
    Task DeleteAsync(SessionNote note, CancellationToken ct = default);

    /// <summary>
    /// Persiste los cambios pendientes en la base de datos.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
