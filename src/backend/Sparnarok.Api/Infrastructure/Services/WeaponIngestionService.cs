using Microsoft.EntityFrameworkCore;
using Sparnarok.Api.Application.DTOs;
using Sparnarok.Api.Application.Interfaces;
using Sparnarok.Api.Domain.Entities;
using Sparnarok.Api.Domain.Enums;
using Sparnarok.Api.Infrastructure.Persistence;

namespace Sparnarok.Api.Infrastructure.Services;

/// <summary>
/// Servicio de aplicación que implementa la lógica de ingesta masiva y Bulk Upsert para <see cref="Weapon"/>.
/// </summary>
public class WeaponIngestionService : IWeaponIngestionService
{
    private readonly ICsvParserService _csvParser;
    private readonly SparnarokDbContext _context;

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="WeaponIngestionService"/>.
    /// </summary>
    public WeaponIngestionService(ICsvParserService csvParser, SparnarokDbContext context)
    {
        _csvParser = csvParser;
        _context = context;
    }

    /// <inheritdoc />
    public async Task<IngestionResultDto> IngestWeaponsAsync(
        Stream csvStream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(csvStream);

        var records = await _csvParser.ParseCsvAsync<WeaponCsvRecord, WeaponCsvClassMap>(
            csvStream,
            cancellationToken);

        var errors = new List<string>();
        var validRecords = new List<(WeaponCsvRecord Record, DamageType DmgType)>();

        int rowIndex = 1; // 1-based, excluyendo encabezado
        foreach (var record in records)
        {
            rowIndex++;
            if (string.IsNullOrWhiteSpace(record.Name))
            {
                errors.Add($"Fila {rowIndex}: El nombre del arma es obligatorio.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(record.Damage))
            {
                errors.Add($"Fila {rowIndex} ('{record.Name}'): La fórmula de daño es obligatoria.");
                continue;
            }

            if (!Enum.TryParse<DamageType>(record.DamageType?.Trim(), ignoreCase: true, out var damageType))
            {
                errors.Add($"Fila {rowIndex} ('{record.Name}'): Tipo de daño '{record.DamageType}' no reconocido en las reglas 5e.");
                continue;
            }

            if (record.Weight.HasValue && record.Weight.Value < 0)
            {
                errors.Add($"Fila {rowIndex} ('{record.Name}'): El peso no puede ser negativo ({record.Weight.Value}).");
                continue;
            }

            validRecords.Add((record, damageType));
        }

        if (validRecords.Count == 0)
        {
            return new IngestionResultDto(records.Count, 0, 0, errors);
        }

        // Consultar armas existentes en la base de datos por nombre (case-insensitive)
        var candidateNames = validRecords.Select(r => r.Record.Name.Trim().ToLowerInvariant()).Distinct().ToList();

        var existingWeapons = await _context.Weapons
            .Where(w => candidateNames.Contains(w.Name.ToLower()))
            .ToListAsync(cancellationToken);

        var existingLookup = existingWeapons
            .ToDictionary(w => w.Name.Trim(), StringComparer.OrdinalIgnoreCase);

        int insertedCount = 0;
        int updatedCount = 0;

        foreach (var (rec, dmgType) in validRecords)
        {
            var trimmedName = rec.Name.Trim();
            var weightVal = rec.Weight ?? 0;
            var costVal = rec.Cost ?? string.Empty;
            var propsVal = rec.Properties ?? string.Empty;

            if (existingLookup.TryGetValue(trimmedName, out var existingWeapon))
            {
                // Actualización (Upsert)
                existingWeapon.Update(
                    rec.Damage.Trim(),
                    dmgType,
                    propsVal,
                    weightVal,
                    costVal);

                updatedCount++;
            }
            else
            {
                // Inserción masiva
                var newWeapon = new Weapon(
                    trimmedName,
                    rec.Damage.Trim(),
                    dmgType,
                    propsVal,
                    weightVal,
                    costVal);

                _context.Weapons.Add(newWeapon);
                existingLookup[trimmedName] = newWeapon; // Evitar duplicados dentro del mismo lote
                insertedCount++;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        return new IngestionResultDto(records.Count, insertedCount, updatedCount, errors);
    }
}
