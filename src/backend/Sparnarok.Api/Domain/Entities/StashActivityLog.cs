namespace Sparnarok.Api.Domain.Entities;

/// <summary>
/// Representa un registro de auditoría de actividad (Audit Log) para el alijo y fondos de una Party.
/// </summary>
public class StashActivityLog
{
    /// <summary>Identificador único del registro de auditoría.</summary>
    public Guid Id { get; private set; }

    /// <summary>Identificador de la Party asociada.</summary>
    public Guid PartyId { get; private set; }

    /// <summary>Identificador del usuario que realizó la acción si estaba autenticado.</summary>
    public Guid? UserId { get; private set; }

    /// <summary>Nombre representativo del usuario o rol ejecutor (ej. 'Dungeon Master', 'Gimli').</summary>
    public string UserDisplayName { get; private set; } = string.Empty;

    /// <summary>Tipo de acción realizada (ej. 'ItemAdded', 'ItemDeleted', 'FundsModified', 'CraftingQueued', 'DowntimeAdvanced', 'CraftingCompleted').</summary>
    public string ActionType { get; private set; } = string.Empty;

    /// <summary>Descripción detallada de la transacción o cambio efectuado.</summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>Marca de tiempo UTC en la que ocurrió el evento.</summary>
    public DateTime TimestampUtc { get; private set; } = DateTime.UtcNow;

    /// <summary>Constructor protegido para Entity Framework Core.</summary>
    protected StashActivityLog() { }

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="StashActivityLog"/>.
    /// </summary>
    public StashActivityLog(
        Guid id,
        Guid partyId,
        Guid? userId,
        string userDisplayName,
        string actionType,
        string description)
    {
        if (string.IsNullOrWhiteSpace(actionType))
        {
            throw new ArgumentException("El tipo de acción no puede estar vacío.", nameof(actionType));
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("La descripción del evento no puede estar vacía.", nameof(description));
        }

        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        PartyId = partyId;
        UserId = userId;
        UserDisplayName = string.IsNullOrWhiteSpace(userDisplayName) ? "Aventurero" : userDisplayName.Trim();
        ActionType = actionType.Trim();
        Description = description.Trim();
        TimestampUtc = DateTime.UtcNow;
    }
}
