using Sparnarok.Api.Domain.Enums;

namespace Sparnarok.Api.Application.DTOs;

/// <summary>
/// Modelo de solicitud para dar de alta una nueva criatura en el Códice.
/// </summary>
/// <param name="Name">Nombre representativo de la criatura.</param>
/// <param name="Size">Categoría de tamaño (ej. Medium, Large).</param>
/// <param name="Type">Tipo biológico/mágico (ej. Humanoid, Dragon).</param>
/// <param name="Alignment">Alineamiento ético y moral.</param>
/// <param name="ChallengeRating">Valor de Desafío (CR, de 0 a 30).</param>
/// <param name="SpeedFeet">Velocidad terrestre base en pies.</param>
/// <param name="Strength">Puntuación de Fuerza (1 a 30).</param>
/// <param name="Dexterity">Puntuación de Destreza (1 a 30).</param>
/// <param name="Constitution">Puntuación de Constitución (1 a 30).</param>
/// <param name="Intelligence">Puntuación de Inteligencia (1 a 30).</param>
/// <param name="Wisdom">Puntuación de Sabiduría (1 a 30).</param>
/// <param name="Charisma">Puntuación de Carisma (1 a 30).</param>
/// <param name="MaxHitPoints">Puntos de golpe máximos (mayor a 0).</param>
/// <param name="ArmorClassBase">CA base (mínimo 1).</param>
/// <param name="ShieldBonus">Bono por escudo (por defecto 0).</param>
/// <param name="AllowsDexterityModifier">Si suma la Destreza a la CA (por defecto true).</param>
/// <param name="MaxDexterityModifier">Límite de Destreza en armadura (opcional).</param>
public record CreateCreatureRequestDto(
    string Name,
    CreatureSize Size,
    CreatureType Type,
    string Alignment,
    double ChallengeRating,
    int SpeedFeet = 30,
    int Strength = 10,
    int Dexterity = 10,
    int Constitution = 10,
    int Intelligence = 10,
    int Wisdom = 10,
    int Charisma = 10,
    int MaxHitPoints = 10,
    int ArmorClassBase = 10,
    int ShieldBonus = 0,
    bool AllowsDexterityModifier = true,
    int? MaxDexterityModifier = null);

/// <summary>
/// Modelo de solicitud para actualizar los datos base de una criatura existente.
/// </summary>
public record UpdateCreatureRequestDto(
    string Name,
    CreatureSize Size,
    CreatureType Type,
    string Alignment,
    double ChallengeRating,
    int SpeedFeet,
    int Strength,
    int Dexterity,
    int Constitution,
    int Intelligence,
    int Wisdom,
    int Charisma,
    int MaxHitPoints,
    int ArmorClassBase,
    int ShieldBonus,
    bool AllowsDexterityModifier,
    int? MaxDexterityModifier);
