using Sparnarok.Api.Domain.Entities;

namespace Sparnarok.Api.Domain.Entities;

/// <summary>
/// Entidad de dominio que modela un grupo estructurado de aventureros o personajes de jugadores (Party).
/// Mantiene una relación Many-to-Many con criaturas que son Personajes de Jugadores (IsPlayerCharacter == true).
/// </summary>
public class Party
{
    private readonly List<Creature> _members = [];
    private readonly List<StashItem> _stashItems = [];

    /// <summary>Identificador único de la Party.</summary>
    public Guid Id { get; private set; }

    /// <summary>Nombre representativo del grupo de héroes (ej. 'Los Protectores de la Costa').</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Identificador del usuario propietario y creador de la Party.</summary>
    public Guid OwnerUserId { get; private set; }

    /// <summary>Fecha y hora UTC de creación de la Party.</summary>
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    /// <summary>Colección de héroes y personajes que componen la Party.</summary>
    public IReadOnlyCollection<Creature> Members => _members.AsReadOnly();

    /// <summary>Colección de objetos y tesoros en el alijo compartido (Collaborative Party Stash).</summary>
    public IReadOnlyCollection<StashItem> StashItems => _stashItems.AsReadOnly();

    /// <summary>Peso total acumulado en libras (lbs) de todos los objetos en el alijo de la Party.</summary>
    public decimal TotalStashWeight => _stashItems.Sum(i => i.Weight * i.Quantity);

    /// <summary>Constructor protegido para Entity Framework Core.</summary>
    protected Party() { }

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="Party"/>.
    /// </summary>
    public Party(Guid id, string name, Guid ownerUserId)
    {
        Id = id;
        SetName(name);
        OwnerUserId = ownerUserId;
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Actualiza el nombre de la Party asegurando su no vacuidad.
    /// </summary>
    public void SetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("El nombre de la Party no puede estar vacío.", nameof(name));
        }

        Name = name.Trim();
    }

    /// <summary>
    /// Añade un personaje a la Party, garantizando que sea un Personaje de Jugador (PC).
    /// </summary>
    public void AddMember(Creature creature)
    {
        ArgumentNullException.ThrowIfNull(creature);

        if (!creature.IsPlayerCharacter)
        {
            throw new InvalidOperationException($"La criatura '{creature.Name}' no es un Personaje de Jugador y no puede formar parte de una Party.");
        }

        if (!_members.Any(m => m.Id == creature.Id))
        {
            _members.Add(creature);
        }
    }

    /// <summary>
    /// Remueve un integrante de la Party por su identificador.
    /// </summary>
    public void RemoveMember(Guid creatureId)
    {
        _members.RemoveAll(m => m.Id == creatureId);
    }

    /// <summary>
    /// Despeja todos los integrantes actuales de la Party.
    /// </summary>
    public void ClearMembers()
    {
        _members.Clear();
    }

    /// <summary>
    /// Añade un objeto al alijo compartido de la Party.
    /// </summary>
    public void AddStashItem(StashItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _stashItems.Add(item);
    }

    /// <summary>
    /// Remueve un objeto del alijo compartido de la Party por su identificador.
    /// </summary>
    public void RemoveStashItem(Guid itemId)
    {
        _stashItems.RemoveAll(i => i.Id == itemId);
    }
}

