using Microsoft.AspNetCore.SignalR;
using Sparnarok.Api.Application.DTOs;
using Sparnarok.Api.Application.Interfaces;

namespace Sparnarok.Api.Infrastructure.Hubs;

/// <summary>
/// Implementación de <see cref="IStashNotificationService"/> mediante SignalR <see cref="IHubContext{THub}"/>.
/// Emite eventos tipados a los clientes suscritos al grupo de la Party correspondiente.
/// </summary>
public class StashNotificationService : IStashNotificationService
{
    private readonly IHubContext<StashHub> _hubContext;

    public StashNotificationService(IHubContext<StashHub> hubContext)
    {
        _hubContext = hubContext;
    }

    private string Group(Guid partyId) => StashHub.GetGroupName(partyId);

    public async Task NotifyItemAddedAsync(Guid partyId, StashItemResponseDto item, CancellationToken cancellationToken = default)
    {
        await _hubContext.Clients.Group(Group(partyId))
            .SendAsync("ItemAdded", item, cancellationToken);
    }

    public async Task NotifyItemDeletedAsync(Guid partyId, Guid itemId, CancellationToken cancellationToken = default)
    {
        await _hubContext.Clients.Group(Group(partyId))
            .SendAsync("ItemDeleted", itemId, cancellationToken);
    }

    public async Task NotifyFundsUpdatedAsync(Guid partyId, PartyFundDto funds, CancellationToken cancellationToken = default)
    {
        await _hubContext.Clients.Group(Group(partyId))
            .SendAsync("FundsUpdated", funds, cancellationToken);
    }

    public async Task NotifyCraftingQueueUpdatedAsync(Guid partyId, IReadOnlyList<CraftingQueueItemDto> queue, CancellationToken cancellationToken = default)
    {
        await _hubContext.Clients.Group(Group(partyId))
            .SendAsync("CraftingQueueUpdated", queue, cancellationToken);
    }

    public async Task NotifyActivityLoggedAsync(Guid partyId, StashActivityLogDto log, CancellationToken cancellationToken = default)
    {
        await _hubContext.Clients.Group(Group(partyId))
            .SendAsync("ActivityLogged", log, cancellationToken);
    }

    public async Task NotifyDowntimeAdvancedAsync(Guid partyId, DowntimeAdvanceResultDto result, CancellationToken cancellationToken = default)
    {
        await _hubContext.Clients.Group(Group(partyId))
            .SendAsync("DowntimeAdvanced", result, cancellationToken);
    }

    public async Task NotifyStashUpdatedAsync(Guid partyId, PartyStashResponseDto stash, CancellationToken cancellationToken = default)
    {
        await _hubContext.Clients.Group(Group(partyId))
            .SendAsync("StashUpdated", stash, cancellationToken);
    }
}
