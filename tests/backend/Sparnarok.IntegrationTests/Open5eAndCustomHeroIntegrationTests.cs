using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Sparnarok.Api.Application.DTOs;
using Sparnarok.Api.Application.Interfaces;
using Sparnarok.Api.Domain.Enums;
using Sparnarok.Api.Infrastructure.Persistence;
using Sparnarok.Api.Infrastructure.Services;
using Xunit;

namespace Sparnarok.IntegrationTests;

/// <summary>
/// Handler simulado de HTTP en memoria para pruebas de integración con HttpClient y Open5e.
/// </summary>
public class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

    public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
    {
        _responseFactory = responseFactory;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(_responseFactory(request));
    }
}

/// <summary>
/// Pruebas de integración para la sincronización con Open5e y perfiles de combatientes customizados.
/// </summary>
public class Open5eAndCustomHeroIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;

    public Open5eAndCustomHeroIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SparnarokDbContext>();
        TestDatabaseInitializer.EnsureCreated(context);
    }

    [Fact]
    public async Task Open5eBestiaryClient_WithMockedHttpMessageHandler_ParsesMonstersSuccessfully()
    {
        // Arrange
        const string mockJson = """
        {
            "count": 1,
            "next": null,
            "previous": null,
            "results": [
                {
                    "slug": "mock-dragon",
                    "name": "Mock Bronze Dragon",
                    "size": "Huge",
                    "type": "Dragon",
                    "alignment": "lawful good",
                    "armor_class": 19,
                    "hit_points": 212,
                    "speed": { "walk": 40, "fly": 80 },
                    "strength": 23,
                    "dexterity": 10,
                    "constitution": 21,
                    "intelligence": 14,
                    "wisdom": 13,
                    "charisma": 17,
                    "cr": 13.0,
                    "actions": [
                        {
                            "name": "Bite",
                            "desc": "Melee Weapon Attack: +11 to hit, reach 10 ft., one target. Hit: 17 (2d10 + 6) piercing damage.",
                            "attack_bonus": 11,
                            "damage_dice": "2d10+6"
                        }
                    ]
                }
            ]
        }
        """;

        var mockHandler = new MockHttpMessageHandler(req =>
        {
            req.RequestUri!.ToString().Should().Contain("monsters/?limit=10&page=1");
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(mockJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        var httpClient = new HttpClient(mockHandler)
        {
            BaseAddress = new Uri("https://api.open5e.com/v1/")
        };

        var bestiaryClient = new Open5eBestiaryClient(httpClient, NullLogger<Open5eBestiaryClient>.Instance);

        // Act
        var result = await bestiaryClient.GetMonstersAsync(limit: 10, page: 1);

        // Assert
        result.Should().NotBeNull();
        result.Count.Should().Be(1);
        result.Results.Should().HaveCount(1);

        var monster = result.Results[0];
        monster.Name.Should().Be("Mock Bronze Dragon");
        monster.ArmorClass.Should().Be(19);
        monster.HitPoints.Should().Be(212);
        monster.Actions.Should().HaveCount(1);
        monster.Actions![0].DamageDice.Should().Be("2d10+6");
    }

    [Fact]
    public async Task SyncOpen5eMonsters_ViaIngestionEndpoint_PersistsMonstersInDatabase()
    {
        // Cleanup previo para aislamiento
        using (var setupScope = _factory.Services.CreateScope())
        {
            var setupDb = setupScope.ServiceProvider.GetRequiredService<SparnarokDbContext>();
            var existingOgre = await setupDb.Creatures.Include(c => c.Actions).FirstOrDefaultAsync(c => c.Name == "Test Ogre Brawler");
            if (existingOgre != null)
            {
                setupDb.Creatures.Remove(existingOgre);
                await setupDb.SaveChangesAsync();
            }
        }

        // Arrange
        const string mockJson = """
        {
            "count": 2,
            "results": [
                {
                    "slug": "test-ogre",
                    "name": "Test Ogre Brawler",
                    "size": "Large",
                    "type": "Giant",
                    "alignment": "chaotic evil",
                    "armor_class": 12,
                    "hit_points": 59,
                    "speed": { "walk": 40 },
                    "strength": 19,
                    "dexterity": 8,
                    "constitution": 16,
                    "intelligence": 5,
                    "wisdom": 7,
                    "charisma": 7,
                    "cr": 2.0,
                    "actions": [
                        {
                            "name": "Greatclub",
                            "desc": "Hit: 13 (2d8 + 4) bludgeoning damage.",
                            "attack_bonus": 6,
                            "damage_dice": "2d8+4"
                        }
                    ]
                }
            ]
        }
        """;

        var mockHandler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(mockJson, System.Text.Encoding.UTF8, "application/json")
        });

        var customFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IOpen5eBestiaryClient));
                if (descriptor != null) services.Remove(descriptor);

                services.AddScoped<IOpen5eBestiaryClient>(_ =>
                    new Open5eBestiaryClient(new HttpClient(mockHandler) { BaseAddress = new Uri("https://api.open5e.com/v1/") }, NullLogger<Open5eBestiaryClient>.Instance));
            });
        });

        var client = customFactory.CreateClient();

        // Act
        var postResponse = await client.PostAsync("/api/ingestion/open5e/monsters/sync?limit=2", null);

        // Assert
        postResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await postResponse.Content.ReadFromJsonAsync<IngestionResultDto>(_jsonOptions);
        result.Should().NotBeNull();
        result!.TotalProcessed.Should().Be(1);
        result.Inserted.Should().Be(1);

        // Verificar directamente en PostgreSQL / DbContext
        using var scope = customFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SparnarokDbContext>();
        var ogre = await db.Creatures.Include(c => c.Actions).FirstOrDefaultAsync(c => c.Name == "Test Ogre Brawler");
        ogre.Should().NotBeNull();
        ogre!.EffectiveArmorClass.Should().Be(12);
        ogre.HitPoints.Max.Should().Be(59);
        ogre.Size.Should().Be(CreatureSize.Large);
        ogre.Type.Should().Be(CreatureType.Giant);
        ogre.IsPlayerCharacter.Should().BeFalse();
        ogre.Actions.Should().Contain(a => a.Name == "Greatclub" && a.DamageFormula == "2d8+4");

        // Teardown
        db.Creatures.Remove(ogre);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task CreateCustomHero_PersistsPlayerCharacterWithCorrectACAndAttack()
    {
        // Cleanup previo para aislamiento
        using (var setupScope = _factory.Services.CreateScope())
        {
            var heroSetupDb = setupScope.ServiceProvider.GetRequiredService<SparnarokDbContext>();
            var existingHero = await heroSetupDb.Creatures.Include(c => c.Actions).FirstOrDefaultAsync(c => c.Name == "Valeria the Paladin");
            if (existingHero != null)
            {
                heroSetupDb.Creatures.Remove(existingHero);
                await heroSetupDb.SaveChangesAsync();
            }
        }

        // Arrange
        var request = new CreateCustomHeroRequestDto
        {
            Name = "Valeria the Paladin",
            MaxHitPoints = 52,
            ArmorClass = 18,
            Strength = 16,
            Dexterity = 12,
            Constitution = 14,
            Intelligence = 10,
            Wisdom = 12,
            Charisma = 16,
            SpeedFeet = 30,
            AttackName = "Smite Strike",
            DamageFormula = "1d8+3",
            AttackModifierAbility = AbilityScoreType.Strength
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/creatures/custom", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<CreatureResponseDto>(_jsonOptions);
        created.Should().NotBeNull();
        created!.Name.Should().Be("Valeria the Paladin");
        created.EffectiveArmorClass.Should().Be(18);
        created.HitPoints.Max.Should().Be(52);
        created.IsPlayerCharacter.Should().BeTrue();
        created.Actions.Should().Contain(a => a.Name == "Smite Strike" && a.DamageFormula == "1d8+3");

        // Validar directamente en base de datos
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SparnarokDbContext>();
        var dbHero = await db.Creatures.Include(c => c.Actions).FirstOrDefaultAsync(c => c.Id == created.Id);
        dbHero.Should().NotBeNull();
        dbHero!.IsPlayerCharacter.Should().BeTrue();
        dbHero.EffectiveArmorClass.Should().Be(18);

        // Teardown
        db.Creatures.Remove(dbHero);
        await db.SaveChangesAsync();
    }
}
