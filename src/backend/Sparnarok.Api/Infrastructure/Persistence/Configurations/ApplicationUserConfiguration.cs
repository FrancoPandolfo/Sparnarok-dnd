using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sparnarok.Api.Domain.Entities;

namespace Sparnarok.Api.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuración relacional Fluent API para la entidad <see cref="ApplicationUser"/>.
/// </summary>
public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.GoogleSubjectId)
            .HasMaxLength(150)
            .IsRequired(false);

        builder.HasIndex(u => u.GoogleSubjectId)
            .IsUnique()
            .HasFilter("\"GoogleSubjectId\" IS NOT NULL");

        builder.Property(u => u.Email)
            .HasMaxLength(256)
            .IsRequired();

        builder.HasIndex(u => u.Email)
            .IsUnique();

        builder.Property(u => u.DisplayName)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(u => u.GlobalNickname)
            .HasMaxLength(100)
            .IsRequired(false);

        builder.Property(u => u.PasswordHash)
            .HasMaxLength(500)
            .IsRequired(false);

        builder.Property(u => u.AvatarUrl)
            .HasMaxLength(1000)
            .IsRequired(false);

        builder.Property(u => u.CreatedAtUtc)
            .IsRequired();

        builder.Property(u => u.LastLoginAtUtc)
            .IsRequired();

        builder.HasMany(u => u.CustomHeroes)
            .WithOne()
            .HasForeignKey(c => c.OwnerUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
