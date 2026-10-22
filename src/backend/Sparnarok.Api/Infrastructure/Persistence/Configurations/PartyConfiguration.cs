using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sparnarok.Api.Domain.Entities;

namespace Sparnarok.Api.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuración Fluent API para la entidad <see cref="Party"/>.
/// </summary>
public class PartyConfiguration : IEntityTypeConfiguration<Party>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Party> builder)
    {
        builder.ToTable("Parties");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(p => p.OwnerUserId)
            .IsRequired();

        builder.HasIndex(p => p.OwnerUserId);

        builder.Property(p => p.CreatedAt)
            .IsRequired();

        // Relación Many-to-Many con Creature (PartyCreatures)
        builder.HasMany(p => p.Members)
            .WithMany()
            .UsingEntity<Dictionary<string, object>>(
                "PartyCreatures",
                j => j.HasOne<Creature>().WithMany().HasForeignKey("CreatureId").OnDelete(DeleteBehavior.Cascade),
                j => j.HasOne<Party>().WithMany().HasForeignKey("PartyId").OnDelete(DeleteBehavior.Cascade)
            );

        builder.Navigation(p => p.Members)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Relación One-to-Many con StashItem
        builder.HasMany(p => p.StashItems)
            .WithOne()
            .HasForeignKey(s => s.PartyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(p => p.StashItems)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
