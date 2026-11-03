using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sparnarok.Api.Application.DTOs;
using Sparnarok.Api.Application.Interfaces;
using Sparnarok.Api.Domain.Entities;
using Sparnarok.Api.Infrastructure.Persistence;

namespace Sparnarok.Api.Infrastructure.Services;

/// <summary>
/// Implementación del servicio de búsqueda con Lazy Ingestion y proxy transparente a Open5e.
/// Realiza consultas sobre PostgreSQL (Neon) y, ante resultados insuficientes,
/// ingesta, mapea (dados, daño y Weapon Masteries 2024) y persiste asíncronamente
/// monstruos desde la API de Open5e para ponerlos a disposición inmediata de Monte Carlo.
/// </summary>
public class CreatureSearchService : ICreatureSearchService
{
    private readonly SparnarokDbContext _context;
    private readonly IOpen5eBestiaryClient _open5eClient;
    private readonly IOpen5eSyncService _open5eSyncService;
    private readonly ILogger<CreatureSearchService> _logger;

    private const int MinLocalResultsThreshold = 3;

    public CreatureSearchService(
        SparnarokDbContext context,
        IOpen5eBestiaryClient open5eClient,
        IOpen5eSyncService open5eSyncService,
        ILogger<CreatureSearchService> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _open5eClient = open5eClient ?? throw new ArgumentNullException(nameof(open5eClient));
        _open5eSyncService = open5eSyncService ?? throw new ArgumentNullException(nameof(open5eSyncService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CreatureResponseDto>> SearchCreaturesAsync(
        string term,
        Guid? currentUserId = null,
        CancellationToken ct = default)
    {
        var trimmed = term?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return Array.Empty<CreatureResponseDto>();
        }

        var lowerTerm = trimmed.ToLowerInvariant();

        // 1. Búsqueda en almacén local (PostgreSQL) con eager loading completo
        var localQuery = _context.Creatures
            .AsNoTracking()
            .Include(c => c.Actions).ThenInclude(a => a.AssociatedMastery)
            .Include(c => c.KnownMasteries)
            .Include(c => c.EquippedArmor)
            .Include(c => c.EquippedShield)
            .Where(c => !c.IsPlayerCharacter || c.OwnerUserId == currentUserId)
            .Where(c => c.Name.ToLower().Contains(lowerTerm));

        var localCreatures = await localQuery.ToListAsync(ct);

        // 2. Si hay resultados suficientes, devolverlos directamente
        if (localCreatures.Count >= MinLocalResultsThreshold)
        {
            return localCreatures
                .OrderBy(c => c.Name)
                .Select(CreatureResponseDto.FromEntity)
                .ToList();
        }

        // 3. Lazy Ingestion: Consultar transparentemente la API de Open5e
        _logger.LogInformation(
            "Lazy Ingestion: Resultados locales insuficientes ({Count}) para '{Term}'. Consultando Open5e...",
            localCreatures.Count, trimmed);

        Open5eMonsterListResponse open5eResponse;
        try
        {
            open5eResponse = await _open5eClient.SearchMonstersAsync(trimmed, limit: 15, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fallo al consultar Open5e durante Lazy Ingestion de '{Term}'. Retornando resultados locales.", trimmed);
            return localCreatures
                .OrderBy(c => c.Name)
                .Select(CreatureResponseDto.FromEntity)
                .ToList();
        }

        if (open5eResponse.Results == null || open5eResponse.Results.Count == 0)
        {
            return localCreatures
                .OrderBy(c => c.Name)
                .Select(CreatureResponseDto.FromEntity)
                .ToList();
        }

        // 4. Traducción y Mapeo en Tiempo Real
        var existingNames = new HashSet<string>(
            await _context.Creatures.Select(c => c.Name.ToLower()).ToListAsync(ct),
            StringComparer.OrdinalIgnoreCase);

        var availableMasteries = await _context.WeaponMasteries
            .ToDictionaryAsync(m => m.Property, ct);

        var newlyIngested = new List<Creature>();

        foreach (var monsterDto in open5eResponse.Results)
        {
            var name = monsterDto.Name?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name)) continue;

            if (existingNames.Contains(name))
            {
                // Ya existe en la base de datos local
                continue;
            }

            try
            {
                var creature = _open5eSyncService.MapToCreature(monsterDto, availableMasteries);
                _context.Creatures.Add(creature);
                newlyIngested.Add(creature);
                existingNames.Add(name);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al mapear monstruo de Open5e '{Name}' durante Lazy Ingestion.", name);
            }
        }

        // 5. Persistencia Silenciosa asíncrona en PostgreSQL
        if (newlyIngested.Count > 0)
        {
            try
            {
                await _context.SaveChangesAsync(ct);
                _logger.LogInformation(
                    "Lazy Ingestion: Persistidas silenciosamente {Count} nuevas criaturas de Open5e en la base de datos.",
                    newlyIngested.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al persistir silenciosamente nuevas criaturas en la base de datos.");
            }
        }

        // 6. Lista unificada de resultados (locales existentes + recién ingestadas)
        var combined = localCreatures
            .Concat(newlyIngested)
            .GroupBy(c => c.Id)
            .Select(g => g.First())
            .OrderBy(c => c.Name)
            .Select(CreatureResponseDto.FromEntity)
            .ToList();

        return combined;
    }
}
