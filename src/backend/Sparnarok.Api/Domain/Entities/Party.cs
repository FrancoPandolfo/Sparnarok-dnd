namespace Sparnarok.Api.Domain.Entities;

/// <summary>
/// Entidad de dominio que modela un grupo estructurado de aventureros o personajes de jugadores (Party).
/// Mantiene una relación Many-to-Many con criaturas que son Personajes de Jugadores (IsPlayerCharacter == true).
/// Incluye inventario colaborativo (Stash), tesoro común (Party Fund), cola de fabricación (Downtime Crafting)
/// y registro de auditoría (Activity Log).
/// </summary>
public class Party
{
    private readonly List<Creature> _members = [];
    private readonly List<StashItem> _stashItems = [];
    private readonly List<CraftingQueueItem> _craftingQueue = [];
    private readonly List<StashActivityLog> _activityLogs = [];
    private readonly List<SessionNote> _sessionNotes = [];
    private readonly List<PartyMember> _partyMembers = [];

    /// <summary>Identificador único de la Party.</summary>
    public Guid Id { get; private set; }

    /// <summary>Nombre representativo del grupo de héroes (ej. 'Los Protectores de la Costa').</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Identificador del usuario propietario y creador de la Party.</summary>
    public Guid OwnerUserId { get; private set; }

    /// <summary>Token único para invitación y enlace de onboarding a la Party (Fase 3.7).</summary>
    public string InviteToken { get; private set; } = Guid.NewGuid().ToString("N");

    /// <summary>Fecha y hora UTC de creación de la Party.</summary>
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    #region Party Fund (Monedas y Tesoro Común)
    /// <summary>Piezas de cobre (PC / cp).</summary>
    public decimal CopperPieces { get; private set; } = 0;

    /// <summary>Piezas de plata (PP / sp).</summary>
    public decimal SilverPieces { get; private set; } = 0;

    /// <summary>Piezas de electro (PE / ep).</summary>
    public decimal ElectrumPieces { get; private set; } = 0;

    /// <summary>Piezas de oro (PO / gp).</summary>
    public decimal GoldPieces { get; private set; } = 0;

    /// <summary>Piezas de platino (PL / pp).</summary>
    public decimal PlatinumPieces { get; private set; } = 0;

    /// <summary>Total consolidado en su equivalente a Piezas de Oro (PO / gp).</summary>
    public decimal TotalGoldEquivalent =>
        (PlatinumPieces * 10m) + GoldPieces + (ElectrumPieces * 0.5m) + (SilverPieces * 0.1m) + (CopperPieces * 0.01m);
    #endregion

    /// <summary>Colección de héroes y personajes que componen la Party.</summary>
    public IReadOnlyCollection<Creature> Members => _members.AsReadOnly();

    /// <summary>Colección de objetos y tesoros en el alijo compartido (Collaborative Party Stash).</summary>
    public IReadOnlyCollection<StashItem> StashItems => _stashItems.AsReadOnly();

    /// <summary>Cola de objetos actualmente en fabricación durante el tiempo de descanso (Downtime Crafting).</summary>
    public IReadOnlyCollection<CraftingQueueItem> CraftingQueue => _craftingQueue.AsReadOnly();

    /// <summary>Historial de transacciones y auditoría del alijo y fondos.</summary>
    public IReadOnlyCollection<StashActivityLog> ActivityLogs => _activityLogs.AsReadOnly();

    /// <summary>Notas de sesión y apuntes de campaña de los integrantes de la Party (Fase 3.7).</summary>
    public IReadOnlyCollection<SessionNote> SessionNotes => _sessionNotes.AsReadOnly();

    /// <summary>Colección de miembros y jugadores registrados con sus roles y nombres en partida (Fase 3.7).</summary>
    public IReadOnlyCollection<PartyMember> PartyMembers => _partyMembers.AsReadOnly();

    /// <summary>Peso total acumulado en libras (lbs) de todos los objetos en el alijo de la Party.</summary>
    public decimal TotalStashWeight => _stashItems.Sum(i => i.Weight * i.Quantity);

    /// <summary>
    /// Estado serializado en JSON del combate activo de la Party para persistencia y rehidratación (Fase 3.8+).
    /// </summary>
    public string? CurrentCombatState { get; private set; }

    /// <summary>Constructor protegido para Entity Framework Core.</summary>
    protected Party() { }

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="Party"/>.
    /// </summary>
    public Party(Guid id, string name, Guid ownerUserId, string? inviteToken = null)
    {
        Id = id;
        SetName(name);
        OwnerUserId = ownerUserId;
        InviteToken = string.IsNullOrWhiteSpace(inviteToken) ? Guid.NewGuid().ToString("N") : inviteToken.Trim();
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Incorpora o actualiza un integrante en la Party con su rol y nombre en partida.
    /// </summary>
    public void AddPartyMember(PartyMember member)
    {
        ArgumentNullException.ThrowIfNull(member);
        var existing = _partyMembers.FirstOrDefault(m => m.UserId == member.UserId);
        if (existing != null)
        {
            existing.SetRole(member.Role);
            existing.SetInGameName(member.InGameName);
        }
        else
        {
            _partyMembers.Add(member);
        }
    }

    /// <summary>
    /// Remueve un integrante de la membresía de la Party por su identificador de usuario.
    /// </summary>
    public void RemovePartyMember(Guid userId)
    {
        _partyMembers.RemoveAll(m => m.UserId == userId);
    }

    /// <summary>
    /// Regenera el token de invitación para invalidar enlaces compartidos previamente.
    /// </summary>
    public void RegenerateInviteToken()
    {
        InviteToken = Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// Actualiza el nombre de la Party asegurando su no vacuidad.
    /// </summary>
    public void SetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("El nombre de la Party no puede estar vacío.", nameof(name));
        }

        Name = name.Trim();
    }

    /// <summary>
    /// Actualiza los fondos del Party Fund asegurando que ninguna denominación sea negativa.
    /// </summary>
    public void UpdateFunds(decimal cp, decimal sp, decimal ep, decimal gp, decimal pp)
    {
        if (cp < 0 || sp < 0 || ep < 0 || gp < 0 || pp < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cp), "Ninguna cantidad de monedas en el Party Fund puede ser negativa.");
        }

        CopperPieces = cp;
        SilverPieces = sp;
        ElectrumPieces = ep;
        GoldPieces = gp;
        PlatinumPieces = pp;
    }

    /// <summary>
    /// Descuenta una cantidad en piezas de oro (gp) del fondo de la party.
    /// </summary>
    public void DeductGold(decimal amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "La cantidad de oro a descontar no puede ser negativa.");
        }

        if (GoldPieces < amount)
        {
            throw new InvalidOperationException($"Fondos insuficientes: se requieren {amount:F1} PO, pero el alijo dispone de {GoldPieces:F1} PO.");
        }

        GoldPieces -= amount;
    }

    /// <summary>
    /// Incrementa las piezas de oro (gp) del fondo de la party.
    /// </summary>
    public void AddGold(decimal amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "La cantidad de oro a añadir no puede ser negativa.");
        }

        GoldPieces += amount;
    }

    /// <summary>
    /// Añade un personaje a la Party, garantizando que sea un Personaje de Jugador (PC).
    /// </summary>
    public void AddMember(Creature creature)
    {
        ArgumentNullException.ThrowIfNull(creature);

        if (!creature.IsPlayerCharacter)
        {
            throw new InvalidOperationException($"La criatura '{creature.Name}' no es un Personaje de Jugador y no puede formar parte de una Party.");
        }

        if (!_members.Any(m => m.Id == creature.Id))
        {
            _members.Add(creature);
        }
    }

    /// <summary>
    /// Remueve un integrante de la Party por su identificador.
    /// </summary>
    public void RemoveMember(Guid creatureId)
    {
        _members.RemoveAll(m => m.Id == creatureId);
    }

    /// <summary>
    /// Despeja todos los integrantes actuales de la Party.
    /// </summary>
    public void ClearMembers()
    {
        _members.Clear();
    }

    /// <summary>
    /// Añade un objeto al alijo compartido de la Party.
    /// </summary>
    public void AddStashItem(StashItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _stashItems.Add(item);
    }

    /// <summary>
    /// Remueve un objeto del alijo compartido de la Party por su identificador.
    /// </summary>
    public void RemoveStashItem(Guid itemId)
    {
        _stashItems.RemoveAll(i => i.Id == itemId);
    }

    /// <summary>
    /// Añade un nuevo trabajo a la cola de fabricación de la party.
    /// </summary>
    public void AddCraftingItem(CraftingQueueItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _craftingQueue.Add(item);
    }

    /// <summary>
    /// Remueve un trabajo de la cola de fabricación.
    /// </summary>
    public void RemoveCraftingItem(Guid itemId)
    {
        _craftingQueue.RemoveAll(i => i.Id == itemId);
    }

    /// <summary>
    /// Registra un evento de auditoría en el historial de la party.
    /// </summary>
    public void AddActivityLog(StashActivityLog log)
    {
        ArgumentNullException.ThrowIfNull(log);
        _activityLogs.Add(log);
    }

    /// <summary>
    /// Actualiza el estado serializado del combate táctico activo de la Party.
    /// </summary>
    /// <param name="stateJson">Cadena JSON del estado completo o null para despejar.</param>
    public void SetCurrentCombatState(string? stateJson)
    {
        CurrentCombatState = stateJson;
    }
}
