using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sparnarok.Api.Application.DTOs;
using Sparnarok.Api.Domain.Entities;
using Sparnarok.Api.Domain.Enums;
using Sparnarok.Api.Infrastructure.Persistence;
using Xunit;

namespace Sparnarok.IntegrationTests;

/// <summary>
/// Pruebas de integración relacional para el pipeline de ingesta masiva (CSV Bulk Ingestion & Upsert)
/// persistiendo en la base de datos administrada por <see cref="CustomWebApplicationFactory"/>.
/// </summary>
public class WeaponIngestionIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;

    public WeaponIngestionIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        };

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SparnarokDbContext>();
        TestDatabaseInitializer.EnsureCreated(context);
    }

    [Fact]
    public async Task IngestWeapons_WithValidCsv_PersistsEntitiesInDatabase()
    {
        // Arrange: Asegurar aislamiento de armas previas
        using (var setupScope = _factory.Services.CreateScope())
        {
            var db = setupScope.ServiceProvider.GetRequiredService<SparnarokDbContext>();
            var toRemove = await db.Weapons.Where(w => w.Name == "Rapier" || w.Name == "Shortsword" || w.Name == "Heavy Crossbow").ToListAsync();
            if (toRemove.Count > 0)
            {
                db.Weapons.RemoveRange(toRemove);
                await db.SaveChangesAsync();
            }
        }

        const string csvData = """
            Name,Damage,DamageType,Properties,Weight,Cost
            Rapier,1d8,Piercing,Finesse,2.0,25 gp
            Shortsword,1d6,Piercing,"Finesse, Light",2.0,10 gp
            Heavy Crossbow,1d10,Piercing,"Heavy, Loading, Two-Handed",18.0,50 gp
            """;

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(csvData));
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("text/csv");
        content.Add(fileContent, "file", "weapons_initial.csv");

        // Act
        var response = await _client.PostAsync("/api/ingestion/weapons", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<IngestionResultDto>(_jsonOptions);
        result.Should().NotBeNull();
        result!.TotalProcessed.Should().Be(3);
        result.Inserted.Should().Be(3);
        result.Updated.Should().Be(0);
        result.Errors.Should().BeEmpty();

        // Verificar directamente en la base de datos relacional
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SparnarokDbContext>();

        var rapier = await dbContext.Weapons.FirstOrDefaultAsync(w => w.Name == "Rapier");
        rapier.Should().NotBeNull();
        rapier!.Damage.Should().Be("1d8");
        rapier.DamageType.Should().Be(DamageType.Piercing);
        rapier.Properties.Should().Be("Finesse");
        rapier.Weight.Should().Be(2.0);
        rapier.Cost.Should().Be("25 gp");

        var crossbow = await dbContext.Weapons.FirstOrDefaultAsync(w => w.Name == "Heavy Crossbow");
        crossbow.Should().NotBeNull();
        crossbow!.Weight.Should().Be(18.0);
    }

    [Fact]
    public async Task IngestWeapons_WithExistingWeapons_PerformsIdempotentUpsert()
    {
        // 0. Limpieza previa de Longsword y Glaive para aislamiento determinista
        using (var setupScope = _factory.Services.CreateScope())
        {
            var db = setupScope.ServiceProvider.GetRequiredService<SparnarokDbContext>();
            var toRemove = await db.Weapons.Where(w => w.Name == "Longsword" || w.Name == "Glaive").ToListAsync();
            if (toRemove.Count > 0)
            {
                db.Weapons.RemoveRange(toRemove);
                await db.SaveChangesAsync();
            }
        }

        // 1. Arrange: Subir Longsword inicial
        const string initialCsv = """
            Name,Damage,DamageType,Properties,Weight,Cost
            Longsword,1d8,Slashing,Versatile (1d10),3.0,15 gp
            """;

        using var initialContent = new MultipartFormDataContent();
        var initialFile = new ByteArrayContent(Encoding.UTF8.GetBytes(initialCsv));
        initialFile.Headers.ContentType = MediaTypeHeaderValue.Parse("text/csv");
        initialContent.Add(initialFile, "file", "weapons_v1.csv");

        var initialResponse = await _client.PostAsync("/api/ingestion/weapons", initialContent);
        initialResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 2. Act: Subir CSV que actualiza Longsword e inserta Glaive
        const string upsertCsv = """
            Name,Damage,DamageType,Properties,Weight,Cost
            Longsword,1d10,Slashing,Versatile (1d12),3.2,20 gp
            Glaive,1d10,Slashing,"Heavy, Reach, Two-Handed",6.0,20 gp
            """;

        using var upsertContent = new MultipartFormDataContent();
        var upsertFile = new ByteArrayContent(Encoding.UTF8.GetBytes(upsertCsv));
        upsertFile.Headers.ContentType = MediaTypeHeaderValue.Parse("text/csv");
        upsertContent.Add(upsertFile, "file", "weapons_v2.csv");

        var upsertResponse = await _client.PostAsync("/api/ingestion/weapons", upsertContent);

        // Assert
        upsertResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await upsertResponse.Content.ReadFromJsonAsync<IngestionResultDto>(_jsonOptions);
        result.Should().NotBeNull();
        result!.TotalProcessed.Should().Be(2);
        result.Inserted.Should().Be(1, "Glaive es nueva");
        result.Updated.Should().Be(1, "Longsword ya existía y debe actualizarse");
        result.Errors.Should().BeEmpty();

        // Verificar que Longsword no se duplicó y sus datos se actualizaron
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SparnarokDbContext>();

        var longswords = await dbContext.Weapons.Where(w => w.Name == "Longsword").ToListAsync();
        longswords.Should().HaveCount(1, "El nombre debe ser único e idempotente");
        longswords[0].Damage.Should().Be("1d10");
        longswords[0].Properties.Should().Be("Versatile (1d12)");
        longswords[0].Weight.Should().Be(3.2);
        longswords[0].Cost.Should().Be("20 gp");
    }

    [Fact]
    public async Task IngestWeapons_WithEmptyOrNonCsvFile_Returns400BadRequest()
    {
        // Arrange: Archivo txt en lugar de csv
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("Invalid payload"));
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("text/plain");
        content.Add(fileContent, "file", "not_a_csv.txt");

        // Act
        var response = await _client.PostAsync("/api/ingestion/weapons", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
