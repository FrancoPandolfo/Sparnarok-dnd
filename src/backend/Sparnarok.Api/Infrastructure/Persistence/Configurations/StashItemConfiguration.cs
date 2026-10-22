using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sparnarok.Api.Domain.Entities;

namespace Sparnarok.Api.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuración Fluent API para la entidad <see cref="StashItem"/> (Collaborative Party Stash).
/// </summary>
public class StashItemConfiguration : IEntityTypeConfiguration<StashItem>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<StashItem> builder)
    {
        builder.ToTable("StashItems");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.PartyId)
            .IsRequired();

        builder.HasIndex(s => s.PartyId);

        builder.Property(s => s.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(s => s.Quantity)
            .HasDefaultValue(1)
            .IsRequired();

        builder.Property(s => s.Weight)
            .HasPrecision(8, 2)
            .HasDefaultValue(0m)
            .IsRequired();

        builder.Property(s => s.Description)
            .HasMaxLength(4000)
            .IsRequired();

        builder.Property(s => s.Rarity)
            .HasMaxLength(50)
            .HasDefaultValue("Common")
            .IsRequired();

        builder.Property(s => s.ItemType)
            .HasMaxLength(100)
            .HasDefaultValue("Adventuring Gear")
            .IsRequired();

        builder.Property(s => s.IsOfficialSrd)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(s => s.ReferenceIndex)
            .HasMaxLength(150);

        builder.Property(s => s.AssignedCreatureId);

        builder.Property(s => s.CreatedAt)
            .IsRequired();
    }
}
