using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sparnarok.Api.Domain.Entities;

namespace Sparnarok.Api.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuración Fluent API para la entidad <see cref="Armor"/>.
/// </summary>
public class ArmorConfiguration : IEntityTypeConfiguration<Armor>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Armor> builder)
    {
        builder.ToTable("Armors");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Name)
            .HasMaxLength(150)
            .IsRequired();

        // Índice único por nombre para optimizar búsquedas durante la ingesta masiva (Upsert)
        builder.HasIndex(a => a.Name)
            .IsUnique();

        builder.Property(a => a.Category)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(a => a.RawArmorClass)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(a => a.BaseArmorClass)
            .IsRequired();

        builder.Property(a => a.ShieldBonus)
            .IsRequired();

        builder.Property(a => a.AllowsDexterityModifier)
            .IsRequired();

        builder.Property(a => a.MaxDexterityModifier)
            .IsRequired(false);

        builder.Property(a => a.StrengthRequirement)
            .IsRequired(false);

        builder.Property(a => a.StealthDisadvantage)
            .IsRequired();

        builder.Property(a => a.Weight)
            .IsRequired();

        builder.Property(a => a.Cost)
            .HasMaxLength(50);
    }
}
