using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sparnarok.Api.Domain.Entities;
using Sparnarok.Api.Domain.Enums;

namespace Sparnarok.Api.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuración Fluent API para la entidad <see cref="WeaponMastery"/>.
/// </summary>
public class WeaponMasteryConfiguration : IEntityTypeConfiguration<WeaponMastery>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<WeaponMastery> builder)
    {
        builder.ToTable("WeaponMasteries");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(m => m.Property)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(m => m.Description)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(m => m.PrerequisiteWeapon)
            .HasMaxLength(150)
            .IsRequired();

        // Seed data canónico para las 8 maestrías oficiales de D&D 5e (2024)
        builder.HasData(
            new WeaponMastery(
                MasteryProperty.Cleave,
                "Cleave",
                "Si impactas a una criatura con un arma cuerpo a cuerpo pesada, puedes hacer una tirada de ataque contra una segunda criatura a 5 pies.",
                "Melee, Heavy",
                Guid.Parse("00000000-0000-0000-0000-000000000001")),
            new WeaponMastery(
                MasteryProperty.Graze,
                "Graze",
                "Si fallas una tirada de ataque, infliges daño igual al modificador de la característica usada para el ataque.",
                "Melee, Heavy",
                Guid.Parse("00000000-0000-0000-0000-000000000002")),
            new WeaponMastery(
                MasteryProperty.Nick,
                "Nick",
                "Permite realizar el ataque adicional de la propiedad Ligera como parte de la acción de Ataque en lugar de una Acción Adicional.",
                "Light",
                Guid.Parse("00000000-0000-0000-0000-000000000003")),
            new WeaponMastery(
                MasteryProperty.Push,
                "Push",
                "Si impactas a una criatura Grande o menor, puedes empujarla hasta 10 pies en línea recta.",
                "Heavy, Two-Handed or Versatile",
                Guid.Parse("00000000-0000-0000-0000-000000000004")),
            new WeaponMastery(
                MasteryProperty.Sap,
                "Sap",
                "Si impactas a una criatura, esta tiene desventaja en su próxima tirada de ataque antes del inicio de tu siguiente turno.",
                "Versatile or Melee",
                Guid.Parse("00000000-0000-0000-0000-000000000005")),
            new WeaponMastery(
                MasteryProperty.Slow,
                "Slow",
                "Si impactas e infliges daño, reduces la velocidad del objetivo en 10 pies hasta el inicio de tu próximo turno.",
                "Ranged or Melee",
                Guid.Parse("00000000-0000-0000-0000-000000000006")),
            new WeaponMastery(
                MasteryProperty.Topple,
                "Topple",
                "Si impactas a una criatura, esta debe superar una salvación de Constitución (CD 8 + Modificador + Bono Competencia) o quedar Derribada.",
                "Heavy or Versatile",
                Guid.Parse("00000000-0000-0000-0000-000000000007")),
            new WeaponMastery(
                MasteryProperty.Vex,
                "Vex",
                "Si impactas e infliges daño, obtienes ventaja en tu próxima tirada de ataque contra ese objetivo antes del final de tu siguiente turno.",
                "Ammunition, Finesse or Light",
                Guid.Parse("00000000-0000-0000-0000-000000000008"))
        );
    }
}
