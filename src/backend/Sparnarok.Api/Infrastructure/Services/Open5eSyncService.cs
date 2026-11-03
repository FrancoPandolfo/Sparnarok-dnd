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

        // Cargar maestrías disponibles de la base de datos
        var masteries = await _context.WeaponMasteries.ToDictionaryAsync(m => m.Property, ct);

        // Cargar nombres de criaturas existentes en un diccionario para verificación eficiente
        var monsterNames = response.Results.Select(r => r.Name.Trim()).Distinct().ToList();
        var existingCreatures = await _context.Creatures
            .Include(c => c.Actions)
            .Include(c => c.KnownMasteries)
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

                    // Limpiar y re-asociar acciones con maestrías
                    existing.ClearActions();
                    AddActionsToCreature(existing, monsterDto.Actions, masteries);

                    updated++;
                }
                else
                {
                    // Dar de alta nueva criatura
                    var newCreature = MapToCreature(monsterDto, masteries);
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
        return MapToCreature(dto, null);
    }

    /// <inheritdoc />
    public Creature MapToCreature(Open5eMonsterDto dto, IReadOnlyDictionary<MasteryProperty, WeaponMastery>? masteries)
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

        AddActionsToCreature(creature, dto.Actions, masteries);
        return creature;
    }

    private static void AddActionsToCreature(
        Creature creature,
        List<Open5eActionDto>? actions,
        IReadOnlyDictionary<MasteryProperty, WeaponMastery>? masteries = null)
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

            // 1. Extraer damageFormula aislando "2d6 + 3" o "2d6+3" del texto descriptivo
            var formula = act.DamageDice;
            var match = Regex.Match(desc, @"\((\d+d\d+(?:\s*[+-]\s*\d+)?)\)");
            if (match.Success)
            {
                formula = match.Groups[1].Value.Replace(" ", "");
            }
            else if (string.IsNullOrWhiteSpace(formula))
            {
                formula = "1d6";
            }

            // 2. Extraer DamageType de forma heurística
            var damageType = DetermineDamageType(desc);

            // 3. Asignar heurísticamente WeaponMasteries (reglas 2024)
            var masteryProp = DetermineWeaponMastery(act.Name, desc);
            WeaponMastery? associatedMastery = null;
            if (masteryProp.HasValue && masteries != null && masteries.TryGetValue(masteryProp.Value, out var m))
            {
                associatedMastery = m;
            }

            var actionEntity = new Action(
                creature.Id,
                act.Name.Trim(),
                ActionType.Action,
                desc,
                isAttack: isAttack,
                reachOrRangeFeet: 5,
                damageFormula: formula,
                damageType: damageType,
                attackModifierAbility: AbilityScoreType.Strength,
                associatedMastery: associatedMastery);

            creature.AddAction(actionEntity);

            if (associatedMastery != null)
            {
                creature.LearnWeaponMastery(associatedMastery);
            }
        }
    }

    /// <summary>
    /// Determina heurísticamente el tipo de daño a partir de la descripción de la acción.
    /// </summary>
    public static DamageType DetermineDamageType(string desc)
    {
        var lower = desc.ToLowerInvariant();
        if (lower.Contains("piercing")) return DamageType.Piercing;
        if (lower.Contains("bludgeoning")) return DamageType.Bludgeoning;
        if (lower.Contains("slashing")) return DamageType.Slashing;
        if (lower.Contains("fire")) return DamageType.Fire;
        if (lower.Contains("cold")) return DamageType.Cold;
        if (lower.Contains("poison")) return DamageType.Poison;
        if (lower.Contains("acid")) return DamageType.Acid;
        if (lower.Contains("lightning")) return DamageType.Lightning;
        if (lower.Contains("necrotic")) return DamageType.Necrotic;
        if (lower.Contains("radiant")) return DamageType.Radiant;
        if (lower.Contains("psychic")) return DamageType.Psychic;
        if (lower.Contains("force")) return DamageType.Force;
        if (lower.Contains("thunder")) return DamageType.Thunder;
        return DamageType.Slashing;
    }

    /// <summary>
    /// Determina heurísticamente la propiedad de Maestría de Armas 2024 según el arma o ataque táctico.
    /// </summary>
    public static MasteryProperty? DetermineWeaponMastery(string actionName, string description)
    {
        var text = $"{actionName} {description}".ToLowerInvariant();

        // 1. Armas oficiales de D&D 5e (2024)
        if (Regex.IsMatch(text, @"\b(greataxe|halberd)\b")) return MasteryProperty.Cleave;
        if (Regex.IsMatch(text, @"\b(greatsword|glaive)\b")) return MasteryProperty.Graze;
        if (Regex.IsMatch(text, @"\b(scimitar|dagger|light hammer|sickle)\b")) return MasteryProperty.Nick;
        if (Regex.IsMatch(text, @"\b(greatclub|pike|warhammer|heavy crossbow)\b")) return MasteryProperty.Push;
        if (Regex.IsMatch(text, @"\b(morningstar|longsword|mace|flail|war pick|spear)\b")) return MasteryProperty.Sap;
        if (Regex.IsMatch(text, @"\b(club|javelin|light crossbow|longbow|musket|sling|whip)\b")) return MasteryProperty.Slow;
        if (Regex.IsMatch(text, @"\b(battleaxe|lance|maul|quarterstaff|trident)\b")) return MasteryProperty.Topple;
        if (Regex.IsMatch(text, @"\b(shortsword|rapier|handaxe|dart|shortbow|hand crossbow|blowgun|pistol)\b")) return MasteryProperty.Vex;

        // 2. Ataques naturales y roles tácticos (reglas 2024)
        if (Regex.IsMatch(text, @"\b(bite|gore|horns?|ram|tusk)\b")) return MasteryProperty.Topple;
        if (Regex.IsMatch(text, @"\b(claws?|talons?|rend)\b")) return MasteryProperty.Vex;
        if (Regex.IsMatch(text, @"\b(slam|tentacles?|tail|fist|smash|stomp)\b")) return MasteryProperty.Push;

        return null;
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
