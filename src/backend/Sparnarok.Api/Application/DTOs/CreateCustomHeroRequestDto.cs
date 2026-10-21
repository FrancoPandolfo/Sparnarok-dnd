using Sparnarok.Api.Domain.Enums;

namespace Sparnarok.Api.Application.DTOs;

/// <summary>
/// Payload para dar de alta un perfil de combatiente customizado (Personaje de Jugador / Héroe)
/// utilizando estadísticas finales directas sin requerir un motor completo de creación de personajes.
/// </summary>
public class CreateCustomHeroRequestDto
{
    /// <summary>Nombre del héroe o aventurero (ej. "Sir Galahad", "Valeria").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Puntos de golpe máximos finales.</summary>
    public int MaxHitPoints { get; set; } = 30;

    /// <summary>Clase de Armadura final efectiva calculada.</summary>
    public int ArmorClass { get; set; } = 15;

    /// <summary>Puntuación de Fuerza (1 - 30).</summary>
    public int Strength { get; set; } = 10;

    /// <summary>Puntuación de Destreza (1 - 30).</summary>
    public int Dexterity { get; set; } = 10;

    /// <summary>Puntuación de Constitución (1 - 30).</summary>
    public int Constitution { get; set; } = 10;

    /// <summary>Puntuación de Inteligencia (1 - 30).</summary>
    public int Intelligence { get; set; } = 10;

    /// <summary>Puntuación de Sabiduría (1 - 30).</summary>
    public int Wisdom { get; set; } = 10;

    /// <summary>Puntuación de Carisma (1 - 30).</summary>
    public int Charisma { get; set; } = 10;

    /// <summary>Velocidad en pies (por defecto 30).</summary>
    public int SpeedFeet { get; set; } = 30;

    /// <summary>Nivel del personaje (1 a 20, por defecto 1).</summary>
    public int Level { get; set; } = 1;

    /// <summary>Nombre del arquetipo o clase (ej. 'Fighter 2H', 'Ranger Ranged').</summary>
    public string? Archetype { get; set; }

    /// <summary>Daño promedio por ronda esperado (Expected DPR). Si no se proporciona, se calcula a partir del arquetipo y nivel.</summary>
    public double? ExpectedDpr { get; set; }

    /// <summary>Nombre del ataque o habilidad táctica primaria del héroe (opcional si se usa DPR).</summary>
    public string? AttackName { get; set; } = "Ataque Principal";

    /// <summary>Fórmula de dados de daño del ataque (ej. "1d8", "2d6", "1d10").</summary>
    public string? DamageFormula { get; set; } = "1d8";

    /// <summary>Característica gobernante para la tirada de ataque del héroe.</summary>
    public AbilityScoreType AttackModifierAbility { get; set; } = AbilityScoreType.Strength;
}
