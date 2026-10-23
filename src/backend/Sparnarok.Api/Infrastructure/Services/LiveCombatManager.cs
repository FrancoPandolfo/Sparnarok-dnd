using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sparnarok.Api.Application.DTOs;
using Sparnarok.Api.Application.Interfaces;
using Sparnarok.Api.Domain.Models;

namespace Sparnarok.Api.Infrastructure.Services;

/// <summary>
/// Implementación de <see cref="ILiveCombatManager"/> que gestiona el estado en memoria de forma segura y concurrente.
/// </summary>
public class LiveCombatManager : ILiveCombatManager
{
    private readonly ConcurrentDictionary<Guid, LiveCombatState> _states = new();
    private readonly Random _random = new();

    public LiveCombatState GetOrCreateState(Guid partyId, string partyName = "")
    {
        return _states.GetOrAdd(partyId, id => new LiveCombatState
        {
            PartyId = id,
            PartyName = string.IsNullOrWhiteSpace(partyName) ? "Party" : partyName.Trim(),
            Status = LiveCombatStatus.NotStarted,
            CurrentRound = 1,
            CurrentTurnIndex = 0,
            Combatants = []
        });
    }

    public LiveCombatState? GetState(Guid partyId)
    {
        return _states.TryGetValue(partyId, out var state) ? state : null;
    }

    public LiveCombatState InitializeCombat(Guid partyId, string partyName, IEnumerable<LiveCombatant> combatants)
    {
        var list = combatants.ToList();
        for (int i = 0; i < list.Count; i++)
        {
            list[i].SortOrder = i;
        }

        var state = new LiveCombatState
        {
            PartyId = partyId,
            PartyName = string.IsNullOrWhiteSpace(partyName) ? "Party" : partyName.Trim(),
            Status = LiveCombatStatus.WaitingForInitiative,
            CurrentRound = 1,
            CurrentTurnIndex = 0,
            Combatants = list
        };

        _states[partyId] = state;
        return state;
    }

    public bool SetInitiative(Guid partyId, Guid combatantId, decimal? initiative)
    {
        if (!_states.TryGetValue(partyId, out var state))
        {
            return false;
        }

        var combatant = state.Combatants.FirstOrDefault(c => c.Id == combatantId);
        if (combatant == null)
        {
            return false;
        }

        combatant.Initiative = initiative;
        return true;
    }

    public bool ReorderCombatant(Guid partyId, Guid combatantId, int newIndex)
    {
        if (!_states.TryGetValue(partyId, out var state) || state.Combatants.Count == 0)
        {
            return false;
        }

        var oldIndex = state.Combatants.FindIndex(c => c.Id == combatantId);
        if (oldIndex < 0)
        {
            return false;
        }

        var clampedIndex = Math.Clamp(newIndex, 0, state.Combatants.Count - 1);
        if (oldIndex == clampedIndex)
        {
            return true;
        }

        var activeCombatant = state.GetCurrentCombatant();
        var combatant = state.Combatants[oldIndex];
        state.Combatants.RemoveAt(oldIndex);
        state.Combatants.Insert(clampedIndex, combatant);

        for (int i = 0; i < state.Combatants.Count; i++)
        {
            state.Combatants[i].SortOrder = i;
        }

        if (activeCombatant != null)
        {
            var newActiveIndex = state.Combatants.IndexOf(activeCombatant);
            if (newActiveIndex >= 0)
            {
                state.CurrentTurnIndex = newActiveIndex;
            }
        }

        return true;
    }

    public LiveCombatant AddCombatant(Guid partyId, LiveCombatant combatant)
    {
        var state = GetOrCreateState(partyId);
        combatant.SortOrder = state.Combatants.Count;
        state.Combatants.Add(combatant);
        return combatant;
    }

    public void AutoRollMonsters(Guid partyId, Func<int>? d20Roller = null)
    {
        if (!_states.TryGetValue(partyId, out var state))
        {
            return;
        }

        var roll = d20Roller ?? (() => _random.Next(1, 21));

        foreach (var combatant in state.Combatants.Where(c => !c.IsPlayerCharacter))
        {
            combatant.Initiative = roll();
        }
    }

    public bool StartCombat(Guid partyId)
    {
        if (!_states.TryGetValue(partyId, out var state) || state.Combatants.Count == 0)
        {
            return false;
        }

        // El orden posicional de Combatants (SortOrder) es la única fuente de verdad.
        // No se reordena por iniciativa ni se fuerza un valor arbitrario.
        for (int i = 0; i < state.Combatants.Count; i++)
        {
            state.Combatants[i].SortOrder = i;
        }

        state.Status = LiveCombatStatus.Active;
        state.CurrentRound = 1;
        state.CurrentTurnIndex = 0;
        return true;
    }

    public bool NextTurn(Guid partyId)
    {
        if (!_states.TryGetValue(partyId, out var state) || state.Status != LiveCombatStatus.Active || state.Combatants.Count == 0)
        {
            return false;
        }

        state.CurrentTurnIndex++;
        if (state.CurrentTurnIndex >= state.Combatants.Count)
        {
            state.CurrentTurnIndex = 0;
            state.CurrentRound++;
        }

        return true;
    }

    public bool PreviousTurn(Guid partyId)
    {
        if (!_states.TryGetValue(partyId, out var state) || state.Status != LiveCombatStatus.Active || state.Combatants.Count == 0)
        {
            return false;
        }

        state.CurrentTurnIndex--;
        if (state.CurrentTurnIndex < 0)
        {
            state.CurrentTurnIndex = state.Combatants.Count - 1;
            state.CurrentRound = Math.Max(1, state.CurrentRound - 1);
        }

        return true;
    }

    public bool ModifyCombatantHp(Guid partyId, Guid combatantId, int deltaHp)
    {
        if (!_states.TryGetValue(partyId, out var state))
        {
            return false;
        }

        var combatant = state.Combatants.FirstOrDefault(c => c.Id == combatantId);
        if (combatant == null)
        {
            return false;
        }

        combatant.CurrentHp = Math.Clamp(combatant.CurrentHp + deltaHp, 0, combatant.MaxHp);
        return true;
    }

    public bool SetCombatantVisibility(Guid partyId, Guid combatantId, bool isVisible)
    {
        if (!_states.TryGetValue(partyId, out var state))
        {
            return false;
        }

        var combatant = state.Combatants.FirstOrDefault(c => c.Id == combatantId);
        if (combatant == null)
        {
            return false;
        }

        combatant.IsVisibleToPlayers = isVisible;
        return true;
    }

    public bool EndCombat(Guid partyId)
    {
        if (!_states.TryGetValue(partyId, out var state))
        {
            return false;
        }

        state.Status = LiveCombatStatus.Ended;
        return true;
    }

    public LiveCombatDmProjectionDto CreateDmProjection(Guid partyId)
    {
        var state = GetOrCreateState(partyId);
        var active = state.GetCurrentCombatant();

        var dmCombatants = state.Combatants.Select(c => new LiveCombatantDmDto(
            c.Id,
            c.Name,
            c.Initiative,
            c.MaxHp,
            c.CurrentHp,
            c.ArmorClass,
            c.IsPlayerCharacter,
            c.IsVisibleToPlayers,
            c.AssignedPlayerUserId,
            c.Conditions,
            c.SortOrder
        )).ToList();

        return new LiveCombatDmProjectionDto(
            state.PartyId,
            state.PartyName,
            state.Status.ToString(),
            state.CurrentRound,
            state.CurrentTurnIndex,
            active?.Id,
            active?.Name,
            dmCombatants
        );
    }

    public LiveCombatPlayerProjectionDto CreatePlayerProjection(Guid partyId)
    {
        var state = GetOrCreateState(partyId);
        var active = state.GetCurrentCombatant();

        // Los jugadores SOLO ven combatientes que sean PCs o monstruos con IsVisibleToPlayers == true
        var playerCombatants = state.Combatants
            .Where(c => c.IsPlayerCharacter || c.IsVisibleToPlayers)
            .Select(c =>
            {
                int? hp = c.IsPlayerCharacter ? c.CurrentHp : null;
                int? maxHp = c.IsPlayerCharacter ? c.MaxHp : null;
                string healthStatus = c.CurrentHp <= 0 ? "Down"
                    : c.CurrentHp <= c.MaxHp / 2 ? "Bloodied"
                    : "Healthy";

                return new LiveCombatantPlayerDto(
                    c.Id,
                    c.Name,
                    c.Initiative,
                    c.IsPlayerCharacter,
                    hp,
                    maxHp,
                    healthStatus,
                    c.Conditions,
                    c.SortOrder
                );
            }).ToList();

        // Si el combatiente de turno es un monstruo oculto, no revelar su nombre ni ID
        Guid? activeId = null;
        string? activeName = null;

        if (active != null)
        {
            if (active.IsPlayerCharacter || active.IsVisibleToPlayers)
            {
                activeId = active.Id;
                activeName = active.Name;
            }
            else
            {
                activeId = null;
                activeName = "Turno del Enemigo";
            }
        }

        return new LiveCombatPlayerProjectionDto(
            state.PartyId,
            state.PartyName,
            state.Status.ToString(),
            state.CurrentRound,
            state.CurrentTurnIndex,
            activeId,
            activeName,
            playerCombatants
        );
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() }
    };

    public string? SerializeState(Guid partyId)
    {
        if (!_states.TryGetValue(partyId, out var state))
        {
            return null;
        }

        return JsonSerializer.Serialize(state, JsonOptions);
    }

    public LiveCombatState? RehydrateState(Guid partyId, string stateJson)
    {
        if (string.IsNullOrWhiteSpace(stateJson))
        {
            return null;
        }

        try
        {
            var state = JsonSerializer.Deserialize<LiveCombatState>(stateJson, JsonOptions);
            if (state == null)
            {
                return null;
            }

            state.PartyId = partyId;
            _states[partyId] = state;
            return state;
        }
        catch
        {
            return null;
        }
    }

    public void ClearState(Guid partyId)
    {
        _states.TryRemove(partyId, out _);
    }
}
