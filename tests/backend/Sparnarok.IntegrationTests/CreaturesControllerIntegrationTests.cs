using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Sparnarok.Api.Application.DTOs;
using Sparnarok.Api.Domain.Enums;
using Sparnarok.Api.Infrastructure.Persistence;
using Xunit;

namespace Sparnarok.IntegrationTests;

public class CreaturesControllerIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;

    public CreaturesControllerIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        // Ensure database is initialized with seed data
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SparnarokDbContext>();
        context.Database.EnsureCreated();
    }

    [Fact]
    public async Task GetAllMasteries_ReturnsEightSeededMasteries()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/weaponmasteries");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var masteries = await response.Content.ReadFromJsonAsync<List<WeaponMasteryResponseDto>>(_jsonOptions);
        masteries.Should().NotBeNull();
        masteries.Should().HaveCount(8);
        masteries!.Select(m => m.Name).Should().Contain(["Cleave", "Graze", "Nick", "Push", "Sap", "Slow", "Topple", "Vex"]);
    }

    [Fact]
    public async Task CreateCreature_WithValidPayload_Returns201CreatedAndPersists()
    {
        // Arrange
        var request = new CreateCreatureRequestDto(
            Name: "Bugbear Stalker",
            Size: CreatureSize.Medium,
            Type: CreatureType.Humanoid,
            Alignment: "Chaotic Evil",
            ChallengeRating: 1,
            SpeedFeet: 30,
            Strength: 15,
            Dexterity: 14,
            Constitution: 13,
            Intelligence: 8,
            Wisdom: 11,
            Charisma: 9,
            MaxHitPoints: 27,
            ArmorClassBase: 14,
            ShieldBonus: 2);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/creatures", request, _jsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var created = await response.Content.ReadFromJsonAsync<CreatureResponseDto>(_jsonOptions);
        created.Should().NotBeNull();
        created!.Name.Should().Be("Bugbear Stalker");
        created.ChallengeRating.Should().Be(1);
        created.ProficiencyBonus.Should().Be(2);
        created.AbilityScores.Strength.Should().Be(15);
        created.AbilityScores.StrengthModifier.Should().Be(2); // floor((15-10)/2) = 2
        created.HitPoints.Max.Should().Be(27);
        created.ArmorClass.ShieldBonus.Should().Be(2);

        // Verify retrieval via Location URL
        var getResponse = await _client.GetAsync(response.Headers.Location);
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateCreature_WithInvalidPayload_Returns400BadRequestWithValidationDetails()
    {
        // Arrange: Missing Name and negative MaxHitPoints
        var invalidRequest = new CreateCreatureRequestDto(
            Name: "",
            Size: CreatureSize.Medium,
            Type: CreatureType.Humanoid,
            Alignment: "Neutral",
            ChallengeRating: 50, // Out of range
            MaxHitPoints: -5);   // Out of range

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/creatures", invalidRequest, _jsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Name");
        content.Should().Contain("ChallengeRating");
        content.Should().Contain("MaxHitPoints");
    }

    [Fact]
    public async Task CompleteTacticalFlow_CreateAddActionLearnMasteryAndDelete()
    {
        // 1. Create Creature
        var createRequest = new CreateCreatureRequestDto(
            Name: "Drow Duelist",
            Size: CreatureSize.Medium,
            Type: CreatureType.Humanoid,
            Alignment: "Neutral Evil",
            ChallengeRating: 2,
            SpeedFeet: 30,
            Strength: 10,
            Dexterity: 16,
            Constitution: 12,
            Intelligence: 11,
            Wisdom: 13,
            Charisma: 12,
            MaxHitPoints: 33,
            ArmorClassBase: 12);

        var createRes = await _client.PostAsJsonAsync("/api/v1/creatures", createRequest, _jsonOptions);
        createRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var creature = await createRes.Content.ReadFromJsonAsync<CreatureResponseDto>(_jsonOptions);

        // 2. Query available masteries to get 'Vex'
        var masteriesRes = await _client.GetAsync("/api/v1/weaponmasteries");
        var masteries = await masteriesRes.Content.ReadFromJsonAsync<List<WeaponMasteryResponseDto>>(_jsonOptions);
        var vexMastery = masteries!.First(m => m.Property == MasteryProperty.Vex);

        // 3. Add an Action with Vex mastery
        var actionRequest = new CreateActionRequestDto(
            Name: "Rapier Thrust",
            Type: ActionType.Action,
            Description: "A precise thrust aiming for vital points.",
            IsAttack: true,
            ReachOrRangeFeet: 5,
            DamageFormula: "1d8",
            DamageType: DamageType.Piercing,
            AttackModifierAbility: AbilityScoreType.Dexterity,
            AssociatedMasteryId: vexMastery.Id);

        var actionRes = await _client.PostAsJsonAsync($"/api/v1/creatures/{creature!.Id}/actions", actionRequest, _jsonOptions);
        actionRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdAction = await actionRes.Content.ReadFromJsonAsync<ActionResponseDto>(_jsonOptions);
        createdAction!.Name.Should().Be("Rapier Thrust");
        createdAction.AssociatedMasteryId.Should().Be(vexMastery.Id);

        // 4. Teach Weapon Mastery to Creature
        var learnRes = await _client.PostAsync($"/api/v1/creatures/{creature.Id}/masteries/{vexMastery.Id}", null);
        learnRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // 5. Query Creature to verify graph is fully populated
        var getRes = await _client.GetAsync($"/api/v1/creatures/{creature.Id}");
        getRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var fullCreature = await getRes.Content.ReadFromJsonAsync<CreatureResponseDto>(_jsonOptions);
        fullCreature!.Actions.Should().HaveCount(1);
        fullCreature.KnownMasteries.Should().HaveCount(1);
        fullCreature.KnownMasteries[0].Property.Should().Be(MasteryProperty.Vex);

        // 6. Delete Creature
        var deleteRes = await _client.DeleteAsync($"/api/v1/creatures/{creature.Id}");
        deleteRes.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 7. Verify 404 on subsequent get
        var notFoundRes = await _client.GetAsync($"/api/v1/creatures/{creature.Id}");
        notFoundRes.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
