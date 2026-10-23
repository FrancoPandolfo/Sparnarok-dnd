using Microsoft.AspNetCore.SignalR;
using Sparnarok.Api.Application.DTOs;
using Sparnarok.Api.Application.Interfaces;
using Sparnarok.Api.Domain.Models;

namespace Sparnarok.Api.Infrastructure.Hubs;

/// <summary>
/// Hub de SignalR para la orquestación en tiempo real de combates tácticos en vivo,
/// con emisión de proyecciones asimétricas por rol (Dungeon Master vs. Jugadores).
/// </summary>
public class LiveCombatHub : Hub
{
    private readonly ILiveCombatManager _combatManager;

    public LiveCombatHub(ILiveCombatManager combatManager)
    {
        _combatManager = combatManager;
    }

    /// <summary>
    /// Une la conexión a los canales correspondientes según su rol ('DungeonMaster' o 'Player')
    /// y le envía inmediatamente el estado proyectado.
    /// </summary>
    public async Task JoinCombat(string partyId, string role)
    {
        if (!Guid.TryParse(partyId, out var id))
        {
            return;
        }

        Context.Items["PartyId"] = id;
        var isDm = string.Equals(role, "DungeonMaster", StringComparison.OrdinalIgnoreCase);

        if (isDm)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GetDmGroupName(id));
            var dmProjection = _combatManager.CreateDmProjection(id);
            await Clients.Caller.SendAsync("CombatStateUpdated", dmProjection);
        }
        else
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GetPlayerGroupName(id));
            var playerProjection = _combatManager.CreatePlayerProjection(id);
            await Clients.Caller.SendAsync("CombatStateUpdated", playerProjection);
        }
    }

    /// <summary>
    /// Remueve la conexión de los canales de combate de la Party.
    /// </summary>
    public async Task LeaveCombat(string partyId)
    {
        if (!Guid.TryParse(partyId, out var id))
        {
            return;
        }

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GetDmGroupName(id));
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GetPlayerGroupName(id));
    }

    /// <summary>
    /// Inicializa un combate con la lista de combatientes provista (Acción del DM).
    /// </summary>
    public async Task InitializeCombat(string partyId, InitializeLiveCombatRequest request)
    {
        if (!Guid.TryParse(partyId, out var id)) return;

        var combatants = request.Combatants.Select(c => new LiveCombatant(
            c.Id,
            c.Name,
            c.MaxHp,
            c.ArmorClass,
            c.IsPlayerCharacter,
            c.IsVisibleToPlayers,
            c.AssignedPlayerUserId,
            c.Initiative
        )).ToList();

        _combatManager.InitializeCombat(id, request.PartyName, combatants);
        await BroadcastCombatStateAsync(id);
    }

    /// <summary>
    /// Registra la iniciativa ingresada por un Jugador o por el DM (opcional, puede ser nula).
    /// </summary>
    public async Task SubmitInitiative(string partyId, Guid combatantId, decimal? initiative)
    {
        if (!Guid.TryParse(partyId, out var id)) return;

        if (_combatManager.SetInitiative(id, combatantId, initiative))
        {
            await BroadcastCombatStateAsync(id);
        }
    }

    /// <summary>
    /// Permite al DM forzar/sobreescribir la iniciativa de cualquier combatiente.
    /// </summary>
    public async Task OverrideInitiative(string partyId, Guid combatantId, decimal? initiative)
    {
        if (!Guid.TryParse(partyId, out var id)) return;

        if (_combatManager.SetInitiative(id, combatantId, initiative))
        {
            await BroadcastCombatStateAsync(id);
        }
    }

    /// <summary>
    /// Reordena un combatiente en la lista posicional mediante Drag and Drop (Acción exclusiva del DM).
    /// </summary>
    public async Task ReorderCombatant(string partyId, string combatantId, int newIndex)
    {
        if (!Guid.TryParse(partyId, out var pId) || !Guid.TryParse(combatantId, out var cId))
        {
            return;
        }

        if (_combatManager.ReorderCombatant(pId, cId, newIndex))
        {
            await BroadcastCombatStateAsync(pId);
        }
    }

    /// <summary>
    /// Sobrecarga que infiere la Party desde el contexto de conexión activo.
    /// </summary>
    public async Task ReorderCombatant(string combatantId, int newIndex)
    {
        Guid? partyId = null;
        if (Context.Items.TryGetValue("PartyId", out var val))
        {
            if (val is Guid g)
            {
                partyId = g;
            }
            else if (val is string s && Guid.TryParse(s, out var parsed))
            {
                partyId = parsed;
            }
        }

        if (partyId.HasValue && Guid.TryParse(combatantId, out var cId))
        {
            if (_combatManager.ReorderCombatant(partyId.Value, cId, newIndex))
            {
                await BroadcastCombatStateAsync(partyId.Value);
            }
        }
    }

    /// <summary>
    /// Tira automáticamente la iniciativa para todos los monstruos/NPCs del combate (Acción del DM).
    /// </summary>
    public async Task AutoRollMonsters(string partyId)
    {
        if (!Guid.TryParse(partyId, out var id)) return;

        _combatManager.AutoRollMonsters(id);
        await BroadcastCombatStateAsync(id);
    }

    /// <summary>
    /// Inicia formalmente el combate, ordenando los combatientes por iniciativa y fijando la ronda 1 (Acción del DM).
    /// </summary>
    public async Task StartCombat(string partyId)
    {
        if (!Guid.TryParse(partyId, out var id)) return;

        if (_combatManager.StartCombat(id))
        {
            await BroadcastCombatStateAsync(id);
        }
    }

    /// <summary>
    /// Avanza al siguiente turno de combate (Acción del DM).
    /// </summary>
    public async Task NextTurn(string partyId)
    {
        if (!Guid.TryParse(partyId, out var id)) return;

        if (_combatManager.NextTurn(id))
        {
            await BroadcastCombatStateAsync(id);
        }
    }

    /// <summary>
    /// Retrocede al turno de combate anterior (Acción del DM).
    /// </summary>
    public async Task PreviousTurn(string partyId)
    {
        if (!Guid.TryParse(partyId, out var id)) return;

        if (_combatManager.PreviousTurn(id))
        {
            await BroadcastCombatStateAsync(id);
        }
    }

    /// <summary>
    /// Modifica los puntos de golpe (HP) de un combatiente mediante un delta (Acción del DM).
    /// </summary>
    public async Task ModifyHp(string partyId, Guid combatantId, int deltaHp)
    {
        if (!Guid.TryParse(partyId, out var id)) return;

        if (_combatManager.ModifyCombatantHp(id, combatantId, deltaHp))
        {
            await BroadcastCombatStateAsync(id);
        }
    }

    /// <summary>
    /// Conmuta la visibilidad de un combatiente ante los jugadores (Acción del DM).
    /// </summary>
    public async Task ToggleVisibility(string partyId, Guid combatantId, bool isVisible)
    {
        if (!Guid.TryParse(partyId, out var id)) return;

        if (_combatManager.SetCombatantVisibility(id, combatantId, isVisible))
        {
            await BroadcastCombatStateAsync(id);
        }
    }

    /// <summary>
    /// Finaliza el combate activo (Acción del DM).
    /// </summary>
    public async Task EndCombat(string partyId)
    {
        if (!Guid.TryParse(partyId, out var id)) return;

        if (_combatManager.EndCombat(id))
        {
            await BroadcastCombatStateAsync(id);
        }
    }

    /// <summary>
    /// Emite proyecciones asimétricas diferenciadas: completa para el DM y censurada para Jugadores.
    /// </summary>
    public async Task BroadcastCombatStateAsync(Guid partyId)
    {
        var dmProjection = _combatManager.CreateDmProjection(partyId);
        var playerProjection = _combatManager.CreatePlayerProjection(partyId);

        await Clients.Group(GetDmGroupName(partyId))
            .SendAsync("CombatStateUpdated", dmProjection);

        await Clients.Group(GetPlayerGroupName(partyId))
            .SendAsync("CombatStateUpdated", playerProjection);
    }

    public static string GetDmGroupName(Guid partyId) => $"combat_{partyId}_dm";
    public static string GetPlayerGroupName(Guid partyId) => $"combat_{partyId}_player";
}
