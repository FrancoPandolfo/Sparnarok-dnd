using Microsoft.EntityFrameworkCore;
using Sparnarok.Api.Domain.Entities;
using Action = Sparnarok.Api.Domain.Entities.Action;

namespace Sparnarok.Api.Infrastructure.Persistence;

/// <summary>
/// Contexto principal de Entity Framework Core para la persistencia relacional en PostgreSQL.
/// </summary>
public class SparnarokDbContext : DbContext
{
    /// <summary>
    /// Conjunto de datos para las criaturas del mundo de D&amp;D 5e.
    /// </summary>
    public DbSet<Creature> Creatures => Set<Creature>();

    /// <summary>
    /// Conjunto de datos para las acciones y ataques tácticos.
    /// </summary>
    public DbSet<Action> Actions => Set<Action>();

    /// <summary>
    /// Conjunto de datos para las propiedades de maestría de armas (Weapon Mastery 2024).
    /// </summary>
    public DbSet<WeaponMastery> WeaponMasteries => Set<WeaponMastery>();

    /// <summary>
    /// Conjunto de datos para las armas canónicas y homebrew ingeridas en el sistema.
    /// </summary>
    public DbSet<Weapon> Weapons => Set<Weapon>();

    /// <summary>
    /// Conjunto de datos para las armaduras y escudos de D&amp;D 5e ingeridos en el sistema.
    /// </summary>
    public DbSet<Armor> Armors => Set<Armor>();

    /// <summary>
    /// Conjunto de datos para los usuarios federados mediante autenticación social (Google OAuth2).
    /// </summary>
    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();

    /// <summary>
    /// Conjunto de datos para los grupos estructurados de héroes (Parties) guardados por los usuarios.
    /// </summary>
    public DbSet<Party> Parties => Set<Party>();

    /// <summary>
    /// Conjunto de datos para los encuentros tácticos de monstruos guardados por los usuarios.
    /// </summary>
    public DbSet<Encounter> Encounters => Set<Encounter>();


    /// <summary>
    /// Inicializa una nueva instancia de <see cref="SparnarokDbContext"/>.
    /// </summary>
    /// <param name="options">Opciones de configuración del contexto.</param>
    public SparnarokDbContext(DbContextOptions<SparnarokDbContext> options)
        : base(options)
    {
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Aplica automáticamente todas las clases de configuración IEntityTypeConfiguration del ensamblado
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SparnarokDbContext).Assembly);
    }
}
