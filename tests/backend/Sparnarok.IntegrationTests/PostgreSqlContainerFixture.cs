using Testcontainers.PostgreSql;
using Xunit;

namespace Sparnarok.IntegrationTests;

/// <summary>
/// Fixture reutilizable para pruebas de integración con PostgreSQL real mediante Testcontainers.
/// </summary>
public class PostgreSqlContainerFixture : IAsyncLifetime
{
    public PostgreSqlContainer Container { get; } = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("sparnarok_test_db")
        .WithUsername("sparnarok_test")
        .WithPassword("sparnarok_secret_test")
        .Build();

    public string ConnectionString => Container.GetConnectionString();

    public async Task InitializeAsync()
    {
        // Solo inicia el contenedor si el daemon de Docker se encuentra disponible en el entorno
        if (IsDockerAvailable())
        {
            await Container.StartAsync();
        }
    }

    public async Task DisposeAsync()
    {
        if (IsDockerAvailable())
        {
            await Container.DisposeAsync().AsTask();
        }
    }

    private static bool IsDockerAvailable()
    {
        // Detecta si Docker daemon está respondiendo para evitar fallas en ambientes de desarrollo sin Docker local
        return Environment.GetEnvironmentVariable("CI") == "true" ||
               !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOCKER_HOST"));
    }
}
