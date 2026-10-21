using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sparnarok.Api.Infrastructure.Persistence;
using Xunit;

namespace Sparnarok.IntegrationTests;

/// <summary>
/// WebApplicationFactory personalizada para pruebas de integración de controladores.
/// Reemplaza la configuración de base de datos por el fixture aislado (PostgreSQL o SQLite fallback).
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public PostgreSqlContainerFixture DbFixture { get; } = new();

    public async Task InitializeAsync()
    {
        await DbFixture.InitializeAsync();
    }

    public new async Task DisposeAsync()
    {
        await DbFixture.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remover todas las configuraciones previas de SparnarokDbContext y Npgsql
            var descriptorsToRemove = services.Where(d =>
                d.ServiceType == typeof(SparnarokDbContext) ||
                d.ServiceType == typeof(DbContextOptions<SparnarokDbContext>) ||
                d.ServiceType == typeof(DbContextOptions) ||
                (d.ServiceType.FullName != null && d.ServiceType.FullName.Contains("SparnarokDbContext")) ||
                (d.ImplementationType != null && d.ImplementationType.FullName != null && d.ImplementationType.FullName.Contains("SparnarokDbContext")) ||
                (d.ServiceType.FullName != null && d.ServiceType.FullName.Contains("Npgsql")) ||
                (d.ImplementationType != null && d.ImplementationType.FullName != null && d.ImplementationType.FullName.Contains("Npgsql"))
            ).ToList();

            foreach (var descriptor in descriptorsToRemove)
            {
                services.Remove(descriptor);
            }

            // Registrar el contexto usando el fixture de pruebas
            services.AddDbContext<SparnarokDbContext>(options =>
            {
                if (DbFixture.IsPostgreSql)
                {
                    options.UseNpgsql(DbFixture.ConnectionString);
                }
                else
                {
                    options.UseSqlite(DbFixture.SqliteConnection!);
                }
            });
        });
    }
}
