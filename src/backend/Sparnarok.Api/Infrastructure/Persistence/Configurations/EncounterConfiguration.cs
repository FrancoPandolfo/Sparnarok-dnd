using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sparnarok.Api.Domain.Entities;

namespace Sparnarok.Api.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuración Fluent API para la entidad <see cref="Encounter"/>.
/// </summary>
public class EncounterConfiguration : IEntityTypeConfiguration<Encounter>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Encounter> builder)
    {
        builder.ToTable("Encounters");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(e => e.OwnerUserId)
            .IsRequired();

        builder.HasIndex(e => e.OwnerUserId);

        builder.Property(e => e.DifficultyNotes)
            .HasMaxLength(500)
            .IsRequired(false);

        builder.Property(e => e.CreatedAt)
            .IsRequired();

        // Relación Many-to-Many con Creature (EncounterCreatures)
        builder.HasMany(e => e.Creatures)
            .WithMany()
            .UsingEntity<Dictionary<string, object>>(
                "EncounterCreatures",
                j => j.HasOne<Creature>().WithMany().HasForeignKey("CreatureId").OnDelete(DeleteBehavior.Cascade),
                j => j.HasOne<Encounter>().WithMany().HasForeignKey("EncounterId").OnDelete(DeleteBehavior.Cascade)
            );

        builder.Navigation(e => e.Creatures)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
