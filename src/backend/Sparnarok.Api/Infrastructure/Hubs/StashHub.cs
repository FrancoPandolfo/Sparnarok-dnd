using Microsoft.AspNetCore.SignalR;

namespace Sparnarok.Api.Infrastructure.Hubs;

/// <summary>
/// Hub de SignalR para la sincronización en tiempo real del alijo de la Party,
/// fondos monetarios, cola de fabricación de downtime y registros de auditoría.
/// </summary>
public class StashHub : Hub
{
    /// <summary>
    /// Une la conexión actual al grupo exclusivo de la Party identificada por <paramref name="partyId"/>.
    /// </summary>
    public async Task JoinPartyGroup(string partyId)
    {
        if (Guid.TryParse(partyId, out var id))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GetGroupName(id));
        }
    }

    /// <summary>
    /// Remueve la conexión actual del grupo exclusivo de la Party.
    /// </summary>
    public async Task LeavePartyGroup(string partyId)
    {
        if (Guid.TryParse(partyId, out var id))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, GetGroupName(id));
        }
    }

    /// <summary>
    /// Obtiene el nombre del grupo de SignalR para una party dada.
    /// </summary>
    public static string GetGroupName(Guid partyId) => $"party_{partyId}";
}
