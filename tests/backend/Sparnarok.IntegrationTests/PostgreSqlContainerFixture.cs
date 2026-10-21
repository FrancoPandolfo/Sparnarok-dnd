using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sparnarok.Api.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace Sparnarok.IntegrationTests;

/// <summary>
/// Fixture de integración que administra la base de datos para pruebas.
/// En entornos con Docker (local o CI) utiliza PostgreSQL real (vía Testcontainers o Service Container de CI).
/// En entornos sin Docker local, utiliza SQLite in-memory para validar el mapeo relacional de EF Core.
/// </summary>
public class PostgreSqlContainerFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;
    private SqliteConnection? _sqliteConnection;
    private string? _connectionString;

    /// <summary>Cadena de conexión activa para la base de datos de prueba.</summary>
    public string ConnectionString => _connectionString ?? throw new InvalidOperationException("Fixture no inicializado.");

    /// <summary>Indica si se está ejecutando sobre PostgreSQL real.</summary>
    public bool IsPostgreSql { get; private set; }

    /// <summary>Conexión Sqlite activa si se opera en modo fallback local.</summary>
    public SqliteConnection? SqliteConnection => _sqliteConnection;

    public async Task InitializeAsync()
    {
        // 1. Detectar si CI ya proveyó una conexión PostgreSQL vía variable de entorno
        var ciConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        if (!string.IsNullOrEmpty(ciConnectionString))
        {
            _connectionString = ciConnectionString;
            IsPostgreSql = true;
            return;
        }

        // 2. Si Docker está disponible, iniciar Testcontainers PostgreSQL
        if (IsDockerAvailable())
        {
            try
            {
                _container = new PostgreSqlBuilder("postgres:16-alpine")
                    .WithDatabase("sparnarok_test_db")
                    .WithUsername("sparnarok_test")
                    .WithPassword("sparnarok_secret_test")
                    .Build();

                await _container.StartAsync();
                _connectionString = _container.GetConnectionString();
                IsPostgreSql = true;
                return;
            }
            catch
            {
                // Si Docker falla al iniciar, caer en modo relacional alternativo
            }
        }

        // 3. Fallback para entornos locales sin Docker
        _sqliteConnection = new SqliteConnection("DataSource=:memory:");
        await _sqliteConnection.OpenAsync();
        _connectionString = _sqliteConnection.ConnectionString;
        IsPostgreSql = false;
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync().AsTask();
        }

        if (_sqliteConnection is not null)
        {
            await _sqliteConnection.DisposeAsync();
        }
    }

    /// <summary>
    /// Crea una nueva instancia de <see cref="SparnarokDbContext"/> conectada al proveedor activo.
    /// </summary>
    public SparnarokDbContext CreateDbContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<SparnarokDbContext>();

        if (IsPostgreSql)
        {
            optionsBuilder.UseNpgsql(ConnectionString);
        }
        else
        {
            optionsBuilder.UseSqlite(_sqliteConnection!);
        }

        return new SparnarokDbContext(optionsBuilder.Options);
    }

    private static bool IsDockerAvailable()
    {
        return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOCKER_HOST"));
    }
}
