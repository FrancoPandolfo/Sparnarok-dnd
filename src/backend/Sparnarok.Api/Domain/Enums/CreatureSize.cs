namespace Sparnarok.Api.Domain.Enums;

/// <summary>
/// Categorías de tamaño para criaturas en D&amp;D 5e (2024).
/// </summary>
public enum CreatureSize
{
    /// <summary>Diminuto (espacio típico: 2.5 x 2.5 pies).</summary>
    Tiny = 1,

    /// <summary>Pequeño (espacio típico: 5 x 5 pies).</summary>
    Small = 2,

    /// <summary>Mediano (espacio típico: 5 x 5 pies).</summary>
    Medium = 3,

    /// <summary>Grande (espacio típico: 10 x 10 pies).</summary>
    Large = 4,

    /// <summary>Enorme (espacio típico: 15 x 15 pies).</summary>
    Huge = 5,

    /// <summary>Gargantuesco (espacio típico: 20 x 20 pies o mayor).</summary>
    Gargantuan = 6
}
