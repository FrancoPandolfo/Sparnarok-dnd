using Sparnarok.Api.Domain.Entities;
using Sparnarok.Api.Domain.Enums;
using Sparnarok.Api.Domain.ValueObjects;

namespace Sparnarok.Api.Domain.Services.Combat;

/// <summary>
/// Calculador canónico de Clase de Armadura (AC) dinámica para D&amp;D 5e (2024).
/// Resuelve las combinaciones de Destreza, Armadura Ligera, Media, Pesada y Escudos.
/// </summary>
public static class ArmorClassCalculator
{
    /// <summary>
    /// Calcula la Clase de Armadura total en tiempo de ejecución a partir del modificador de Destreza,
    /// la armadura corporal equipada y la presencia de un escudo.
    /// </summary>
    /// <param name="dexterityModifier">Modificador de Destreza de la criatura.</param>
    /// <param name="equippedArmor">Pieza de armadura corporal vestida (o null si está sin armadura).</param>
    /// <param name="hasShield">Indica si la criatura tiene empuñado un escudo.</param>
    /// <returns>Valor total de CA calculado.</returns>
    public static int CalculateAc(int dexterityModifier, Armor? equippedArmor, bool hasShield = false)
    {
        int baseAc;

        if (equippedArmor == null || equippedArmor.Category == ArmorCategory.Shield)
        {
            // Sin armadura: 10 + Modificador de Destreza
            baseAc = 10 + dexterityModifier;
        }
        else
        {
            switch (equippedArmor.Category)
            {
                case ArmorCategory.Light:
                    // Armadura Ligera: CA Base + Modificador de Destreza
                    baseAc = equippedArmor.BaseArmorClass + dexterityModifier;
                    break;

                case ArmorCategory.Medium:
                    // Armadura Media: CA Base + Modificador de Destreza (con un límite máximo de +2)
                    var effectiveDex = Math.Min(dexterityModifier, 2);
                    baseAc = equippedArmor.BaseArmorClass + effectiveDex;
                    break;

                case ArmorCategory.Heavy:
                    // Armadura Pesada: CA Base pura (se ignora el modificador de Destreza)
                    baseAc = equippedArmor.BaseArmorClass;
                    break;

                default:
                    baseAc = 10 + dexterityModifier;
                    break;
            }
        }

        // Escudos: Suma un +2 a la CA total si hay uno equipado
        if (hasShield || equippedArmor?.Category == ArmorCategory.Shield)
        {
            baseAc += 2;
        }

        return baseAc;
    }

    /// <summary>
    /// Sobrecarga que calcula la CA dinámica integral de una criatura considerando
    /// sus puntuaciones de características, armadura vestida, escudo empuñado o defensas naturales.
    /// </summary>
    /// <param name="creature">Instancia de la criatura.</param>
    /// <returns>Valor total de CA efectiva.</returns>
    public static int CalculateAc(Creature creature)
    {
        ArgumentNullException.ThrowIfNull(creature);

        var dexMod = creature.AbilityScores.GetModifier(AbilityScoreType.Dexterity);
        var hasShield = creature.EquippedShield != null || creature.ArmorClass.ShieldBonus > 0;

        // Si tiene armadura corporal equipada en su ranura, manda la armadura
        if (creature.EquippedArmor != null)
        {
            return CalculateAc(dexMod, creature.EquippedArmor, hasShield);
        }

        // Si no tiene armadura equipada pero el monstruo tiene una configuración base distinta a Unarmored (ej. piel gruesa, armadura natural)
        if (creature.ArmorClass != ArmorClass.Unarmored)
        {
            var shieldBonus = creature.EquippedShield != null ? 2 : creature.ArmorClass.ShieldBonus;
            var compositeAc = new ArmorClass(
                creature.ArmorClass.Base,
                shieldBonus,
                creature.ArmorClass.AllowsDexterityModifier,
                creature.ArmorClass.MaxDexterityModifier);

            return compositeAc.CalculateTotal(dexMod);
        }

        // Sin armadura estándar
        return CalculateAc(dexMod, null, hasShield);
    }
}
