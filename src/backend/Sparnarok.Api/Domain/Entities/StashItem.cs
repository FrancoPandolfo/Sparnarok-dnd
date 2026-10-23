namespace Sparnarok.Api.Domain.Entities;

/// <summary>
/// Representa un objeto o equipo dentro del alijo compartido de una Party (Collaborative Party Stash).
/// Soporta tanto objetos oficiales del SRD (vía D&amp;D 5e API) como creaciones personalizadas (Homebrew).
/// </summary>
public class StashItem
{
    /// <summary>Identificador único del objeto en el alijo.</summary>
    public Guid Id { get; private set; }

    /// <summary>Identificador de la Party a la que pertenece este alijo.</summary>
    public Guid PartyId { get; private set; }

    /// <summary>Nombre representativo del objeto (ej. 'Bag of Holding', 'Longsword', 'Potion of Healing').</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Cantidad de unidades del objeto en el alijo.</summary>
    public int Quantity { get; private set; } = 1;

    /// <summary>Peso unitario del objeto en libras (lbs). Puede ser 0 para objetos ingrávidos.</summary>
    public decimal Weight { get; private set; }

    /// <summary>Descripción detallada, propiedades mágicas o notas del objeto.</summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>Rareza del objeto (Common, Uncommon, Rare, Very Rare, Legendary, Artifact, None).</summary>
    public string Rarity { get; private set; } = "Common";

    /// <summary>Categoría o tipo de objeto (Weapon, Armor, Wondrous Item, Potion, Adventuring Gear, etc.).</summary>
    public string ItemType { get; private set; } = "Adventuring Gear";

    /// <summary>Indica si el objeto fue importado desde el SRD oficial o es Homebrew.</summary>
    public bool IsOfficialSrd { get; private set; }

    /// <summary>Índice identificador en la API oficial de D&amp;D 5e (ej. 'bag-of-holding'). Nulo si es homebrew.</summary>
    public string? ReferenceIndex { get; private set; }

    /// <summary>Identificador opcional del personaje de la Party al que está asignado o sintonizado el objeto.</summary>
    public Guid? AssignedCreatureId { get; private set; }

    /// <summary>Indica si el objeto ha sido identificado por el Dungeon Master (revela propiedades mágicas completas).</summary>
    public bool IsIdentified { get; private set; } = true;

    /// <summary>Fecha y hora UTC de adición del objeto al alijo.</summary>
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    /// <summary>Constructor protegido para Entity Framework Core.</summary>
    protected StashItem() { }

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="StashItem"/>.
    /// </summary>
    public StashItem(
        Guid id,
        Guid partyId,
        string name,
        int quantity,
        decimal weight,
        string description,
        string rarity = "Common",
        string itemType = "Adventuring Gear",
        bool isOfficialSrd = false,
        string? referenceIndex = null,
        Guid? assignedCreatureId = null,
        bool isIdentified = true)
    {
        Id = id;
        PartyId = partyId;
        SetName(name);
        SetQuantity(quantity);
        SetWeight(weight);
        Description = description?.Trim() ?? string.Empty;
        Rarity = string.IsNullOrWhiteSpace(rarity) ? "Common" : rarity.Trim();
        ItemType = string.IsNullOrWhiteSpace(itemType) ? "Adventuring Gear" : itemType.Trim();
        IsOfficialSrd = isOfficialSrd;
        ReferenceIndex = referenceIndex?.Trim();
        AssignedCreatureId = assignedCreatureId;
        IsIdentified = isIdentified;
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Establece el nombre del objeto asegurando que no sea nulo ni vacío.
    /// </summary>
    public void SetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("El nombre del objeto no puede estar vacío.", nameof(name));
        }

        Name = name.Trim();
    }

    /// <summary>
    /// Modifica la cantidad de objetos en el alijo (mínimo 1).
    /// </summary>
    public void SetQuantity(int quantity)
    {
        if (quantity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "La cantidad de objetos debe ser al menos 1.");
        }

        Quantity = quantity;
    }

    /// <summary>
    /// Modifica el peso unitario del objeto (no puede ser negativo).
    /// </summary>
    public void SetWeight(decimal weight)
    {
        if (weight < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(weight), "El peso unitario no puede ser negativo.");
        }

        Weight = weight;
    }

    /// <summary>
    /// Actualiza los atributos mutables del objeto de forma atómica.
    /// </summary>
    public void UpdateDetails(string name, int quantity, decimal weight, string description, string rarity, string itemType)
    {
        SetName(name);
        SetQuantity(quantity);
        SetWeight(weight);
        Description = description?.Trim() ?? string.Empty;
        Rarity = string.IsNullOrWhiteSpace(rarity) ? "Common" : rarity.Trim();
        ItemType = string.IsNullOrWhiteSpace(itemType) ? "Adventuring Gear" : itemType.Trim();
    }

    /// <summary>
    /// Asigna o desasigna el objeto a un personaje integrante de la Party.
    /// </summary>
    public void AssignToCreature(Guid? creatureId)
    {
        AssignedCreatureId = creatureId;
    }

    /// <summary>
    /// Identifica el objeto mágico o desconocido. Acción exclusiva del Dungeon Master.
    /// </summary>
    public void Identify()
    {
        IsIdentified = true;
    }

    /// <summary>
    /// Establece explícitamente el estado de identificación del objeto.
    /// </summary>
    public void SetIdentified(bool isIdentified)
    {
        IsIdentified = isIdentified;
    }
}
