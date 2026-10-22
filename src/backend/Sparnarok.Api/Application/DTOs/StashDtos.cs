using Sparnarok.Api.Domain.Entities;

namespace Sparnarok.Api.Application.DTOs;

/// <summary>
/// DTO de respuesta para un objeto almacenado en el alijo de la Party.
/// </summary>
public record StashItemResponseDto(
    Guid Id,
    Guid PartyId,
    string Name,
    int Quantity,
    decimal Weight,
    string Description,
    string Rarity,
    string ItemType,
    bool IsOfficialSrd,
    string? ReferenceIndex,
    Guid? AssignedCreatureId,
    DateTime CreatedAt
)
{
    /// <summary>
    /// Proyecta una entidad <see cref="StashItem"/> a su DTO de respuesta.
    /// </summary>
    public static StashItemResponseDto FromEntity(StashItem item) =>
        new(
            item.Id,
            item.PartyId,
            item.Name,
            item.Quantity,
            item.Weight,
            item.Description,
            item.Rarity,
            item.ItemType,
            item.IsOfficialSrd,
            item.ReferenceIndex,
            item.AssignedCreatureId,
            item.CreatedAt
        );
}

/// <summary>
/// DTO de respuesta con el inventario completo y peso acumulado del alijo de la Party.
/// </summary>
public record PartyStashResponseDto(
    Guid PartyId,
    string PartyName,
    decimal TotalWeight,
    IReadOnlyList<StashItemResponseDto> Items
);

/// <summary>
/// Solicitud de creación de un nuevo objeto en el alijo de la Party.
/// </summary>
public record CreateStashItemRequest(
    string Name,
    int Quantity,
    decimal Weight,
    string? Description,
    string? Rarity,
    string? ItemType,
    bool IsOfficialSrd = false,
    string? ReferenceIndex = null,
    Guid? AssignedCreatureId = null
);

/// <summary>
/// Solicitud de actualización de un objeto del alijo.
/// </summary>
public record UpdateStashItemRequest(
    string Name,
    int Quantity,
    decimal Weight,
    string? Description,
    string? Rarity,
    string? ItemType,
    Guid? AssignedCreatureId = null
);
