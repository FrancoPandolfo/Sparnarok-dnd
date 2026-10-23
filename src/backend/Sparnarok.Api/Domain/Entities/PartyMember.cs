namespace Sparnarok.Api.Domain.Entities;

/// <summary>
/// Entidad de dominio intermedia que modela la membresía y el rol de un usuario dentro de una Party específica.
/// Permite el modelo de Identidad Dual: un usuario posee un <see cref="ApplicationUser.GlobalNickname"/> global
/// pero asume un <see cref="InGameName"/> y un <see cref="Role"/> contextual en cada grupo o campaña.
/// </summary>
public class PartyMember
{
    /// <summary>Identificador único del registro de membresía.</summary>
    public Guid Id { get; private set; }

    /// <summary>Identificador de la Party a la que pertenece.</summary>
    public Guid PartyId { get; private set; }

    /// <summary>Identificador del usuario miembro.</summary>
    public Guid UserId { get; private set; }

    /// <summary>Rol asignado dentro de la Party ('DungeonMaster' o 'Player').</summary>
    public string Role { get; private set; } = "Player";

    /// <summary>Nombre dentro de la partida (apodo del DM o nombre del personaje del jugador).</summary>
    public string InGameName { get; private set; } = string.Empty;

    /// <summary>Fecha y hora de incorporación al grupo en UTC.</summary>
    public DateTime JoinedAt { get; private set; } = DateTime.UtcNow;

    /// <summary>Navegación EF Core hacia la Party.</summary>
    public Party? Party { get; private set; }

    /// <summary>Navegación EF Core hacia el Usuario.</summary>
    public ApplicationUser? User { get; private set; }

    /// <summary>Constructor protegido requerido por Entity Framework Core.</summary>
    protected PartyMember() { }

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="PartyMember"/>.
    /// </summary>
    /// <param name="id">Identificador único o Guid.Empty para autogenerar.</param>
    /// <param name="partyId">Identificador de la Party.</param>
    /// <param name="userId">Identificador del usuario.</param>
    /// <param name="role">Rol ('DungeonMaster' o 'Player').</param>
    /// <param name="inGameName">Nombre o alias en partida.</param>
    public PartyMember(Guid id, Guid partyId, Guid userId, string role, string inGameName)
    {
        if (partyId == Guid.Empty)
            throw new ArgumentException("El identificador de la Party no puede estar vacío.", nameof(partyId));

        if (userId == Guid.Empty)
            throw new ArgumentException("El identificador del usuario no puede estar vacío.", nameof(userId));

        Id = id;
        PartyId = partyId;
        UserId = userId;
        SetRole(role);
        SetInGameName(inGameName);
        JoinedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Establece y valida el rol del usuario en la Party.
    /// </summary>
    /// <param name="role">Rol a asignar ('DungeonMaster' o 'Player').</param>
    public void SetRole(string role)
    {
        var trimmed = role?.Trim();
        if (trimmed != "DungeonMaster" && trimmed != "Player")
        {
            throw new ArgumentException("El rol debe ser estrictamente 'DungeonMaster' o 'Player'.", nameof(role));
        }

        Role = trimmed;
    }

    /// <summary>
    /// Actualiza el nombre del usuario dentro de la partida.
    /// </summary>
    /// <param name="inGameName">Nuevo nombre en partida.</param>
    public void SetInGameName(string inGameName)
    {
        if (string.IsNullOrWhiteSpace(inGameName))
        {
            throw new ArgumentException("El nombre en partida (InGameName) no puede estar vacío.", nameof(inGameName));
        }

        InGameName = inGameName.Trim();
    }
}
