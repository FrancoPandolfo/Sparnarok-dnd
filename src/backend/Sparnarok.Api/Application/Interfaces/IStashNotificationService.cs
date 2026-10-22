using Sparnarok.Api.Application.DTOs;

namespace Sparnarok.Api.Application.Interfaces;

/// <summary>
/// Servicio de emisión de notificaciones en tiempo real a través de SignalR para eventos del alijo y Party.
/// </summary>
public interface IStashNotificationService
{
    /// <summary>Emite evento cuando se añade un objeto al alijo.</summary>
    Task NotifyItemAddedAsync(Guid partyId, StashItemResponseDto item, CancellationToken cancellationToken = default);

    /// <summary>Emite evento cuando se elimina un objeto del alijo.</summary>
    Task NotifyItemDeletedAsync(Guid partyId, Guid itemId, CancellationToken cancellationToken = default);

    /// <summary>Emite evento cuando los fondos de la Party cambian.</summary>
    Task NotifyFundsUpdatedAsync(Guid partyId, PartyFundDto funds, CancellationToken cancellationToken = default);

    /// <summary>Emite evento cuando la cola de fabricación de downtime se actualiza.</summary>
    Task NotifyCraftingQueueUpdatedAsync(Guid partyId, IReadOnlyList<CraftingQueueItemDto> queue, CancellationToken cancellationToken = default);

    /// <summary>Emite evento cuando se registra una nueva entrada en el historial de auditoría.</summary>
    Task NotifyActivityLoggedAsync(Guid partyId, StashActivityLogDto log, CancellationToken cancellationToken = default);

    /// <summary>Emite evento cuando el Dungeon Master avanza días de downtime.</summary>
    Task NotifyDowntimeAdvancedAsync(Guid partyId, DowntimeAdvanceResultDto result, CancellationToken cancellationToken = default);

    /// <summary>Emite evento de refresco integral del alijo.</summary>
    Task NotifyStashUpdatedAsync(Guid partyId, PartyStashResponseDto stash, CancellationToken cancellationToken = default);
}
