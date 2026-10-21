using Sparnarok.Api.Domain.Entities;
using Sparnarok.Api.Domain.Enums;

namespace Sparnarok.Api.Application.DTOs;

/// <summary>
/// Modelo de transferencia que describe completamente una criatura de D&amp;D 5e (2024),
/// incluyendo estadísticas, salud, defensas, acciones tácticas y maestrías aprendidas.
/// </summary>
public record CreatureResponseDto(
    Guid Id,
    string Name,
    CreatureSize Size,
    CreatureType Type,
    string Alignment,
    double ChallengeRating,
    int ProficiencyBonus,
    int SpeedFeet,
    int EffectiveArmorClass,
    bool IsBloodied,
    bool IsConscious,
    AbilityScoresDto AbilityScores,
    HitPointsDto HitPoints,
    ArmorClassDto ArmorClass,
    IReadOnlyList<ActionResponseDto> Actions,
    IReadOnlyList<WeaponMasteryResponseDto> KnownMasteries)
{
    /// <summary>
    /// Mapea una entidad <see cref="Creature"/> a su correspondiente DTO de respuesta.
    /// </summary>
    /// <param name="entity">Entidad de dominio.</param>
    /// <returns>Instancia de <see cref="CreatureResponseDto"/>.</returns>
    public static CreatureResponseDto FromEntity(Creature entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new CreatureResponseDto(
            entity.Id,
            entity.Name,
            entity.Size,
            entity.Type,
            entity.Alignment,
            entity.ChallengeRating,
            entity.ProficiencyBonus,
            entity.SpeedFeet,
            entity.EffectiveArmorClass,
            entity.HitPoints.IsBloodied,
            entity.HitPoints.IsConscious,
            new AbilityScoresDto(
                entity.AbilityScores.Strength,
                entity.AbilityScores.GetModifier(AbilityScoreType.Strength),
                entity.AbilityScores.Dexterity,
                entity.AbilityScores.GetModifier(AbilityScoreType.Dexterity),
                entity.AbilityScores.Constitution,
                entity.AbilityScores.GetModifier(AbilityScoreType.Constitution),
                entity.AbilityScores.Intelligence,
                entity.AbilityScores.GetModifier(AbilityScoreType.Intelligence),
                entity.AbilityScores.Wisdom,
                entity.AbilityScores.GetModifier(AbilityScoreType.Wisdom),
                entity.AbilityScores.Charisma,
                entity.AbilityScores.GetModifier(AbilityScoreType.Charisma)),
            new HitPointsDto(
                entity.HitPoints.Max,
                entity.HitPoints.Current,
                entity.HitPoints.Temporary),
            new ArmorClassDto(
                entity.ArmorClass.Base,
                entity.ArmorClass.ShieldBonus,
                entity.ArmorClass.AllowsDexterityModifier,
                entity.ArmorClass.MaxDexterityModifier),
            entity.Actions.Select(ActionResponseDto.FromEntity).ToList(),
            entity.KnownMasteries.Select(WeaponMasteryResponseDto.FromEntity).ToList());
    }
}

/// <summary>
/// Desglose de puntuaciones y modificadores calculados de características.
/// </summary>
public record AbilityScoresDto(
    int Strength,
    int StrengthModifier,
    int Dexterity,
    int DexterityModifier,
    int Constitution,
    int ConstitutionModifier,
    int Intelligence,
    int IntelligenceModifier,
    int Wisdom,
    int WisdomModifier,
    int Charisma,
    int CharismaModifier);

/// <summary>
/// Estado numérico de puntos de golpe.
/// </summary>
public record HitPointsDto(
    int Max,
    int Current,
    int Temporary);

/// <summary>
/// Configuración de Clase de Armadura y defensas.
/// </summary>
public record ArmorClassDto(
    int Base,
    int ShieldBonus,
    bool AllowsDexterityModifier,
    int? MaxDexterityModifier);
