using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sparnarok.Api.Domain.Entities;
using Action = Sparnarok.Api.Domain.Entities.Action;

namespace Sparnarok.Api.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuración Fluent API para la entidad <see cref="Action"/>.
/// </summary>
public class ActionConfiguration : IEntityTypeConfiguration<Action>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Action> builder)
    {
        builder.ToTable("Actions");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Name)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(a => a.Description)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(a => a.Type)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(a => a.DamageFormula)
            .HasMaxLength(50);

        builder.Property(a => a.DamageType)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(a => a.AttackModifierAbility)
            .HasConversion<string>()
            .HasMaxLength(50);

        // Relación con WeaponMastery (opcional, OnDelete SetNull)
        builder.HasOne(a => a.AssociatedMastery)
            .WithMany()
            .HasForeignKey(a => a.AssociatedMasteryId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
