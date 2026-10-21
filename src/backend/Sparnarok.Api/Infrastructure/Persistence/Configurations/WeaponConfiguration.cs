using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sparnarok.Api.Domain.Entities;

namespace Sparnarok.Api.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuración Fluent API para la entidad <see cref="Weapon"/>.
/// </summary>
public class WeaponConfiguration : IEntityTypeConfiguration<Weapon>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Weapon> builder)
    {
        builder.ToTable("Weapons");

        builder.HasKey(w => w.Id);

        builder.Property(w => w.Name)
            .HasMaxLength(150)
            .IsRequired();

        // Índice para optimizar búsquedas por nombre durante la ingesta masiva (Upsert)
        builder.HasIndex(w => w.Name)
            .IsUnique();

        builder.Property(w => w.Damage)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(w => w.DamageType)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(w => w.Properties)
            .HasMaxLength(500);

        builder.Property(w => w.Weight)
            .IsRequired();

        builder.Property(w => w.Cost)
            .HasMaxLength(50);
    }
}
