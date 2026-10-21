using System.Text.RegularExpressions;
using Sparnarok.Api.Domain.Enums;
using Sparnarok.Api.Domain.ValueObjects;

namespace Sparnarok.Api.Domain.Entities;

/// <summary>
/// Entidad de dominio que modela una pieza de armadura o escudo en D&amp;D 5e (2024),
/// con su categoría, fórmula de CA, requisitos de fuerza, sigilo, peso y costo comercial.
/// </summary>
public class Armor
{
    /// <summary>Identificador único de la armadura o escudo.</summary>
    public Guid Id { get; private set; }

    /// <summary>Nombre canónico de la armadura (ej. "Half Plate Armor", "Plate Armor", "Shield").</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Categoría según reglas 2024 (Light, Medium, Heavy, Shield).</summary>
    public ArmorCategory Category { get; private set; }

    /// <summary>Cadena textual original de la CA en el catálogo (ej. "15 + Dex modifier (max 2)", "+2").</summary>
    public string RawArmorClass { get; private set; } = string.Empty;

    /// <summary>Clase de armadura base numérica provista por la pieza.</summary>
    public int BaseArmorClass { get; private set; }

    /// <summary>Bono adicional provisto por escudo (típicamente +2 si es Shield, 0 en armaduras corporales).</summary>
    public int ShieldBonus { get; private set; }

    /// <summary>Indica si esta armadura suma el modificador de Destreza de la criatura.</summary>
    public bool AllowsDexterityModifier { get; private set; }

    /// <summary>Tope máximo aplicable del modificador de Destreza (ej. 2 para armadura media, null para ligera).</summary>
    public int? MaxDexterityModifier { get; private set; }

    /// <summary>Puntuación mínima de Fuerza requerida para no penalizar la velocidad (ej. 13, 15 o null si no requiere).</summary>
    public int? StrengthRequirement { get; private set; }

    /// <summary>Indica si vestir esta armadura impone desventaja en tiradas de Sigilo (Dexterity - Stealth).</summary>
    public bool StealthDisadvantage { get; private set; }

    /// <summary>Peso del equipo en libras.</summary>
    public double Weight { get; private set; }

    /// <summary>Costo de mercado en monedas (ej. "750 GP", "1,500 GP", "10 GP").</summary>
    public string Cost { get; private set; } = string.Empty;

    /// <summary>Constructor protegido para EF Core.</summary>
    protected Armor() { }

    /// <summary>
    /// Crea una nueva instancia de armadura o escudo en el dominio.
    /// </summary>
    /// <param name="name">Nombre de la armadura.</param>
    /// <param name="category">Categoría (Light, Medium, Heavy, Shield).</param>
    /// <param name="rawArmorClass">Fórmula cruda de CA.</param>
    /// <param name="strengthRequirement">Fuerza requerida (opcional).</param>
    /// <param name="stealthDisadvantage">Si causa desventaja en sigilo.</param>
    /// <param name="weight">Peso en libras.</param>
    /// <param name="cost">Costo comercial.</param>
    /// <param name="id">Identificador único opcional.</param>
    public Armor(
        string name,
        ArmorCategory category,
        string rawArmorClass,
        int? strengthRequirement = null,
        bool stealthDisadvantage = false,
        double weight = 0,
        string cost = "",
        Guid? id = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre de la armadura es obligatorio.", nameof(name));

        if (string.IsNullOrWhiteSpace(rawArmorClass))
            throw new ArgumentException("La fórmula de Clase de Armadura es obligatoria.", nameof(rawArmorClass));

        if (weight < 0)
            throw new ArgumentOutOfRangeException(nameof(weight), "El peso de la armadura no puede ser negativo.");

        Id = id ?? Guid.NewGuid();
        Name = name.Trim();
        Category = category;
        RawArmorClass = rawArmorClass.Trim();
        StrengthRequirement = strengthRequirement;
        StealthDisadvantage = stealthDisadvantage;
        Weight = weight;
        Cost = cost?.Trim() ?? string.Empty;

        // Parsear y poblar las métricas matemáticas de CA
        ApplyParsedArmorClass(RawArmorClass, Category);
    }

    /// <summary>
    /// Actualiza los atributos tácticos durante procesos de ingesta idempotente (Bulk Upsert).
    /// </summary>
    public void Update(
        ArmorCategory category,
        string rawArmorClass,
        int? strengthRequirement,
        bool stealthDisadvantage,
        double weight,
        string cost)
    {
        if (string.IsNullOrWhiteSpace(rawArmorClass))
            throw new ArgumentException("La fórmula de Clase de Armadura no puede estar vacía.", nameof(rawArmorClass));

        if (weight < 0)
            throw new ArgumentOutOfRangeException(nameof(weight), "El peso de la armadura no puede ser negativo.");

        Category = category;
        RawArmorClass = rawArmorClass.Trim();
        StrengthRequirement = strengthRequirement;
        StealthDisadvantage = stealthDisadvantage;
        Weight = weight;
        Cost = cost?.Trim() ?? string.Empty;

        ApplyParsedArmorClass(RawArmorClass, Category);
    }

    /// <summary>
    /// Parsea la cadena de texto de ArmorClass (ej. "11 + Dex modifier", "15 + Dex modifier (max 2)", "18", "+2")
    /// y asigna las propiedades de CA base, bonos de escudo y límites de destreza según la categoría.
    /// </summary>
    private void ApplyParsedArmorClass(string acString, ArmorCategory category)
    {
        var (baseAc, shieldBonus, allowsDex, maxDex) = ParseArmorClass(acString, category);
        BaseArmorClass = baseAc;
        ShieldBonus = shieldBonus;
        AllowsDexterityModifier = allowsDex;
        MaxDexterityModifier = maxDex;
    }

    /// <summary>
    /// Algoritmo de parseo formal para cadenas de CA de D&amp;D 5e (2024).
    /// </summary>
    public static (int BaseAc, int ShieldBonus, bool AllowsDex, int? MaxDex) ParseArmorClass(string acString, ArmorCategory category)
    {
        if (string.IsNullOrWhiteSpace(acString))
            throw new ArgumentException("La cadena de armadura no puede ser nula o vacía.", nameof(acString));

        var clean = acString.Trim();

        // Caso 1: Escudo (Shield) - e.g. "+2"
        if (category == ArmorCategory.Shield || clean.StartsWith('+'))
        {
            var matchBonus = Regex.Match(clean, @"\+(\d+)");
            int bonus = matchBonus.Success ? int.Parse(matchBonus.Groups[1].Value) : 2;
            return (BaseAc: 0, ShieldBonus: bonus, AllowsDex: false, MaxDex: null);
        }

        // Extraer el número base principal (ej. "11", "15", "18")
        var matchNumber = Regex.Match(clean, @"^(\d+)");
        if (!matchNumber.Success)
        {
            throw modernFormatException(clean);
        }

        int baseAc = int.Parse(matchNumber.Groups[1].Value);

        // Caso 2: Armadura Ligera (Light)
        if (category == ArmorCategory.Light)
        {
            return (BaseAc: baseAc, ShieldBonus: 0, AllowsDex: true, MaxDex: null);
        }

        // Caso 3: Armadura Media (Medium)
        if (category == ArmorCategory.Medium)
        {
            // Busca si especifica tope (max N) o asume el estándar 5e de 2
            var matchMax = Regex.Match(clean, @"max\s*(\d+)", RegexOptions.IgnoreCase);
            int maxCap = matchMax.Success ? int.Parse(matchMax.Groups[1].Value) : 2;
            return (BaseAc: baseAc, ShieldBonus: 0, AllowsDex: true, MaxDex: maxCap);
        }

        // Caso 4: Armadura Pesada (Heavy)
        if (category == ArmorCategory.Heavy)
        {
            return (BaseAc: baseAc, ShieldBonus: 0, AllowsDex: false, MaxDex: null);
        }

        // Fallback por defecto según texto si la categoría no coincidió
        bool hasDex = clean.Contains("Dex", StringComparison.OrdinalIgnoreCase);
        bool hasMax2 = clean.Contains("max 2", StringComparison.OrdinalIgnoreCase);

        return (
            BaseAc: baseAc,
            ShieldBonus: 0,
            AllowsDex: hasDex,
            MaxDex: hasMax2 ? 2 : null);
    }

    private static FormatException modernFormatException(string raw) =>
        new($"El formato de armadura '{raw}' no pudo ser reconocido como una fórmula válida de D&D 5e.");
}
