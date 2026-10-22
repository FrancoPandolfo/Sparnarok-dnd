using Microsoft.EntityFrameworkCore;
using Sparnarok.Api.Application.Interfaces;
using Sparnarok.Api.Domain.Entities;
using Sparnarok.Api.Infrastructure.Persistence;

namespace Sparnarok.Api.Infrastructure.Repositories;

/// <summary>
/// Implementación de repositorio para <see cref="Encounter"/> basada en Entity Framework Core.
/// </summary>
public class EncounterRepository : IEncounterRepository
{
    private readonly SparnarokDbContext _context;

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="EncounterRepository"/>.
    /// </summary>
    public EncounterRepository(SparnarokDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Encounter>> GetAllByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Encounters
            .Include(e => e.Creatures)
                .ThenInclude(c => c.Actions)
                    .ThenInclude(a => a.AssociatedMastery)
            .Include(e => e.Creatures)
                .ThenInclude(c => c.KnownMasteries)
            .Include(e => e.Creatures)
                .ThenInclude(c => c.EquippedArmor)
            .Include(e => e.Creatures)
                .ThenInclude(c => c.EquippedShield)
            .Where(e => e.OwnerUserId == userId)
            .OrderByDescending(e => e.CreatedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Encounter?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Encounters
            .Include(e => e.Creatures)
                .ThenInclude(c => c.Actions)
                    .ThenInclude(a => a.AssociatedMastery)
            .Include(e => e.Creatures)
                .ThenInclude(c => c.KnownMasteries)
            .Include(e => e.Creatures)
                .ThenInclude(c => c.EquippedArmor)
            .Include(e => e.Creatures)
                .ThenInclude(c => c.EquippedShield)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task AddAsync(Encounter encounter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(encounter);
        await _context.Encounters.AddAsync(encounter, cancellationToken);
    }

    /// <inheritdoc />
    public Task UpdateAsync(Encounter encounter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(encounter);
        _context.Encounters.Update(encounter);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteAsync(Encounter encounter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(encounter);
        _context.Encounters.Remove(encounter);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }
}
