using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sparnarok.Api.Domain.Entities;

namespace Sparnarok.Api.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuración Fluent API para la entidad <see cref="StashActivityLog"/>.
/// </summary>
public class StashActivityLogConfiguration : IEntityTypeConfiguration<StashActivityLog>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<StashActivityLog> builder)
    {
        builder.ToTable("StashActivityLogs");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.PartyId)
            .IsRequired();

        builder.HasIndex(a => a.PartyId);
        builder.HasIndex(a => new { a.PartyId, a.TimestampUtc });

        builder.Property(a => a.UserId);

        builder.Property(a => a.UserDisplayName)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(a => a.ActionType)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(a => a.Description)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(a => a.TimestampUtc)
            .IsRequired();
    }
}
