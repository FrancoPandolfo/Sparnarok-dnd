namespace Sparnarok.Api.Domain.Entities;

/// <summary>
/// Representa una nota de sesión de juego o apunte narrativo redactado exclusivamente por el Dungeon Master.
/// </summary>
public class SessionNote
{
    /// <summary>Identificador único de la nota.</summary>
    public Guid Id { get; private set; }

    /// <summary>Identificador de la Party a la que pertenece la nota.</summary>
    public Guid PartyId { get; private set; }

    /// <summary>Identificador del usuario propietario y autor de la nota de sesión privada (Fase 3.7).</summary>
    public Guid OwnerUserId { get; private set; }

    /// <summary>Título descriptivo de la nota o sesión (ej. 'Sesión 4: El templo subterráneo').</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>Contenido en formato Markdown con apuntes de lore, trampas, recompensas o eventos.</summary>
    public string Content { get; private set; } = string.Empty;

    /// <summary>Fecha y hora de creación de la nota en UTC.</summary>
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    /// <summary>Fecha y hora de la última modificación en UTC.</summary>
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

    /// <summary>Navegación EF Core hacia la Party.</summary>
    public Party? Party { get; private set; }

    /// <summary>Navegación EF Core hacia el Usuario propietario.</summary>
    public ApplicationUser? OwnerUser { get; private set; }

    /// <summary>Constructor protegido para Entity Framework Core.</summary>
    protected SessionNote() { }

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="SessionNote"/>.
    /// </summary>
    /// <param name="id">Identificador único o Guid.Empty para autogenerar.</param>
    /// <param name="partyId">Identificador de la Party propietaria.</param>
    /// <param name="ownerUserId">Identificador del usuario autor y propietario de la nota.</param>
    /// <param name="title">Título de la nota.</param>
    /// <param name="content">Cuerpo en Markdown de la nota.</param>
    public SessionNote(Guid id, Guid partyId, Guid ownerUserId, string title, string content)
    {
        if (partyId == Guid.Empty)
        {
            throw new ArgumentException("El identificador de la Party no puede estar vacío.", nameof(partyId));
        }

        if (ownerUserId == Guid.Empty)
        {
            throw new ArgumentException("El identificador del usuario propietario no puede estar vacío.", nameof(ownerUserId));
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("El título de la nota de sesión no puede estar vacío.", nameof(title));
        }

        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        PartyId = partyId;
        OwnerUserId = ownerUserId;
        Title = title.Trim();
        Content = content?.Trim() ?? string.Empty;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Actualiza el título y contenido de la nota de sesión.
    /// </summary>
    /// <param name="newTitle">Nuevo título.</param>
    /// <param name="newContent">Nuevo cuerpo en Markdown.</param>
    public void Update(string newTitle, string newContent)
    {
        if (string.IsNullOrWhiteSpace(newTitle))
        {
            throw new ArgumentException("El título de la nota de sesión no puede estar vacío.", nameof(newTitle));
        }

        Title = newTitle.Trim();
        Content = newContent?.Trim() ?? string.Empty;
        UpdatedAt = DateTime.UtcNow;
    }
}
