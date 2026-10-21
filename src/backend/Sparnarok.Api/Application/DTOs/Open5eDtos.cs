using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sparnarok.Api.Application.DTOs;

/// <summary>
/// Respuesta paginada devuelta por el endpoint de monstruos de la API de Open5e.
/// </summary>
public class Open5eMonsterListResponse
{
    /// <summary>Cantidad total de monstruos en la base de datos de Open5e.</summary>
    [JsonPropertyName("count")]
    public int Count { get; set; }

    /// <summary>URL para acceder a la siguiente página de resultados.</summary>
    [JsonPropertyName("next")]
    public string? Next { get; set; }

    /// <summary>URL para acceder a la página previa.</summary>
    [JsonPropertyName("previous")]
    public string? Previous { get; set; }

    /// <summary>Colección de monstruos incluidos en la página actual.</summary>
    [JsonPropertyName("results")]
    public List<Open5eMonsterDto> Results { get; set; } = [];
}

/// <summary>
/// DTO con la estructura de un monstruo en la API de Open5e v1.
/// </summary>
public class Open5eMonsterDto
{
    [JsonPropertyName("slug")]
    public string Slug { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public string Size { get; set; } = "Medium";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "Monstrosity";

    [JsonPropertyName("subtype")]
    public string? Subtype { get; set; }

    [JsonPropertyName("alignment")]
    public string? Alignment { get; set; }

    [JsonPropertyName("armor_class")]
    public int ArmorClass { get; set; } = 10;

    [JsonPropertyName("armor_desc")]
    public string? ArmorDesc { get; set; }

    [JsonPropertyName("hit_points")]
    public int HitPoints { get; set; } = 10;

    [JsonPropertyName("hit_dice")]
    public string? HitDice { get; set; }

    [JsonPropertyName("speed")]
    public JsonElement? Speed { get; set; }

    [JsonPropertyName("strength")]
    public int Strength { get; set; } = 10;

    [JsonPropertyName("dexterity")]
    public int Dexterity { get; set; } = 10;

    [JsonPropertyName("constitution")]
    public int Constitution { get; set; } = 10;

    [JsonPropertyName("intelligence")]
    public int Intelligence { get; set; } = 10;

    [JsonPropertyName("wisdom")]
    public int Wisdom { get; set; } = 10;

    [JsonPropertyName("charisma")]
    public int Charisma { get; set; } = 10;

    [JsonPropertyName("challenge_rating")]
    public string? ChallengeRating { get; set; }

    [JsonPropertyName("cr")]
    public double? Cr { get; set; }

    [JsonPropertyName("actions")]
    public List<Open5eActionDto>? Actions { get; set; }
}

/// <summary>
/// DTO que representa una acción táctica o ataque de monstruo en Open5e.
/// </summary>
public class Open5eActionDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("desc")]
    public string Desc { get; set; } = string.Empty;

    [JsonPropertyName("attack_bonus")]
    public int? AttackBonus { get; set; }

    [JsonPropertyName("damage_dice")]
    public string? DamageDice { get; set; }
}
