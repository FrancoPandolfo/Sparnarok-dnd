namespace Sparnarok.Api.Application.DTOs;

/// <summary>
/// DTO de combatiente proyectado exclusivamente para el Dungeon Master (sin censura).
/// </summary>
public record LiveCombatantDmDto(
    Guid Id,
    string Name,
    decimal? Initiative,
    int MaxHp,
    int CurrentHp,
    int ArmorClass,
    bool IsPlayerCharacter,
    bool IsVisibleToPlayers,
    Guid? AssignedPlayerUserId,
    string? Conditions,
    int SortOrder = 0
);

/// <summary>
/// DTO de combatiente proyectado para jugadores (censurado para evitar metagaming).
/// Los monstruos invisibles se omiten completamente. Para monstruos visibles, CA y HP exactos se ocultan.
/// </summary>
public record LiveCombatantPlayerDto(
    Guid Id,
    string Name,
    decimal? Initiative,
    bool IsPlayerCharacter,
    int? CurrentHp,
    int? MaxHp,
    string HealthStatus,
    string? Conditions,
    int SortOrder = 0,
    Guid? AssignedPlayerUserId = null
);

/// <summary>
/// Proyección completa del estado del combate para el Dungeon Master.
/// </summary>
public record LiveCombatDmProjectionDto(
    Guid PartyId,
    string PartyName,
    string Status,
    int CurrentRound,
    int CurrentTurnIndex,
    Guid? ActiveCombatantId,
    string? ActiveCombatantName,
    IReadOnlyList<LiveCombatantDmDto> Combatants
);

/// <summary>
/// Proyección censurada del estado del combate para los Jugadores.
/// </summary>
public record LiveCombatPlayerProjectionDto(
    Guid PartyId,
    string PartyName,
    string Status,
    int CurrentRound,
    int CurrentTurnIndex,
    Guid? ActiveCombatantId,
    string? ActiveCombatantName,
    IReadOnlyList<LiveCombatantPlayerDto> Combatants
);

/// <summary>
/// Solicitud de inicialización de un combatiente individual.
/// </summary>
public record InitializeLiveCombatantRequest(
    Guid Id,
    string Name,
    int MaxHp,
    int ArmorClass,
    bool IsPlayerCharacter,
    bool IsVisibleToPlayers = true,
    Guid? AssignedPlayerUserId = null,
    decimal? Initiative = null
);

/// <summary>
/// Solicitud para inicializar un combate en vivo con combatientes.
/// </summary>
public record InitializeLiveCombatRequest(
    string PartyName,
    IReadOnlyList<InitializeLiveCombatantRequest> Combatants
);

/// <summary>
/// Solicitud de carga o modificación de iniciativa.
/// </summary>
public record SetInitiativeRequest(
    Guid CombatantId,
    decimal Initiative
);

/// <summary>
/// Solicitud de modificación de puntos de golpe (HP).
/// </summary>
public record ModifyCombatantHpRequest(
    Guid CombatantId,
    int DeltaHp
);

/// <summary>
/// Solicitud de conmutación de visibilidad de un combatiente.
/// </summary>
public record ToggleVisibilityRequest(
    Guid CombatantId,
    bool IsVisible
);

/// <summary>
/// Solicitud de reordenamiento posicional de un combatiente (Drag and Drop).
/// </summary>
public record ReorderCombatantRequest(
    Guid CombatantId,
    int NewIndex
);

/// <summary>
/// Solicitud para agregar un combatiente rápido en caliente directamente a la iniciativa activa (sin entidad persistida en BD).
/// </summary>
public record AddQuickCombatantRequest(
    string Name,
    int MaxHp,
    int ArmorClass,
    bool IsPlayerCharacter = false,
    bool IsVisibleToPlayers = true,
    decimal? Initiative = null
);


