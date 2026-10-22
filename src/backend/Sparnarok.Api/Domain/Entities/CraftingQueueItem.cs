using System.Text.Json.Serialization;

namespace Sparnarok.Api.Domain.Entities;

/// <summary>
/// Representa un objeto en proceso de fabricación durante el tiempo de descanso (Downtime Crafting).
/// </summary>
public class CraftingQueueItem
{
    /// <summary>Identificador único del trabajo de fabricación.</summary>
    public Guid Id { get; private set; }

    /// <summary>Identificador de la Party propietaria.</summary>
    public Guid PartyId { get; private set; }

    /// <summary>Nombre del objeto a fabricar (ej. 'Potion of Healing', 'Cloak of Protection').</summary>
    public string ItemName { get; private set; } = string.Empty;

    /// <summary>Tipo o categoría del objeto (ej. 'Potion', 'Scroll', 'Wondrous Items').</summary>
    public string ItemType { get; private set; } = string.Empty;

    /// <summary>Rareza del objeto (Common, Uncommon, Rare, Very Rare, Legendary, Artifact).</summary>
    public string Rarity { get; private set; } = "Common";

    /// <summary>Costo en oro (PO) descontado del Party Fund para iniciar la fabricación.</summary>
    public decimal GoldCost { get; private set; }

    /// <summary>Días de downtime requeridos originalmente para completar la fabricación.</summary>
    public int DaysRequired { get; private set; }

    /// <summary>Días restantes de downtime para que el objeto esté listo.</summary>
    public int DaysRemaining { get; private set; }

    /// <summary>Peso estimado en libras del objeto terminado.</summary>
    public decimal Weight { get; private set; }

    /// <summary>Descripción y propiedades mágicas o mecánicas del objeto.</summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>Indica si el objeto proviene del catálogo oficial SRD 2024.</summary>
    public bool IsOfficialSrd { get; private set; }

    /// <summary>Índice o slug de referencia en la API externa de D&amp;D 5e si aplica.</summary>
    public string? ReferenceIndex { get; private set; }

    /// <summary>Nombre del personaje o artesano a cargo de la fabricación.</summary>
    public string? CrafterName { get; private set; }

    /// <summary>Estado actual de la fabricación: 'InProgress', 'Completed', 'Cancelled'.</summary>
    public string Status { get; private set; } = "InProgress";

    /// <summary>Fecha y hora UTC en la que se encoló la fabricación.</summary>
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;

    /// <summary>Constructor protegido para Entity Framework Core.</summary>
    protected CraftingQueueItem() { }

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="CraftingQueueItem"/>.
    /// </summary>
    public CraftingQueueItem(
        Guid id,
        Guid partyId,
        string itemName,
        string itemType,
        string rarity,
        decimal goldCost,
        int daysRequired,
        decimal weight,
        string description,
        bool isOfficialSrd,
        string? referenceIndex,
        string? crafterName)
    {
        if (string.IsNullOrWhiteSpace(itemName))
        {
            throw new ArgumentException("El nombre del objeto a fabricar no puede estar vacío.", nameof(itemName));
        }

        if (daysRequired < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(daysRequired), "Los días de downtime requeridos deben ser al menos 1.");
        }

        if (goldCost < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(goldCost), "El costo en oro no puede ser negativo.");
        }

        if (weight < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(weight), "El peso no puede ser negativo.");
        }

        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        PartyId = partyId;
        ItemName = itemName.Trim();
        ItemType = string.IsNullOrWhiteSpace(itemType) ? "Adventuring Gear" : itemType.Trim();
        Rarity = string.IsNullOrWhiteSpace(rarity) ? "Common" : rarity.Trim();
        GoldCost = goldCost;
        DaysRequired = daysRequired;
        DaysRemaining = daysRequired;
        Weight = weight;
        Description = description?.Trim() ?? string.Empty;
        IsOfficialSrd = isOfficialSrd;
        ReferenceIndex = referenceIndex;
        CrafterName = string.IsNullOrWhiteSpace(crafterName) ? null : crafterName.Trim();
        Status = "InProgress";
        CreatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Avanza 1 día de downtime en el progreso de fabricación.
    /// Retorna true si el objeto completó su fabricación (alcanzó 0 días restantes).
    /// </summary>
    public bool AdvanceDay()
    {
        if (Status != "InProgress")
        {
            return false;
        }

        DaysRemaining = Math.Max(0, DaysRemaining - 1);
        if (DaysRemaining == 0)
        {
            Status = "Completed";
            return true;
        }

        return false;
    }

    /// <summary>
    /// Convierte el objeto terminado en una entidad <see cref="StashItem"/> para agregarlo al alijo principal.
    /// </summary>
    public StashItem ToStashItem()
    {
        return new StashItem(
            Guid.NewGuid(),
            PartyId,
            ItemName,
            1,
            Weight,
            Description,
            Rarity,
            ItemType,
            IsOfficialSrd,
            ReferenceIndex,
            null
        );
    }
}
