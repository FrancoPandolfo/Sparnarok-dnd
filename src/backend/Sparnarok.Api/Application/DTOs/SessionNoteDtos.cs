using Sparnarok.Api.Domain.Entities;

namespace Sparnarok.Api.Application.DTOs;

/// <summary>
/// DTO de respuesta para una nota de sesión o apunte de campaña.
/// </summary>
public record SessionNoteResponseDto(
    Guid Id,
    Guid PartyId,
    string Title,
    string Content,
    DateTime CreatedAt,
    DateTime UpdatedAt
)
{
    /// <summary>
    /// Proyecta una entidad <see cref="SessionNote"/> a su DTO de respuesta.
    /// </summary>
    public static SessionNoteResponseDto FromEntity(SessionNote note) =>
        new(
            note.Id,
            note.PartyId,
            note.Title,
            note.Content,
            note.CreatedAt,
            note.UpdatedAt
        );
}

/// <summary>
/// Solicitud de creación de una nueva nota de sesión.
/// </summary>
public record CreateSessionNoteRequest(
    string Title,
    string Content
);

/// <summary>
/// Solicitud de actualización de una nota de sesión existente.
/// </summary>
public record UpdateSessionNoteRequest(
    string Title,
    string Content
);
