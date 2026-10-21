namespace Sparnarok.Api.Domain.Enums;

/// <summary>
/// Categoría o tipo de armadura de acuerdo con las reglas oficiales de D&amp;D 5e (2024).
/// </summary>
public enum ArmorCategory
{
    /// <summary>
    /// Armadura ligera: añade el modificador completo de Destreza a la CA base.
    /// </summary>
    Light,

    /// <summary>
    /// Armadura media: añade el modificador de Destreza con un tope máximo de +2.
    /// </summary>
    Medium,

    /// <summary>
    /// Armadura pesada: no suma ni resta modificadores de Destreza a la CA base.
    /// </summary>
    Heavy,

    /// <summary>
    /// Escudo: se empuña para sumar un bono plano de +2 a la CA total.
    /// </summary>
    Shield
}
