using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Sparnarok.Api.Application.DTOs;
using Sparnarok.Api.Application.Interfaces;
using Sparnarok.Api.Domain.Entities;
using Sparnarok.Api.Domain.Enums;
using Sparnarok.Api.Infrastructure.Persistence;

namespace Sparnarok.Api.Infrastructure.Services;

/// <summary>
/// Servicio de aplicación que implementa la ingesta masiva y Bulk Upsert para el catálogo de <see cref="Armor"/>.
/// </summary>
public class ArmorIngestionService : IArmorIngestionService
{
    private readonly ICsvParserService _csvParser;
    private readonly SparnarokDbContext _context;

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="ArmorIngestionService"/>.
    /// </summary>
    public ArmorIngestionService(ICsvParserService csvParser, SparnarokDbContext context)
    {
        _csvParser = csvParser ?? throw new ArgumentNullException(nameof(csvParser));
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc />
    public async Task<IngestionResultDto> IngestArmorsAsync(
        Stream csvStream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(csvStream);

        var records = await _csvParser.ParseCsvAsync<ArmorCsvRecord, ArmorCsvClassMap>(
            csvStream,
            cancellationToken);

        var errors = new List<string>();
        var validRecords = new List<(ArmorCsvRecord Record, ArmorCategory Category, int? StrengthReq, bool StealthDisadv)>();

        int rowIndex = 1;
        foreach (var record in records)
        {
            rowIndex++;
            if (string.IsNullOrWhiteSpace(record.Name))
            {
                errors.Add($"Fila {rowIndex}: El nombre de la armadura es obligatorio.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(record.ArmorClass))
            {
                errors.Add($"Fila {rowIndex} ('{record.Name}'): La fórmula de Clase de Armadura es obligatoria.");
                continue;
            }

            if (!Enum.TryParse<ArmorCategory>(record.Category?.Trim(), ignoreCase: true, out var category))
            {
                errors.Add($"Fila {rowIndex} ('{record.Name}'): Categoría de armadura '{record.Category}' no reconocida (válidas: Light, Medium, Heavy, Shield).");
                continue;
            }

            if (record.Weight.HasValue && record.Weight.Value < 0)
            {
                errors.Add($"Fila {rowIndex} ('{record.Name}'): El peso no puede ser negativo ({record.Weight.Value}).");
                continue;
            }

            // Normalización de guiones "-" para campos opcionales
            int? strengthRequirement = null;
            var rawStrength = record.Strength?.Trim();
            if (!string.IsNullOrWhiteSpace(rawStrength) && rawStrength != "-")
            {
                var matchStr = Regex.Match(rawStrength, @"\d+");
                if (matchStr.Success && int.TryParse(matchStr.Value, out var strVal))
                {
                    strengthRequirement = strVal;
                }
            }

            bool stealthDisadvantage = false;
            var rawStealth = record.Stealth?.Trim();
            if (!string.IsNullOrWhiteSpace(rawStealth) && rawStealth != "-")
            {
                stealthDisadvantage = rawStealth.Contains("Disadvantage", StringComparison.OrdinalIgnoreCase);
            }

            validRecords.Add((record, category, strengthRequirement, stealthDisadvantage));
        }

        if (validRecords.Count == 0)
        {
            return new IngestionResultDto(records.Count, 0, 0, errors);
        }

        // Consultar armaduras existentes en la base de datos por nombre
        var candidateNames = validRecords
            .Select(r => r.Record.Name.Trim().ToLowerInvariant())
            .Distinct()
            .ToList();

        var existingArmors = await _context.Armors
            .Where(a => candidateNames.Contains(a.Name.ToLower()))
            .ToListAsync(cancellationToken);

        var existingLookup = existingArmors
            .ToDictionary(a => a.Name.Trim(), StringComparer.OrdinalIgnoreCase);

        int insertedCount = 0;
        int updatedCount = 0;

        foreach (var (rec, cat, strReq, stealthDisadv) in validRecords)
        {
            var trimmedName = rec.Name.Trim();
            var weightVal = rec.Weight ?? 0;
            var rawCost = rec.Cost?.Trim() ?? string.Empty;
            if (rawCost == "-") rawCost = string.Empty;

            if (existingLookup.TryGetValue(trimmedName, out var existingArmor))
            {
                // Actualización (Upsert)
                existingArmor.Update(
                    cat,
                    rec.ArmorClass.Trim(),
                    strReq,
                    stealthDisadv,
                    weightVal,
                    rawCost);

                updatedCount++;
            }
            else
            {
                // Inserción
                var newArmor = new Armor(
                    trimmedName,
                    cat,
                    rec.ArmorClass.Trim(),
                    strReq,
                    stealthDisadv,
                    weightVal,
                    rawCost);

                _context.Armors.Add(newArmor);
                existingLookup[trimmedName] = newArmor;
                insertedCount++;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        return new IngestionResultDto(
            TotalProcessed: records.Count,
            Inserted: insertedCount,
            Updated: updatedCount,
            Errors: errors);
    }
}
