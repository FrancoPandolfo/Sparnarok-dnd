using Microsoft.AspNetCore.SignalR;
using Sparnarok.Api.Application.DTOs;
using Sparnarok.Api.Application.Extensions;
using Sparnarok.Api.Application.Interfaces;
using Sparnarok.Api.Domain.Models;

namespace Sparnarok.Api.Infrastructure.Hubs;

/// <summary>
/// Hub de SignalR para la orquestación en tiempo real de combates tácticos en vivo,
/// con emisión de proyecciones asimétricas por rol (Dungeon Master vs. Jugadores).
/// </summary>
public class LiveCombatHub : Hub
{
    public static readonly Guid QuickSessionGuid = Guid.Parse("00000000-0000-0000-0000-000000000099");

    private readonly ILiveCombatManager _combatManager;
    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly ILogger<LiveCombatHub> _logger;

    public LiveCombatHub(
        ILiveCombatManager combatManager,
        IServiceScopeFactory? scopeFactory = null,
        ILogger<LiveCombatHub>? logger = null)
    {
        _combatManager = combatManager;
        _scopeFactory = scopeFactory;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<LiveCombatHub>.Instance;
    }

    /// <summary>
    /// Une la conexión a los canales correspondientes según su rol ('DungeonMaster' o 'Player')
    /// y le envía inmediatamente el estado proyectado.
    /// </summary>
    public async Task JoinCombat(string? partyId, string role)
    {
        if (!TryResolvePartyId(partyId, out var id))
        {
            return;
        }

        Context.Items["PartyId"] = id;
        Context.Items["Role"] = role;

        // Intentar restaurar (rehidratar) el estado desde la base de datos si la memoria está vacía
        if (id != QuickSessionGuid)
        {
            var currentState = _combatManager.GetState(id);
            if (currentState == null || currentState.Status == LiveCombatStatus.NotStarted)
            {
                await TryRehydrateFromDatabaseAsync(id);
            }
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GetPartyGroupName(id));

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
    public async Task LeaveCombat(string? partyId)
    {
        if (!TryResolvePartyId(partyId, out var id))
        {
            return;
        }

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GetDmGroupName(id));
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GetPlayerGroupName(id));
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GetPartyGroupName(id));
    }

    /// <summary>
    /// Inicializa un combate con la lista de combatientes provista (Acción del DM).
    /// </summary>
    public async Task InitializeCombat(string? partyId, InitializeLiveCombatRequest request)
    {
        if (!TryResolvePartyId(partyId, out var id)) return;

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
        await PersistCombatStateAsync(id);
        await BroadcastCombatStateAsync(id);

        var encounterStartedPayload = new
        {
            PartyId = id.ToString(),
            PartyName = request.PartyName,
            StartedAt = DateTime.UtcNow
        };
        await Clients.Group(GetPartyGroupName(id)).SendAsync("EncounterStarted", encounterStartedPayload);
        await Clients.Group(GetPlayerGroupName(id)).SendAsync("EncounterStarted", encounterStartedPayload);
        await Clients.Group(GetDmGroupName(id)).SendAsync("EncounterStarted", encounterStartedPayload);
    }

    /// <summary>
    /// Registra la iniciativa ingresada por un Jugador o por el DM (opcional, puede ser nula).
    /// </summary>
    public async Task SubmitInitiative(string? partyId, Guid combatantId, decimal? initiative)
    {
        if (!TryResolvePartyId(partyId, out var id)) return;

        if (_combatManager.SetInitiative(id, combatantId, initiative))
        {
            await PersistCombatStateAsync(id);
            await BroadcastCombatStateAsync(id);
        }
    }

    /// <summary>
    /// Permite al DM forzar/sobreescribir la iniciativa de cualquier combatiente.
    /// </summary>
    public async Task OverrideInitiative(string? partyId, Guid combatantId, decimal? initiative)
    {
        if (!TryResolvePartyId(partyId, out var id)) return;

        if (_combatManager.SetInitiative(id, combatantId, initiative))
        {
            await PersistCombatStateAsync(id);
            await BroadcastCombatStateAsync(id);
        }
    }

    /// <summary>
    /// Actualiza la iniciativa de un combatiente.
    /// Si el usuario tiene el rol de 'Player', solo puede actualizar la iniciativa de su propio combatiente.
    /// El Dungeon Master puede actualizar la iniciativa de cualquier combatiente.
    /// </summary>
    public async Task UpdateCombatantInitiative(string? partyId, string combatantId, decimal? newInitiative)
    {
        if (!TryResolvePartyId(partyId, out var id)) return;
        if (string.IsNullOrWhiteSpace(combatantId)) return;
        if (!Guid.TryParse(combatantId.Trim(), out var parsedCombatantId)) return;

        var state = _combatManager.GetState(id);
        if (state == null) return;

        var targetCombatant = state.Combatants.FirstOrDefault(c => c.Id == parsedCombatantId);
        if (targetCombatant == null) return;

        var isDm = await IsCallerDungeonMasterAsync(id);

        if (!isDm)
        {
            var currentUserId = Context.User?.GetUserId();
            var isOwnCombatant = false;

            if (currentUserId.HasValue)
            {
                if (targetCombatant.AssignedPlayerUserId.HasValue && targetCombatant.AssignedPlayerUserId.Value == currentUserId.Value)
                {
                    isOwnCombatant = true;
                }
                else if (targetCombatant.Id == currentUserId.Value)
                {
                    isOwnCombatant = true;
                }
            }

            if (!isOwnCombatant)
            {
                _logger.LogWarning("Acceso no autorizado: El usuario {UserId} intentó modificar la iniciativa del combatiente {CombatantId} ajeno", currentUserId, combatantId);
                return;
            }
        }

        if (_combatManager.SetInitiative(id, parsedCombatantId, newInitiative))
        {
            await PersistCombatStateAsync(id);
            await BroadcastCombatStateAsync(id);
        }
    }

    /// <summary>
    /// Elimina un combatiente del combate activo y recalcula el SortOrder del resto de la lista (Acción exclusiva del DM).
    /// Emite el broadcast estándar 'CombatStateUpdated' a todos los clientes de ese PartyId.
    /// </summary>
    public async Task RemoveCombatant(string? partyId, string combatantId)
    {
        if (!TryResolvePartyId(partyId, out var id)) return;
        if (string.IsNullOrWhiteSpace(combatantId)) return;
        if (!Guid.TryParse(combatantId.Trim(), out var parsedCombatantId)) return;

        var isDm = await IsCallerDungeonMasterAsync(id);
        if (!isDm)
        {
            _logger.LogWarning("Acceso no autorizado: Intento de eliminar combatiente {CombatantId} en Party {PartyId} sin rol de Dungeon Master", combatantId, id);
            return;
        }

        if (_combatManager.RemoveCombatant(id, parsedCombatantId))
        {
            await PersistCombatStateAsync(id);
            await BroadcastCombatStateAsync(id);
        }
    }

    /// <summary>
    /// Auto-ordena los combatientes de acuerdo a la estrategia de SortMode especificada (Acción exclusiva del DM).
    /// Emite el broadcast estándar 'CombatStateUpdated' a todos los clientes de ese PartyId.
    /// </summary>
    public async Task AutoSortInitiative(string? partyId, SortMode mode)
    {
        if (!TryResolvePartyId(partyId, out var id)) return;

        var isDm = await IsCallerDungeonMasterAsync(id);
        if (!isDm)
        {
            _logger.LogWarning("Acceso no autorizado: Intento de auto-ordenar iniciativas en Party {PartyId} sin rol de Dungeon Master", id);
            return;
        }

        if (_combatManager.AutoSortInitiative(id, mode))
        {
            await PersistCombatStateAsync(id);
            await BroadcastCombatStateAsync(id);
        }
    }

    private async Task<bool> IsCallerDungeonMasterAsync(Guid partyId)
    {
        if (Context.Items.TryGetValue("Role", out var roleObj) && roleObj is string r)
        {
            if (string.Equals(r, "DungeonMaster", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        var currentUserId = Context.User?.GetUserId();
        if (currentUserId.HasValue && _scopeFactory != null && partyId != QuickSessionGuid)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var partyRepo = scope.ServiceProvider.GetRequiredService<IPartyRepository>();
                var party = await partyRepo.GetByIdAsync(partyId);
                if (party != null && party.OwnerUserId == currentUserId.Value)
                {
                    return true;
                }
            }
            catch {}
        }

        return false;
    }

    /// <summary>
    /// Reordena un combatiente en la lista posicional mediante Drag and Drop (Acción exclusiva del DM).
    /// Infiere la Party desde el contexto de conexión activo asignado en JoinCombat.
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
            else if (val is string s && TryResolvePartyId(s, out var parsed))
            {
                partyId = parsed;
            }
        }

        if (partyId.HasValue && Guid.TryParse(combatantId, out var cId))
        {
            if (_combatManager.ReorderCombatant(partyId.Value, cId, newIndex))
            {
                await PersistCombatStateAsync(partyId.Value);
                await BroadcastCombatStateAsync(partyId.Value);
            }
        }
    }

    /// <summary>
    /// Reordena un combatiente especificando explícitamente el identificador de la Party (Acción exclusiva del DM).
    /// </summary>
    public async Task ReorderCombatantInParty(string? partyId, string combatantId, int newIndex)
    {
        if (!TryResolvePartyId(partyId, out var pId) || !Guid.TryParse(combatantId, out var cId))
        {
            return;
        }

        if (_combatManager.ReorderCombatant(pId, cId, newIndex))
        {
            await PersistCombatStateAsync(pId);
            await BroadcastCombatStateAsync(pId);
        }
    }

    /// <summary>
    /// Añade un nuevo combatiente rápido en caliente directamente al combate activo (Acción del DM).
    /// No requiere persistencia previa en la base de datos ni pertenencia a una Party formal.
    /// </summary>
    public async Task AddQuickCombatant(string? partyId, AddQuickCombatantRequest request)
    {
        if (!TryResolvePartyId(partyId, out var id)) return;
        if (request is null || string.IsNullOrWhiteSpace(request.Name)) return;

        var combatant = new LiveCombatant(
            Guid.NewGuid(),
            request.Name.Trim(),
            Math.Max(1, request.MaxHp),
            Math.Max(0, request.ArmorClass),
            request.IsPlayerCharacter,
            request.IsVisibleToPlayers,
            null,
            request.Initiative
        );

        _combatManager.AddCombatant(id, combatant);
        await PersistCombatStateAsync(id);
        await BroadcastCombatStateAsync(id);
    }

    /// <summary>
    /// Tira automáticamente la iniciativa para todos los monstruos/NPCs del combate (Acción del DM).
    /// </summary>
    public async Task AutoRollMonsters(string? partyId)
    {
        if (!TryResolvePartyId(partyId, out var id)) return;

        _combatManager.AutoRollMonsters(id);
        await PersistCombatStateAsync(id);
        await BroadcastCombatStateAsync(id);
    }

    /// <summary>
    /// Inicia formalmente el combate, ordenando los combatientes por iniciativa y fijando la ronda 1 (Acción del DM).
    /// </summary>
    public async Task StartCombat(string? partyId)
    {
        if (!TryResolvePartyId(partyId, out var id)) return;

        if (_combatManager.StartCombat(id))
        {
            await PersistCombatStateAsync(id);
            await BroadcastCombatStateAsync(id);

            var state = _combatManager.GetState(id);
            var encounterStartedPayload = new
            {
                PartyId = id.ToString(),
                PartyName = state?.PartyName ?? "Encuentro en Vivo",
                StartedAt = DateTime.UtcNow
            };
            await Clients.Group(GetPartyGroupName(id)).SendAsync("EncounterStarted", encounterStartedPayload);
            await Clients.Group(GetPlayerGroupName(id)).SendAsync("EncounterStarted", encounterStartedPayload);
            await Clients.Group(GetDmGroupName(id)).SendAsync("EncounterStarted", encounterStartedPayload);
        }
    }

    /// <summary>
    /// Permite al Dungeon Master incorporar directamente a un miembro de la Party al combate activo
    /// sin requerir confirmación previa ni invitación.
    /// Busca los datos del jugador (HP, CA, Nombre) en la base de datos de la Party,
    /// asigna una iniciativa nula (null), y lo agrega al estado en memoria al final de la lista con su SortOrder correspondiente.
    /// Finalmente, emite el broadcast estándar 'CombatStateUpdated' a todos los clientes de ese PartyId.
    /// </summary>
    public async Task ForceAddPartyMemberToCombat(string? partyId, string partyMemberUserId)
    {
        if (!TryResolvePartyId(partyId, out var id)) return;
        if (string.IsNullOrWhiteSpace(partyMemberUserId)) return;

        Guid.TryParse(partyMemberUserId.Trim(), out var memberUserId);

        string name = "Héroe";
        int maxHp = 20;
        int currentHp = 20;
        int armorClass = 14;

        if (_scopeFactory != null && id != QuickSessionGuid)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var partyRepo = scope.ServiceProvider.GetRequiredService<IPartyRepository>();
                var party = await partyRepo.GetByIdAsync(id);
                if (party != null)
                {
                    var partyMember = party.PartyMembers.FirstOrDefault(pm =>
                        (memberUserId != Guid.Empty && (pm.UserId == memberUserId || pm.Id == memberUserId)) ||
                        pm.UserId.ToString().Equals(partyMemberUserId.Trim(), StringComparison.OrdinalIgnoreCase) ||
                        pm.Id.ToString().Equals(partyMemberUserId.Trim(), StringComparison.OrdinalIgnoreCase));

                    if (partyMember != null)
                    {
                        name = !string.IsNullOrWhiteSpace(partyMember.InGameName) ? partyMember.InGameName.Trim() : name;

                        var creature = party.Members.FirstOrDefault(m =>
                            m.Name.Equals(partyMember.InGameName, StringComparison.OrdinalIgnoreCase) ||
                            (partyMember.Id != Guid.Empty && m.Id == partyMember.Id));

                        if (creature != null)
                        {
                            name = creature.Name;
                            maxHp = creature.HitPoints.Max;
                            currentHp = creature.HitPoints.Current;
                            armorClass = creature.EffectiveArmorClass;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudieron obtener los datos de la Party desde la base de datos para el miembro {PartyMemberUserId}", partyMemberUserId);
            }
        }

        _combatManager.ForceAddPartyMember(
            id,
            memberUserId != Guid.Empty ? memberUserId : null,
            name,
            maxHp,
            armorClass,
            currentHp
        );

        await PersistCombatStateAsync(id);
        await BroadcastCombatStateAsync(id);
    }

    /// <summary>
    /// Permite que un Jugador se agregue voluntariamente a la lista de iniciativa del combate activo (Fase Lobby).
    /// </summary>
    public async Task JoinEncounter(string? partyId, LiveCombatantPlayerDto playerStats)
    {
        if (!TryResolvePartyId(partyId, out var id)) return;
        if (playerStats == null) return;

        var userId = Context.User?.GetUserId();
        var displayName = Context.User?.GetDisplayName() ?? "Héroe";
        var name = !string.IsNullOrWhiteSpace(playerStats.Name) ? playerStats.Name.Trim() : displayName;
        var maxHp = playerStats.MaxHp ?? 10;
        var currentHp = playerStats.CurrentHp ?? maxHp;

        var combatant = new LiveCombatant(
            playerStats.Id == Guid.Empty ? Guid.NewGuid() : playerStats.Id,
            name,
            maxHp,
            10,
            true,
            true,
            userId,
            playerStats.Initiative
        );
        combatant.CurrentHp = Math.Clamp(currentHp, 0, combatant.MaxHp);

        var state = _combatManager.GetState(id);
        var existing = state?.Combatants.FirstOrDefault(c =>
            (c.Id != Guid.Empty && c.Id == combatant.Id) ||
            (userId.HasValue && c.AssignedPlayerUserId == userId.Value));

        if (existing != null)
        {
            existing.Initiative = playerStats.Initiative ?? existing.Initiative;
            existing.CurrentHp = Math.Clamp(currentHp, 0, existing.MaxHp);
            existing.Name = name;
        }
        else
        {
            _combatManager.AddCombatant(id, combatant);
        }

        await PersistCombatStateAsync(id);
        await BroadcastCombatStateAsync(id);
    }

    /// <summary>
    /// Avanza al siguiente turno de combate (Acción del DM).
    /// </summary>
    public async Task NextTurn(string? partyId)
    {
        if (!TryResolvePartyId(partyId, out var id)) return;

        if (_combatManager.NextTurn(id))
        {
            await PersistCombatStateAsync(id);
            await BroadcastCombatStateAsync(id);
        }
    }

    /// <summary>
    /// Retrocede al turno de combate anterior (Acción del DM).
    /// </summary>
    public async Task PreviousTurn(string? partyId)
    {
        if (!TryResolvePartyId(partyId, out var id)) return;

        if (_combatManager.PreviousTurn(id))
        {
            await PersistCombatStateAsync(id);
            await BroadcastCombatStateAsync(id);
        }
    }

    /// <summary>
    /// Modifica los puntos de golpe (HP) de un combatiente mediante un delta (Acción del DM).
    /// </summary>
    public async Task ModifyHp(string? partyId, Guid combatantId, int deltaHp)
    {
        if (!TryResolvePartyId(partyId, out var id)) return;

        if (_combatManager.ModifyCombatantHp(id, combatantId, deltaHp))
        {
            await PersistCombatStateAsync(id);
            await BroadcastCombatStateAsync(id);
        }
    }

    /// <summary>
    /// Conmuta la visibilidad de un combatiente ante los jugadores (Acción del DM).
    /// </summary>
    public async Task ToggleVisibility(string? partyId, Guid combatantId, bool isVisible)
    {
        if (!TryResolvePartyId(partyId, out var id)) return;

        if (_combatManager.SetCombatantVisibility(id, combatantId, isVisible))
        {
            await PersistCombatStateAsync(id);
            await BroadcastCombatStateAsync(id);
        }
    }

    /// <summary>
    /// Finaliza el combate activo (Acción del DM).
    /// </summary>
    public async Task EndCombat(string? partyId)
    {
        if (!TryResolvePartyId(partyId, out var id)) return;

        if (_combatManager.EndCombat(id))
        {
            await PersistCombatStateAsync(id);
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
    public static string GetPartyGroupName(Guid partyId) => $"combat_{partyId}_party";

    /// <summary>
    /// Intenta restaurar el estado de combate persistido en la base de datos de la Party.
    /// </summary>
    private async Task TryRehydrateFromDatabaseAsync(Guid partyId)
    {
        if (_scopeFactory == null || partyId == Guid.Empty || partyId == QuickSessionGuid)
        {
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var partyRepo = scope.ServiceProvider.GetRequiredService<IPartyRepository>();
            var party = await partyRepo.GetByIdAsync(partyId);
            if (party != null && !string.IsNullOrWhiteSpace(party.CurrentCombatState))
            {
                _combatManager.RehydrateState(partyId, party.CurrentCombatState);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al rehidratar el estado de combate para la Party {PartyId}", partyId);
        }
    }

    /// <summary>
    /// Serializa y guarda el estado actual del combate en la entidad Party si no es una sala efímera.
    /// </summary>
    private async Task PersistCombatStateAsync(Guid partyId)
    {
        if (_scopeFactory == null || partyId == Guid.Empty || partyId == QuickSessionGuid)
        {
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var partyRepo = scope.ServiceProvider.GetRequiredService<IPartyRepository>();
            var party = await partyRepo.GetByIdAsync(partyId);
            if (party != null)
            {
                var serialized = _combatManager.SerializeState(partyId);
                party.SetCurrentCombatState(serialized);
                await partyRepo.UpdateAsync(party);
                await partyRepo.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al persistir el estado de combate para la Party {PartyId}", partyId);
        }
    }

    /// <summary>
    /// Resuelve de forma determinística identificadores alfanuméricos ("quick-session") a un GUID válido para SignalR y el CombatManager.
    /// </summary>
    public static bool TryResolvePartyId(string? partyId, out Guid id)
    {
        if (string.IsNullOrWhiteSpace(partyId) ||
            string.Equals(partyId.Trim(), "null", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(partyId.Trim(), "undefined", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(partyId.Trim(), "quick-session", StringComparison.OrdinalIgnoreCase))
        {
            id = QuickSessionGuid;
            return true;
        }

        if (Guid.TryParse(partyId.Trim(), out id))
        {
            return true;
        }

        using var md5 = System.Security.Cryptography.MD5.Create();
        id = new Guid(md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(partyId.Trim())));
        return true;
    }
}
