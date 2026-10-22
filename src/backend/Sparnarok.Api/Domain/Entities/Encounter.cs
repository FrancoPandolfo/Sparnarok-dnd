namespace Sparnarok.Api.Domain.Entities;

/// <summary>
/// Entidad de dominio que modela un encuentro táctico preconfigurado de criaturas/monstruos para combate.
/// Mantiene una relación Many-to-Many con criaturas del bestiario.
/// </summary>
public class Encounter
{
    private readonly List<Creature> _creatures = [];

    /// <summary>Identificador único del Encuentro.</summary>
    public Guid Id { get; private set; }

    /// <summary>Nombre del encuentro (ej. 'Emboscada de Bugbears en el Bosque').</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Identificador del usuario propietario y creador del Encuentro.</summary>
    public Guid OwnerUserId { get; private set; }

    /// <summary>Notas tácticas u observaciones sobre la dificultad del encuentro (opcional).</summary>
    public string? DifficultyNotes { get; private set; }

    /// <summary>Fecha y hora UTC de creación del Encuentro.</summary>
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    /// <summary>Colección de monstruos y criaturas que componen el Encuentro.</summary>
    public IReadOnlyCollection<Creature> Creatures => _creatures.AsReadOnly();

    /// <summary>Constructor protegido para Entity Framework Core.</summary>
    protected Encounter() { }

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="Encounter"/>.
    /// </summary>
    public Encounter(Guid id, string name, Guid ownerUserId, string? difficultyNotes = null)
    {
        Id = id;
        SetName(name);
        OwnerUserId = ownerUserId;
        DifficultyNotes = difficultyNotes?.Trim();
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Actualiza el nombre del Encuentro validando su contenido.
    /// </summary>
    public void SetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("El nombre del Encuentro no puede estar vacío.", nameof(name));
        }

        Name = name.Trim();
    }

    /// <summary>
    /// Actualiza las notas tácticas o de dificultad del Encuentro.
    /// </summary>
    public void SetDifficultyNotes(string? notes)
    {
        DifficultyNotes = notes?.Trim();
    }

    /// <summary>
    /// Añade una criatura/monstruo al Encuentro.
    /// </summary>
    public void AddCreature(Creature creature)
    {
        ArgumentNullException.ThrowIfNull(creature);

        if (!_creatures.Any(c => c.Id == creature.Id))
        {
            _creatures.Add(creature);
        }
    }

    /// <summary>
    /// Remueve una criatura del Encuentro por su identificador.
    /// </summary>
    public void RemoveCreature(Guid creatureId)
    {
        _creatures.RemoveAll(c => c.Id == creatureId);
    }

    /// <summary>
    /// Despeja todos los monstruos actuales del Encuentro.
    /// </summary>
    public void ClearCreatures()
    {
        _creatures.Clear();
    }
}
