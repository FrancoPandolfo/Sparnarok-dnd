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
/// DTO de respuesta con el inventario completo, fondos y peso acumulado del alijo de la Party.
/// </summary>
public record PartyStashResponseDto(
    Guid PartyId,
    string PartyName,
    decimal TotalWeight,
    IReadOnlyList<StashItemResponseDto> Items,
    PartyFundDto? Funds = null,
    IReadOnlyList<CraftingQueueItemDto>? CraftingQueue = null
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

/// <summary>
/// DTO de fondos monetarios (Party Fund) de una Party.
/// </summary>
public record PartyFundDto(
    Guid PartyId,
    decimal CopperPieces,
    decimal SilverPieces,
    decimal ElectrumPieces,
    decimal GoldPieces,
    decimal PlatinumPieces,
    decimal TotalGoldEquivalent
)
{
    /// <summary>
    /// Proyecta una entidad <see cref="Party"/> a su DTO de fondos.
    /// </summary>
    public static PartyFundDto FromEntity(Party party) =>
        new(
            party.Id,
            party.CopperPieces,
            party.SilverPieces,
            party.ElectrumPieces,
            party.GoldPieces,
            party.PlatinumPieces,
            party.TotalGoldEquivalent
        );
}

/// <summary>
/// Solicitud de actualización de los fondos del Party Fund.
/// </summary>
public record UpdatePartyFundsRequest(
    decimal CopperPieces,
    decimal SilverPieces,
    decimal ElectrumPieces,
    decimal GoldPieces,
    decimal PlatinumPieces
);

/// <summary>
/// DTO de respuesta para un objeto en la cola de fabricación (Downtime Crafting).
/// </summary>
public record CraftingQueueItemDto(
    Guid Id,
    Guid PartyId,
    string ItemName,
    string ItemType,
    string Rarity,
    decimal GoldCost,
    int DaysRequired,
    int DaysRemaining,
    decimal Weight,
    string Description,
    bool IsOfficialSrd,
    string? ReferenceIndex,
    string? CrafterName,
    string Status,
    DateTime CreatedAtUtc
)
{
    /// <summary>
    /// Proyecta una entidad <see cref="CraftingQueueItem"/> a su DTO de respuesta.
    /// </summary>
    public static CraftingQueueItemDto FromEntity(CraftingQueueItem item) =>
        new(
            item.Id,
            item.PartyId,
            item.ItemName,
            item.ItemType,
            item.Rarity,
            item.GoldCost,
            item.DaysRequired,
            item.DaysRemaining,
            item.Weight,
            item.Description,
            item.IsOfficialSrd,
            item.ReferenceIndex,
            item.CrafterName,
            item.Status,
            item.CreatedAtUtc
        );
}

/// <summary>
/// Solicitud para encolar un nuevo objeto para su fabricación en downtime.
/// </summary>
public record QueueCraftingItemRequest(
    string ItemName,
    string? ItemType,
    string? Rarity,
    decimal GoldCost,
    int DaysRequired,
    decimal Weight,
    string? Description,
    bool IsOfficialSrd = false,
    string? ReferenceIndex = null,
    string? CrafterName = null
);

/// <summary>
/// DTO de respuesta para un registro del historial de auditoría de actividad (Audit Log).
/// </summary>
public record StashActivityLogDto(
    Guid Id,
    Guid PartyId,
    Guid? UserId,
    string UserDisplayName,
    string ActionType,
    string Description,
    DateTime TimestampUtc
)
{
    /// <summary>
    /// Proyecta una entidad <see cref="StashActivityLog"/> a su DTO de respuesta.
    /// </summary>
    public static StashActivityLogDto FromEntity(StashActivityLog log) =>
        new(
            log.Id,
            log.PartyId,
            log.UserId,
            log.UserDisplayName,
            log.ActionType,
            log.Description,
            log.TimestampUtc
        );
}

/// <summary>
/// DTO con el resultado de avanzar días de downtime.
/// </summary>
public record DowntimeAdvanceResultDto(
    int DaysAdvanced,
    int CompletedItemsCount,
    int ActiveItemsCount,
    IReadOnlyList<string> CompletedItemNames,
    IReadOnlyList<CraftingQueueItemDto> UpdatedQueue,
    IReadOnlyList<StashItemResponseDto> UpdatedStash
);
