using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sparnarok.Api.Application.DTOs;
using Sparnarok.Api.Application.Interfaces;
using Sparnarok.Api.Domain.Entities;
using Sparnarok.Api.Domain.Enums;
using Sparnarok.Api.Domain.ValueObjects;
using Sparnarok.Api.Infrastructure.Persistence;
using Action = Sparnarok.Api.Domain.Entities.Action;

namespace Sparnarok.Api.Infrastructure.Services;

/// <summary>
/// Implementación del servicio de sincronización e ingesta masiva de monstruos de Open5e.
/// </summary>
public class Open5eSyncService : IOpen5eSyncService
{
    private readonly IOpen5eBestiaryClient _bestiaryClient;
    private readonly SparnarokDbContext _context;
    private readonly ILogger<Open5eSyncService> _logger;

    public Open5eSyncService(
        IOpen5eBestiaryClient bestiaryClient,
        SparnarokDbContext context,
        ILogger<Open5eSyncService> logger)
    {
        _bestiaryClient = bestiaryClient;
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IngestionResultDto> SyncMonstersAsync(int limit = 50, CancellationToken ct = default)
    {
        _logger.LogInformation("Iniciando sincronización de monstruos desde Open5e (límite={Limit})...", limit);

        Open5eMonsterListResponse response;
        try
        {
            response = await _bestiaryClient.GetMonstersAsync(limit, 1, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo al consultar el cliente de Open5e.");
            return new IngestionResultDto(0, 0, 0, [$"Fallo de conexión con Open5e: {ex.Message}"]);
        }

        var totalProcessed = 0;
        var inserted = 0;
        var updated = 0;
        var errors = new List<string>();

        // Cargar nombres de criaturas existentes en un diccionario para verificación eficiente
        var monsterNames = response.Results.Select(r => r.Name.Trim()).Distinct().ToList();
        var existingCreatures = await _context.Creatures
            .Include(c => c.Actions)
            .Where(c => monsterNames.Contains(c.Name))
            .ToDictionaryAsync(c => c.Name, StringComparer.OrdinalIgnoreCase, ct);

        foreach (var monsterDto in response.Results)
        {
            totalProcessed++;
            try
            {
                var creatureName = monsterDto.Name.Trim();
                if (string.IsNullOrWhiteSpace(creatureName))
                {
                    errors.Add($"Fila #{totalProcessed}: Nombre de criatura inválido o en blanco.");
                    continue;
                }

                if (existingCreatures.TryGetValue(creatureName, out var existing))
                {
                    var abilities = new AbilityScores(
                        Math.Clamp(monsterDto.Strength, 1, 30),
                        Math.Clamp(monsterDto.Dexterity, 1, 30),
                        Math.Clamp(monsterDto.Constitution, 1, 30),
                        Math.Clamp(monsterDto.Intelligence, 1, 30),
                        Math.Clamp(monsterDto.Wisdom, 1, 30),
                        Math.Clamp(monsterDto.Charisma, 1, 30));

                    var hpVal = Math.Max(1, monsterDto.HitPoints);
                    var hp = new HitPoints(hpVal, hpVal);
                    var acVal = Math.Max(1, monsterDto.ArmorClass);
                    var ac = new ArmorClass(acVal, 0, allowsDexterityModifier: false);

                    existing.UpdateStatistics(
                        creatureName,
                        MapToCreatureSize(monsterDto.Size),
                        MapToCreatureType(monsterDto.Type),
                        string.IsNullOrWhiteSpace(monsterDto.Alignment) ? "Unaligned" : monsterDto.Alignment.Trim(),
                        ParseChallengeRating(monsterDto),
                        ParseSpeed(monsterDto.Speed),
                        abilities,
                        hp,
                        ac);

                    // Limpiar y re-asociar acciones
                    existing.ClearActions();
                    AddActionsToCreature(existing, monsterDto.Actions);

                    updated++;
                }
                else
                {
                    // Dar de alta nueva criatura
                    var newCreature = MapToCreature(monsterDto);
                    _context.Creatures.Add(newCreature);
                    existingCreatures[creatureName] = newCreature;
                    inserted++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al sincronizar criatura '{Name}'.", monsterDto.Name);
                errors.Add($"Error al sincronizar '{monsterDto.Name}': {ex.Message}");
            }
        }

        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Sincronización Open5e finalizada. Procesados={Total}, Insertados={Inserted}, Actualizados={Updated}, Errores={Errors}",
            totalProcessed, inserted, updated, errors.Count);

        return new IngestionResultDto(totalProcessed, inserted, updated, errors);
    }

    /// <inheritdoc />
    public Creature MapToCreature(Open5eMonsterDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var name = string.IsNullOrWhiteSpace(dto.Name) ? "Unknown Monster" : dto.Name.Trim();
        var size = MapToCreatureSize(dto.Size);
        var type = MapToCreatureType(dto.Type);
        var alignment = string.IsNullOrWhiteSpace(dto.Alignment) ? "Unaligned" : dto.Alignment.Trim();
        var cr = ParseChallengeRating(dto);
        var speed = ParseSpeed(dto.Speed);

        var abilities = new AbilityScores(
            Math.Clamp(dto.Strength, 1, 30),
            Math.Clamp(dto.Dexterity, 1, 30),
            Math.Clamp(dto.Constitution, 1, 30),
            Math.Clamp(dto.Intelligence, 1, 30),
            Math.Clamp(dto.Wisdom, 1, 30),
            Math.Clamp(dto.Charisma, 1, 30));

        var hpVal = Math.Max(1, dto.HitPoints);
        var hp = new HitPoints(hpVal, hpVal);

        // La CA de Open5e es la CA final calculada del monstruo; se desactiva destreza adicional para evitar duplicación.
        var acVal = Math.Max(1, dto.ArmorClass);
        var ac = new ArmorClass(acVal, 0, allowsDexterityModifier: false);

        var creature = new Creature(
            name,
            size,
            type,
            alignment,
            cr,
            speed,
            abilities,
            hp,
            ac,
            isPlayerCharacter: false);

        AddActionsToCreature(creature, dto.Actions);
        return creature;
    }

    private static void AddActionsToCreature(Creature creature, List<Open5eActionDto>? actions)
    {
        if (actions == null || actions.Count == 0) return;

        foreach (var act in actions)
        {
            if (string.IsNullOrWhiteSpace(act.Name)) continue;

            var desc = act.Desc ?? string.Empty;
            var isAttack = act.AttackBonus.HasValue ||
                           !string.IsNullOrWhiteSpace(act.DamageDice) ||
                           desc.Contains("Attack:", StringComparison.OrdinalIgnoreCase) ||
                           desc.Contains("Hit:", StringComparison.OrdinalIgnoreCase);

            var formula = act.DamageDice;
            if (string.IsNullOrWhiteSpace(formula))
            {
                var match = Regex.Match(desc, @"\((\d+d\d+(?:\s*[+-]\s*\d+)?)\)");
                formula = match.Success ? match.Groups[1].Value.Replace(" ", "") : "1d6";
            }

            var actionEntity = new Action(
                creature.Id,
                act.Name.Trim(),
                ActionType.Action,
                desc,
                isAttack: isAttack,
                reachOrRangeFeet: 5,
                damageFormula: formula,
                damageType: DamageType.Slashing,
                attackModifierAbility: AbilityScoreType.Strength);

            creature.AddAction(actionEntity);
        }
    }

    public static CreatureSize MapToCreatureSize(string? sizeStr)
    {
        if (string.IsNullOrWhiteSpace(sizeStr)) return CreatureSize.Medium;
        if (Enum.TryParse<CreatureSize>(sizeStr.Trim(), ignoreCase: true, out var size)) return size;
        return CreatureSize.Medium;
    }

    public static CreatureType MapToCreatureType(string? typeStr)
    {
        if (string.IsNullOrWhiteSpace(typeStr)) return CreatureType.Monstrosity;
        var clean = typeStr.Trim().Split(' ', ',', '-')[0];
        if (Enum.TryParse<CreatureType>(clean, ignoreCase: true, out var type)) return type;
        return CreatureType.Monstrosity;
    }

    public static double ParseChallengeRating(Open5eMonsterDto dto)
    {
        if (dto.Cr.HasValue && dto.Cr.Value >= 0) return dto.Cr.Value;
        if (string.IsNullOrWhiteSpace(dto.ChallengeRating)) return 1.0;
        var str = dto.ChallengeRating.Trim();
        if (str == "1/2") return 0.5;
        if (str == "1/4") return 0.25;
        if (str == "1/8") return 0.125;
        if (double.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out var cr)) return cr;
        return 1.0;
    }

    public static int ParseSpeed(JsonElement? speedElem)
    {
        if (!speedElem.HasValue) return 30;
        var elem = speedElem.Value;

        if (elem.ValueKind == JsonValueKind.Object)
        {
            if (elem.TryGetProperty("walk", out var walkElem))
            {
                if (walkElem.ValueKind == JsonValueKind.Number) return walkElem.GetInt32();
                if (walkElem.ValueKind == JsonValueKind.String && int.TryParse(walkElem.GetString()?.Split(' ')[0], out var speed))
                    return speed;
            }
        }
        else if (elem.ValueKind == JsonValueKind.Number)
        {
            return elem.GetInt32();
        }

        return 30;
    }
}
