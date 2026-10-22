namespace Sparnarok.Api.Application.DTOs;

/// <summary>
/// Resumen simplificado de un objeto o ítem de referencia obtenido de la API externa D&amp;D 5e (SRD).
/// </summary>
/// <param name="Index">Identificador único o slug del objeto en el SRD (ej. 'bag-of-holding', 'dagger').</param>
/// <param name="Name">Nombre oficial del objeto.</param>
/// <param name="Url">Ruta de acceso canónica del recurso en la API externa.</param>
public record ReferenceItemSummaryDto(
    string Index,
    string Name,
    string Url
);

/// <summary>
/// Detalle completo de un objeto de referencia mapeado para compatibilidad directa con <see cref="Sparnarok.Api.Domain.Entities.StashItem"/>.
/// </summary>
/// <param name="Index">Identificador único o slug en el SRD.</param>
/// <param name="Name">Nombre oficial del objeto.</param>
/// <param name="Weight">Peso en libras (lbs). Si la API devuelve nulo o no aplica, se normaliza de forma segura a 0.</param>
/// <param name="Description">Descripción y reglas del objeto.</param>
/// <param name="Rarity">Rareza del objeto (Common, Uncommon, Rare, Very Rare, Legendary, Artifact).</param>
/// <param name="ItemType">Categoría del objeto (Weapon, Armor, Wondrous Item, Potion, Adventuring Gear, etc.).</param>
/// <param name="IsOfficialSrd">Indica que el objeto proviene del SRD oficial.</param>
/// <param name="Cost">Costo monetario estándar si aplica (ej. '2 gp').</param>
public record ReferenceEquipmentDetailDto(
    string Index,
    string Name,
    decimal Weight,
    string Description,
    string Rarity,
    string ItemType,
    bool IsOfficialSrd,
    string? Cost
);
