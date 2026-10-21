using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Sparnarok.Api.Application.DTOs;
using Sparnarok.Api.Domain.Entities;
using Sparnarok.Api.Domain.Enums;
using Sparnarok.Api.Domain.ValueObjects;
using Sparnarok.Api.Infrastructure.Persistence;
using Xunit;

namespace Sparnarok.IntegrationTests;

/// <summary>
/// Pruebas de integración para el endpoint POST /api/simulation/montecarlo.
/// </summary>
public class SimulationControllerIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;
    private readonly JsonSerializerOptions _jsonOptions;

    public SimulationControllerIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        };

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SparnarokDbContext>();
        context.Database.EnsureCreated();
    }

    [Fact]
    public async Task RunMontecarlo_WithValidCreaturesAndWeapons_ReturnsAggregatedMetrics()
    {
        // Arrange: Sembrar criaturas y armas en la base de datos
        var creatureA = new Creature(
            "Test Gladiator A",
            CreatureSize.Medium,
            CreatureType.Humanoid,
            "Neutral",
            challengeRating: 2.0,
            speedFeet: 30,
            abilityScores: new AbilityScores(16, 14, 14, 10, 10, 10),
            hitPoints: new HitPoints(30),
            armorClass: new ArmorClass(15, 0, false));

        var creatureB = new Creature(
            "Test Gladiator B",
            CreatureSize.Medium,
            CreatureType.Humanoid,
            "Neutral",
            challengeRating: 2.0,
            speedFeet: 30,
            abilityScores: new AbilityScores(14, 16, 12, 10, 10, 10),
            hitPoints: new HitPoints(25),
            armorClass: new ArmorClass(14, 0, false));

        var weaponA = new Weapon("Test Greatsword", "2d6", DamageType.Slashing);
        var weaponB = new Weapon("Test Rapier", "1d8", DamageType.Piercing, properties: "Finesse");

        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<SparnarokDbContext>();
            context.Creatures.AddRange(creatureA, creatureB);
            context.Weapons.AddRange(weaponA, weaponB);
            await context.SaveChangesAsync();
        }

        var request = new MontecarloRequestDto(
            CombatantAId: creatureA.Id,
            WeaponAId: weaponA.Id,
            CombatantBId: creatureB.Id,
            WeaponBId: weaponB.Id,
            Iterations: 500);

        // Act
        var response = await _client.PostAsJsonAsync("/api/simulation/montecarlo", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<MontecarloResultDto>(content, _jsonOptions);

        result.Should().NotBeNull();
        result!.TotalIterations.Should().Be(500);
        result.CombatantAName.Should().Be("Test Gladiator A");
        result.CombatantBName.Should().Be("Test Gladiator B");
        result.WeaponAName.Should().Be("Test Greatsword");
        result.WeaponBName.Should().Be("Test Rapier");
        (result.CombatantAWins + result.CombatantBWins + result.Draws).Should().Be(500);
        (result.CombatantAWinRate + result.CombatantBWinRate + result.DrawRate).Should().BeInRange(99.9, 100.1);
        result.AverageRounds.Should().BeGreaterThan(0);
        result.CombatantAAverageDpr.Should().BeGreaterThan(0);
        result.CombatantBAverageDpr.Should().BeGreaterThan(0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    [InlineData(60000)]
    public async Task RunMontecarlo_WithOutOfRangeIterations_ReturnsBadRequest(int iterations)
    {
        var request = new MontecarloRequestDto(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            iterations);

        var response = await _client.PostAsJsonAsync("/api/simulation/montecarlo", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RunMontecarlo_WithNonExistentEntity_ReturnsNotFound()
    {
        var request = new MontecarloRequestDto(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            100);

        var response = await _client.PostAsJsonAsync("/api/simulation/montecarlo", request);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
