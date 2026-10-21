using Microsoft.EntityFrameworkCore;
using Sparnarok.Api.Application.Interfaces;
using Sparnarok.Api.Domain.Entities;
using Sparnarok.Api.Infrastructure.Persistence;
using Action = Sparnarok.Api.Domain.Entities.Action;

namespace Sparnarok.Api.Infrastructure.Repositories;

/// <summary>
/// Implementación de repositorio para <see cref="Creature"/> basada en Entity Framework Core.
/// </summary>
public class CreatureRepository : ICreatureRepository
{
    private readonly SparnarokDbContext _context;

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="CreatureRepository"/>.
    /// </summary>
    /// <param name="context">Contexto de base de datos EF Core.</param>
    public CreatureRepository(SparnarokDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Creature>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Creatures
            .Include(c => c.Actions)
                .ThenInclude(a => a.AssociatedMastery)
            .Include(c => c.KnownMasteries)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Creature?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Creatures
            .Include(c => c.Actions)
                .ThenInclude(a => a.AssociatedMastery)
            .Include(c => c.KnownMasteries)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task AddAsync(Creature creature, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(creature);
        await _context.Creatures.AddAsync(creature, cancellationToken);
    }

    /// <inheritdoc />
    public async Task AddActionAsync(Action action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        await _context.Actions.AddAsync(action, cancellationToken);
    }

    /// <inheritdoc />
    public Task UpdateAsync(Creature creature, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(creature);
        _context.Creatures.Update(creature);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteAsync(Creature creature, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(creature);
        _context.Creatures.Remove(creature);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }
}
