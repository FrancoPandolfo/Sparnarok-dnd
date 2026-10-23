using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sparnarok.Api.Domain.Entities;

namespace Sparnarok.Api.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuración Fluent API para la entidad intermedia <see cref="PartyMember"/>.
/// </summary>
public class PartyMemberConfiguration : IEntityTypeConfiguration<PartyMember>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PartyMember> builder)
    {
        builder.ToTable("PartyMembers");

        builder.HasKey(pm => pm.Id);

        builder.Property(pm => pm.Id)
            .ValueGeneratedOnAdd();

        builder.Property(pm => pm.PartyId)
            .IsRequired();

        builder.Property(pm => pm.UserId)
            .IsRequired();

        builder.Property(pm => pm.Role)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(pm => pm.InGameName)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(pm => pm.JoinedAt)
            .IsRequired();

        // Índice compuesto único: un usuario sólo puede tener un registro de membresía por Party
        builder.HasIndex(pm => new { pm.PartyId, pm.UserId })
            .IsUnique();

        builder.HasIndex(pm => pm.UserId);

        builder.HasOne(pm => pm.Party)
            .WithMany(p => p.PartyMembers)
            .HasForeignKey(pm => pm.PartyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(pm => pm.User)
            .WithMany()
            .HasForeignKey(pm => pm.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
