using Microsoft.EntityFrameworkCore;
using Sparnarok.Api.Application.Interfaces;
using Sparnarok.Api.Domain.Entities;
using Sparnarok.Api.Infrastructure.Persistence;

namespace Sparnarok.Api.Infrastructure.Repositories;

/// <summary>
/// Implementación de repositorio para <see cref="Party"/> basada en Entity Framework Core.
/// </summary>
public class PartyRepository : IPartyRepository
{
    private readonly SparnarokDbContext _context;

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="PartyRepository"/>.
    /// </summary>
    public PartyRepository(SparnarokDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Party>> GetAllByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Parties
            .Include(p => p.Members)
                .ThenInclude(m => m.Actions)
                    .ThenInclude(a => a.AssociatedMastery)
            .Include(p => p.Members)
                .ThenInclude(m => m.KnownMasteries)
            .Include(p => p.Members)
                .ThenInclude(m => m.EquippedArmor)
            .Include(p => p.Members)
                .ThenInclude(m => m.EquippedShield)
            .Include(p => p.StashItems)
            .Where(p => p.OwnerUserId == userId)
            .OrderByDescending(p => p.CreatedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Party?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Parties
            .Include(p => p.Members)
                .ThenInclude(m => m.Actions)
                    .ThenInclude(a => a.AssociatedMastery)
            .Include(p => p.Members)
                .ThenInclude(m => m.KnownMasteries)
            .Include(p => p.Members)
                .ThenInclude(m => m.EquippedArmor)
            .Include(p => p.Members)
                .ThenInclude(m => m.EquippedShield)
            .Include(p => p.StashItems)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task AddAsync(Party party, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(party);
        await _context.Parties.AddAsync(party, cancellationToken);
    }

    /// <inheritdoc />
    public Task UpdateAsync(Party party, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(party);
        _context.Parties.Update(party);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteAsync(Party party, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(party);
        _context.Parties.Remove(party);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task AddStashItemAsync(StashItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        await _context.StashItems.AddAsync(item, cancellationToken);
    }

    /// <inheritdoc />
    public Task RemoveStashItemAsync(StashItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        _context.StashItems.Remove(item);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<Party?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Parties
            .Include(p => p.Members)
                .ThenInclude(m => m.Actions)
                    .ThenInclude(a => a.AssociatedMastery)
            .Include(p => p.Members)
                .ThenInclude(m => m.KnownMasteries)
            .Include(p => p.Members)
                .ThenInclude(m => m.EquippedArmor)
            .Include(p => p.Members)
                .ThenInclude(m => m.EquippedShield)
            .Include(p => p.StashItems)
            .Include(p => p.CraftingQueue)
            .Include(p => p.ActivityLogs)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task AddCraftingItemAsync(CraftingQueueItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        await _context.CraftingQueueItems.AddAsync(item, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CraftingQueueItem>> GetCraftingQueueAsync(Guid partyId, CancellationToken cancellationToken = default)
    {
        return await _context.CraftingQueueItems
            .Where(c => c.PartyId == partyId)
            .OrderByDescending(c => c.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task AddActivityLogAsync(StashActivityLog log, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(log);
        await _context.StashActivityLogs.AddAsync(log, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StashActivityLog>> GetActivityLogsAsync(Guid partyId, int limit = 50, CancellationToken cancellationToken = default)
    {
        return await _context.StashActivityLogs
            .Where(a => a.PartyId == partyId)
            .OrderByDescending(a => a.TimestampUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }
}
