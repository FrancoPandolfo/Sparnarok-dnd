using Sparnarok.Api.Domain.Enums;

namespace Sparnarok.Api.Domain.Entities;

/// <summary>
/// Entidad que representa una propiedad de Maestría de Armas según las reglas de D&amp;D 5e (2024).
/// </summary>
public class WeaponMastery
{
    /// <summary>Identificador único de la maestría.</summary>
    public Guid Id { get; private set; }

    /// <summary>Tipo de propiedad de maestría formal (2024).</summary>
    public MasteryProperty Property { get; private set; }

    /// <summary>Nombre representativo de la maestría.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Descripción detallada de la mecánica y efecto.</summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>Requisito o tipo de arma que admite esta maestría (ej. 'Pesada, Cuerpo a cuerpo', 'Ligera').</summary>
    public string PrerequisiteWeapon { get; private set; } = string.Empty;

    /// <summary>Constructor protegido para EF Core.</summary>
    protected WeaponMastery() { }

    /// <summary>
    /// Crea una nueva maestría de armas.
    /// </summary>
    /// <param name="property">Propiedad formal de maestría.</param>
    /// <param name="name">Nombre de la maestría.</param>
    /// <param name="description">Efecto mecánico detallado.</param>
    /// <param name="prerequisiteWeapon">Tipo de arma compatible.</param>
    /// <param name="id">Identificador opcional (si no se provee se genera uno nuevo).</param>
    public WeaponMastery(
        MasteryProperty property,
        string name,
        string description,
        string prerequisiteWeapon,
        Guid? id = null)
    {
        Id = id ?? Guid.NewGuid();
        Property = property;
        Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("El nombre no puede estar vacío.", nameof(name)) : name;
        Description = string.IsNullOrWhiteSpace(description) ? throw new ArgumentException("La descripción no puede estar vacía.", nameof(description)) : description;
        PrerequisiteWeapon = prerequisiteWeapon ?? string.Empty;
    }

    /// <summary>
    /// Fábrica canónica con las ocho propiedades oficiales de Maestría de Armas 2024.
    /// </summary>
    public static class Presets
    {
        /// <summary>Maestría Cleave (Hendedura).</summary>
        public static WeaponMastery Cleave => new(
            MasteryProperty.Cleave,
            "Cleave",
            "Si impactas a una criatura con un arma cuerpo a cuerpo pesada, puedes hacer una tirada de ataque contra una segunda criatura a 5 pies.",
            "Melee, Heavy");

        /// <summary>Maestría Graze (Rozadura).</summary>
        public static WeaponMastery Graze => new(
            MasteryProperty.Graze,
            "Graze",
            "Si fallas una tirada de ataque, infliges daño igual al modificador de la característica usada para el ataque.",
            "Melee, Heavy");

        /// <summary>Maestría Nick (Tajo Rápido).</summary>
        public static WeaponMastery Nick => new(
            MasteryProperty.Nick,
            "Nick",
            "Permite realizar el ataque adicional de la propiedad Ligera como parte de la acción de Ataque en lugar de una Acción Adicional.",
            "Light");

        /// <summary>Maestría Push (Empujón).</summary>
        public static WeaponMastery Push => new(
            MasteryProperty.Push,
            "Push",
            "Si impactas a una criatura Grande o menor, puedes empujarla hasta 10 pies en línea recta.",
            "Heavy, Two-Handed or Versatile");

        /// <summary>Maestría Sap (Contusión).</summary>
        public static WeaponMastery Sap => new(
            MasteryProperty.Sap,
            "Sap",
            "Si impactas a una criatura, esta tiene desventaja en su próxima tirada de ataque antes del inicio de tu siguiente turno.",
            "Versatile or Melee");

        /// <summary>Maestría Slow (Frenado).</summary>
        public static WeaponMastery Slow => new(
            MasteryProperty.Slow,
            "Slow",
            "Si impactas e infliges daño, reduces la velocidad del objetivo en 10 pies hasta el inicio de tu próximo turno.",
            "Ranged or Melee");

        /// <summary>Maestría Topple (Derribo).</summary>
        public static WeaponMastery Topple => new(
            MasteryProperty.Topple,
            "Topple",
            "Si impactas a una criatura, esta debe superar una salvación de Constitución (CD 8 + Modificador + Bono Competencia) o quedar Derribada.",
            "Heavy or Versatile");

        /// <summary>Maestría Vex (Hostigamiento).</summary>
        public static WeaponMastery Vex => new(
            MasteryProperty.Vex,
            "Vex",
            "Si impactas e infliges daño, obtienes ventaja en tu próxima tirada de ataque contra ese objetivo antes del final de tu siguiente turno.",
            "Ammunition, Finesse or Light");
    }
}
