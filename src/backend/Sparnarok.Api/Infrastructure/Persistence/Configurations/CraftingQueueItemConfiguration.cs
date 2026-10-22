using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sparnarok.Api.Domain.Entities;

namespace Sparnarok.Api.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuración Fluent API para la entidad <see cref="CraftingQueueItem"/>.
/// </summary>
public class CraftingQueueItemConfiguration : IEntityTypeConfiguration<CraftingQueueItem>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CraftingQueueItem> builder)
    {
        builder.ToTable("CraftingQueueItems");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.PartyId)
            .IsRequired();

        builder.HasIndex(c => c.PartyId);
        builder.HasIndex(c => new { c.PartyId, c.Status });

        builder.Property(c => c.ItemName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(c => c.ItemType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(c => c.Rarity)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(c => c.GoldCost)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(c => c.DaysRequired)
            .IsRequired();

        builder.Property(c => c.DaysRemaining)
            .IsRequired();

        builder.Property(c => c.Weight)
            .HasPrecision(10, 2)
            .IsRequired();

        builder.Property(c => c.Description)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(c => c.IsOfficialSrd)
            .IsRequired();

        builder.Property(c => c.ReferenceIndex)
            .HasMaxLength(100);

        builder.Property(c => c.CrafterName)
            .HasMaxLength(150);

        builder.Property(c => c.Status)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(c => c.CreatedAtUtc)
            .IsRequired();
    }
}
