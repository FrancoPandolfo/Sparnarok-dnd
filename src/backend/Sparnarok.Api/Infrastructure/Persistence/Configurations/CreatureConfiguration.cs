using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sparnarok.Api.Domain.Entities;

namespace Sparnarok.Api.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuración Fluent API para el agregado raíz <see cref="Creature"/>.
/// </summary>
public class CreatureConfiguration : IEntityTypeConfiguration<Creature>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Creature> builder)
    {
        builder.ToTable("Creatures");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(c => c.Size)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(c => c.Type)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(c => c.Alignment)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(c => c.ChallengeRating)
            .IsRequired();

        builder.Property(c => c.SpeedFeet)
            .IsRequired();

        // 1. Mapeo de Objeto de Valor: AbilityScores (OwnsOne)
        builder.OwnsOne(c => c.AbilityScores, ab =>
        {
            ab.Property(a => a.Strength).HasColumnName("Strength").IsRequired();
            ab.Property(a => a.Dexterity).HasColumnName("Dexterity").IsRequired();
            ab.Property(a => a.Constitution).HasColumnName("Constitution").IsRequired();
            ab.Property(a => a.Intelligence).HasColumnName("Intelligence").IsRequired();
            ab.Property(a => a.Wisdom).HasColumnName("Wisdom").IsRequired();
            ab.Property(a => a.Charisma).HasColumnName("Charisma").IsRequired();
        });

        // 2. Mapeo de Objeto de Valor: HitPoints (OwnsOne)
        builder.OwnsOne(c => c.HitPoints, hp =>
        {
            hp.Property(h => h.Max).HasColumnName("MaxHitPoints").IsRequired();
            hp.Property(h => h.Current).HasColumnName("CurrentHitPoints").IsRequired();
            hp.Property(h => h.Temporary).HasColumnName("TemporaryHitPoints").IsRequired();
        });

        // 3. Mapeo de Objeto de Valor: ArmorClass (OwnsOne)
        builder.OwnsOne(c => c.ArmorClass, ac =>
        {
            ac.Property(a => a.Base).HasColumnName("ArmorClassBase").IsRequired();
            ac.Property(a => a.ShieldBonus).HasColumnName("ArmorClassShieldBonus").IsRequired();
            ac.Property(a => a.AllowsDexterityModifier).HasColumnName("ArmorClassAllowsDex").IsRequired();
            ac.Property(a => a.MaxDexterityModifier).HasColumnName("ArmorClassMaxDex").IsRequired(false);
        });

        // 4. Relación 1:N con Action (Borrado en Cascada)
        builder.HasMany(c => c.Actions)
            .WithOne()
            .HasForeignKey(a => a.CreatureId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(c => c.Actions)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // 5. Relación N:M con WeaponMastery mediante tabla intermedia
        builder.HasMany(c => c.KnownMasteries)
            .WithMany()
            .UsingEntity<Dictionary<string, object>>(
                "CreatureWeaponMasteries",
                j => j.HasOne<WeaponMastery>().WithMany().HasForeignKey("WeaponMasteryId").OnDelete(DeleteBehavior.Cascade),
                j => j.HasOne<Creature>().WithMany().HasForeignKey("CreatureId").OnDelete(DeleteBehavior.Cascade)
            );

        builder.Navigation(c => c.KnownMasteries)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // 6. Relaciones opcionales para Armadura y Escudo equipados (SetNull al eliminar armadura)
        builder.HasOne(c => c.EquippedArmor)
            .WithMany()
            .HasForeignKey(c => c.EquippedArmorId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(c => c.EquippedShield)
            .WithMany()
            .HasForeignKey(c => c.EquippedShieldId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
