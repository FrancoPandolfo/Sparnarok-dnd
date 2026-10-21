namespace Sparnarok.Api.Domain.ValueObjects;

/// <summary>
/// Objeto de valor inmutable que modela la Clase de Armadura (AC) y sus componentes en D&amp;D 5e (2024).
/// </summary>
public record ArmorClass
{
    /// <summary>Valor base de la armadura o defensa natural.</summary>
    public int Base { get; init; }

    /// <summary>Bonificador aportado por escudo (típicamente +2).</summary>
    public int ShieldBonus { get; init; }

    /// <summary>Indica si la armadura permite aplicar el modificador de Destreza.</summary>
    public bool AllowsDexterityModifier { get; init; }

    /// <summary>Límite máximo de bono de Destreza permitido (null si no tiene tope, como en armadura ligera o sin armadura).</summary>
    public int? MaxDexterityModifier { get; init; }

    /// <summary>Constructor privado para deserialización y EF Core.</summary>
    private ArmorClass() { }

    /// <summary>
    /// Crea una nueva configuración de Clase de Armadura.
    /// </summary>
    /// <param name="baseValue">Valor base (mínimo 1).</param>
    /// <param name="shieldBonus">Bono por escudo (por defecto 0).</param>
    /// <param name="allowsDexterityModifier">Si suma Destreza (por defecto true).</param>
    /// <param name="maxDexterityModifier">Tope de Destreza (por defecto null).</param>
    public ArmorClass(
        int baseValue,
        int shieldBonus = 0,
        bool allowsDexterityModifier = true,
        int? maxDexterityModifier = null)
    {
        if (baseValue <= 0)
            throw new ArgumentOutOfRangeException(nameof(baseValue), baseValue, "La CA base debe ser mayor a cero.");

        if (shieldBonus < 0)
            throw new ArgumentOutOfRangeException(nameof(shieldBonus), shieldBonus, "El bono de escudo no puede ser negativo.");

        Base = baseValue;
        ShieldBonus = shieldBonus;
        AllowsDexterityModifier = allowsDexterityModifier;
        MaxDexterityModifier = maxDexterityModifier;
    }

    /// <summary>
    /// Defensa sin armadura por defecto (10 + Destreza).
    /// </summary>
    public static ArmorClass Unarmored => new(10, allowsDexterityModifier: true);

    /// <summary>
    /// Calcula la Clase de Armadura efectiva considerando el modificador de Destreza de la criatura.
    /// </summary>
    /// <param name="dexterityModifier">Modificador de Destreza de la criatura.</param>
    /// <returns>Valor numérico total de CA.</returns>
    public int CalculateTotal(int dexterityModifier)
    {
        var effectiveDex = AllowsDexterityModifier ? dexterityModifier : 0;
        if (MaxDexterityModifier.HasValue)
        {
            effectiveDex = Math.Min(effectiveDex, MaxDexterityModifier.Value);
        }

        return Base + ShieldBonus + effectiveDex;
    }
}
