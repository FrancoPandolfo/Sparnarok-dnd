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

        builder.Property(p => p.InviteToken)
            .HasMaxLength(64)
            .IsRequired();

        builder.HasIndex(p => p.InviteToken)
            .IsUnique();

        builder.Property(p => p.CreatedAt)
            .IsRequired();

        builder.Property(p => p.CurrentCombatState)
            .IsRequired(false);

        // Fondos de la Party (Party Fund)
        builder.Property(p => p.CopperPieces)
            .HasPrecision(18, 2)
            .HasDefaultValue(0);

        builder.Property(p => p.SilverPieces)
            .HasPrecision(18, 2)
            .HasDefaultValue(0);

        builder.Property(p => p.ElectrumPieces)
            .HasPrecision(18, 2)
            .HasDefaultValue(0);

        builder.Property(p => p.GoldPieces)
            .HasPrecision(18, 2)
            .HasDefaultValue(0);

        builder.Property(p => p.PlatinumPieces)
            .HasPrecision(18, 2)
            .HasDefaultValue(0);

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

        // Relación One-to-Many con CraftingQueueItem
        builder.HasMany(p => p.CraftingQueue)
            .WithOne()
            .HasForeignKey(c => c.PartyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(p => p.CraftingQueue)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Relación One-to-Many con StashActivityLog
        builder.HasMany(p => p.ActivityLogs)
            .WithOne()
            .HasForeignKey(a => a.PartyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(p => p.ActivityLogs)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Relación One-to-Many con SessionNote
        builder.HasMany(p => p.SessionNotes)
            .WithOne(n => n.Party)
            .HasForeignKey(n => n.PartyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(p => p.SessionNotes)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Relación One-to-Many con PartyMember (Fase 3.7)
        builder.HasMany(p => p.PartyMembers)
            .WithOne(pm => pm.Party)
            .HasForeignKey(pm => pm.PartyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(p => p.PartyMembers)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
