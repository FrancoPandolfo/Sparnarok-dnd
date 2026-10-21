using Microsoft.EntityFrameworkCore;
using Sparnarok.Api.Application.Interfaces;
using Sparnarok.Api.Domain.Entities;
using Sparnarok.Api.Domain.Enums;
using Sparnarok.Api.Infrastructure.Persistence;

namespace Sparnarok.Api.Infrastructure.Repositories;

/// <summary>
/// Implementación de repositorio para <see cref="WeaponMastery"/> basada en Entity Framework Core.
/// </summary>
public class WeaponMasteryRepository : IWeaponMasteryRepository
{
    private readonly SparnarokDbContext _context;

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="WeaponMasteryRepository"/>.
    /// </summary>
    /// <param name="context">Contexto de base de datos EF Core.</param>
    public WeaponMasteryRepository(SparnarokDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<WeaponMastery>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.WeaponMasteries
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<WeaponMastery?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.WeaponMasteries
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<WeaponMastery?> GetByPropertyAsync(MasteryProperty property, CancellationToken cancellationToken = default)
    {
        return await _context.WeaponMasteries
            .FirstOrDefaultAsync(m => m.Property == property, cancellationToken);
    }
}
