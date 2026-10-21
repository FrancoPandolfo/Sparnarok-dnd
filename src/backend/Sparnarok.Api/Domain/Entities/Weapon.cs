using Sparnarok.Api.Domain.Enums;

namespace Sparnarok.Api.Domain.Entities;

/// <summary>
/// Entidad de dominio que representa un arma de combate en D&amp;D 5e (2024),
/// con su fórmula de daño base, tipo de daño, propiedades tácticas y costo.
/// </summary>
public class Weapon
{
    /// <summary>Identificador único del arma.</summary>
    public Guid Id { get; private set; }

    /// <summary>Nombre canónico del arma (ej. "Longsword", "Dagger").</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Fórmula de dados de daño base (ej. "1d8", "1d4", "2d6").</summary>
    public string Damage { get; private set; } = string.Empty;

    /// <summary>Tipo de daño infligido (ej. Slashing, Piercing, Bludgeoning).</summary>
    public DamageType DamageType { get; private set; }

    /// <summary>Propiedades tácticas delimitadas por coma (ej. "Versatile (1d10), Martial").</summary>
    public string Properties { get; private set; } = string.Empty;

    /// <summary>Peso del arma en libras.</summary>
    public double Weight { get; private set; }

    /// <summary>Costo de mercado en monedas (ej. "15 gp", "2 gp", "5 sp").</summary>
    public string Cost { get; private set; } = string.Empty;

    /// <summary>Constructor protegido para EF Core.</summary>
    protected Weapon() { }

    /// <summary>
    /// Crea una nueva arma de D&amp;D 5e.
    /// </summary>
    /// <param name="name">Nombre del arma.</param>
    /// <param name="damage">Fórmula de daño.</param>
    /// <param name="damageType">Tipo de daño.</param>
    /// <param name="properties">Propiedades de armas.</param>
    /// <param name="weight">Peso en libras.</param>
    /// <param name="cost">Costo en monedas.</param>
    /// <param name="id">Identificador único opcional.</param>
    public Weapon(
        string name,
        string damage,
        DamageType damageType,
        string properties = "",
        double weight = 0,
        string cost = "",
        Guid? id = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre del arma es obligatorio.", nameof(name));

        if (string.IsNullOrWhiteSpace(damage))
            throw new ArgumentException("La fórmula de daño del arma es obligatoria.", nameof(damage));

        if (weight < 0)
            throw new ArgumentOutOfRangeException(nameof(weight), "El peso del arma no puede ser negativo.");

        Id = id ?? Guid.NewGuid();
        Name = name.Trim();
        Damage = damage.Trim();
        DamageType = damageType;
        Properties = properties?.Trim() ?? string.Empty;
        Weight = weight;
        Cost = cost?.Trim() ?? string.Empty;
    }

    /// <summary>
    /// Actualiza los atributos tácticos, daño y costo del arma durante procesos de ingesta idempotente (Upsert).
    /// </summary>
    public void Update(
        string damage,
        DamageType damageType,
        string properties,
        double weight,
        string cost)
    {
        if (string.IsNullOrWhiteSpace(damage))
            throw new ArgumentException("La fórmula de daño no puede estar vacía.", nameof(damage));

        if (weight < 0)
            throw new ArgumentOutOfRangeException(nameof(weight), "El peso del arma no puede ser negativo.");

        Damage = damage.Trim();
        DamageType = damageType;
        Properties = properties?.Trim() ?? string.Empty;
        Weight = weight;
        Cost = cost?.Trim() ?? string.Empty;
    }
}
