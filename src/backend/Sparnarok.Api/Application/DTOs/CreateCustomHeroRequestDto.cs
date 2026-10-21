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

    /// <summary>Nombre del ataque o habilidad táctica primaria del héroe.</summary>
    public string AttackName { get; set; } = "Ataque Principal";

    /// <summary>Fórmula de dados de daño del ataque (ej. "1d8", "2d6", "1d10").</summary>
    public string DamageFormula { get; set; } = "1d8";

    /// <summary>Característica gobernante para la tirada de ataque del héroe.</summary>
    public AbilityScoreType AttackModifierAbility { get; set; } = AbilityScoreType.Strength;
}
