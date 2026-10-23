namespace Sparnarok.Api.Domain.Models;

/// <summary>
/// Estados de ciclo de vida para un combate en vivo de una Party.
/// </summary>
public enum LiveCombatStatus
{
    /// <summary>El combate no ha sido inicializado.</summary>
    NotStarted,

    /// <summary>El combate fue creado y está a la espera de que los jugadores carguen su iniciativa.</summary>
    WaitingForInitiative,

    /// <summary>El combate está activo, con orden de iniciativa y turnos en progreso.</summary>
    Active,

    /// <summary>El combate ha finalizado.</summary>
    Ended
}

/// <summary>
/// Modelo de combatiente en memoria para el tracker interactivo de combate.
/// </summary>
public class LiveCombatant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public decimal? Initiative { get; set; }
    public int MaxHp { get; set; }
    public int CurrentHp { get; set; }
    public int ArmorClass { get; set; }
    public bool IsPlayerCharacter { get; set; }
    public bool IsVisibleToPlayers { get; set; } = true;
    public Guid? AssignedPlayerUserId { get; set; }
    public string? Conditions { get; set; }

    public LiveCombatant() { }

    public LiveCombatant(
        Guid id,
        string name,
        int maxHp,
        int armorClass,
        bool isPlayerCharacter,
        bool isVisibleToPlayers = true,
        Guid? assignedPlayerUserId = null,
        decimal? initiative = null)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        Name = name.Trim();
        MaxHp = Math.Max(1, maxHp);
        CurrentHp = MaxHp;
        ArmorClass = Math.Max(1, armorClass);
        IsPlayerCharacter = isPlayerCharacter;
        IsVisibleToPlayers = isVisibleToPlayers;
        AssignedPlayerUserId = assignedPlayerUserId;
        Initiative = initiative;
    }
}

/// <summary>
/// Estado completo en memoria de un combate activo para una Party específica.
/// </summary>
public class LiveCombatState
{
    public Guid PartyId { get; set; }
    public string PartyName { get; set; } = string.Empty;
    public LiveCombatStatus Status { get; set; } = LiveCombatStatus.NotStarted;
    public int CurrentRound { get; set; } = 1;
    public int CurrentTurnIndex { get; set; } = 0;
    public List<LiveCombatant> Combatants { get; set; } = [];

    public LiveCombatant? GetCurrentCombatant()
    {
        if (Status != LiveCombatStatus.Active || Combatants.Count == 0)
        {
            return null;
        }

        var index = Math.Clamp(CurrentTurnIndex, 0, Combatants.Count - 1);
        return Combatants[index];
    }
}
