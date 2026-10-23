using Microsoft.EntityFrameworkCore;
using Sparnarok.Api.Application.Interfaces;
using Sparnarok.Api.Domain.Entities;
using Sparnarok.Api.Infrastructure.Persistence;

namespace Sparnarok.Api.Infrastructure.Repositories;

/// <summary>
/// Implementación de <see cref="ISessionNoteRepository"/> basada en Entity Framework Core y PostgreSQL.
/// </summary>
public class SessionNoteRepository : ISessionNoteRepository
{
    private readonly SparnarokDbContext _context;

    public SessionNoteRepository(SparnarokDbContext context)
    {
        _context = context;
    }

    public async Task<SessionNote?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.SessionNotes
            .FirstOrDefaultAsync(n => n.Id == id, ct);
    }

    public async Task<IReadOnlyList<SessionNote>> GetAllByPartyIdAsync(Guid partyId, CancellationToken ct = default)
    {
        return await _context.SessionNotes
            .Where(n => n.PartyId == partyId)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task AddAsync(SessionNote note, CancellationToken ct = default)
    {
        await _context.SessionNotes.AddAsync(note, ct);
    }

    public Task UpdateAsync(SessionNote note, CancellationToken ct = default)
    {
        _context.SessionNotes.Update(note);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(SessionNote note, CancellationToken ct = default)
    {
        _context.SessionNotes.Remove(note);
        return Task.CompletedTask;
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return await _context.SaveChangesAsync(ct);
    }
}
