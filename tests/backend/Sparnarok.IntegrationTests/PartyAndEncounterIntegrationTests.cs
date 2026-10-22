using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sparnarok.Api.Application.DTOs;
using Sparnarok.Api.Domain.Enums;
using Sparnarok.Api.Infrastructure.Persistence;
using Xunit;

namespace Sparnarok.IntegrationTests;

/// <summary>
/// Pruebas de integración para Partys y Encuentros tácticos (Fase 3.5),
/// validando CRUD, aislamiento por usuario y restricciones de autorización a nivel de recursos.
/// </summary>
public class PartyAndEncounterIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public PartyAndEncounterIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SparnarokDbContext>();
        TestDatabaseInitializer.EnsureCreated(context);
    }

    private async Task<AuthResponseDto> AuthenticateUserAsync(string prefix)
    {
        var email = $"{prefix}_{Guid.NewGuid():N}@sparnarok.dnd";
        var sub = $"sub_{prefix}_{Guid.NewGuid():N}";
        var token = $"mock-google-token:{email}:{prefix} User:{sub}:https://avatar.url/{prefix}.png";

        var response = await _client.PostAsJsonAsync("/api/auth/google", new GoogleLoginRequestDto(token));
        response.EnsureSuccessStatusCode();

        var auth = await response.Content.ReadFromJsonAsync<AuthResponseDto>(_jsonOptions);
        return auth!;
    }

    private async Task<CreatureResponseDto> CreateCustomHeroAsync(string heroName, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/creatures/custom");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(new CreateCustomHeroRequestDto
        {
            Name = heroName,
            MaxHitPoints = 35,
            ArmorClass = 16,
            Strength = 16,
            Dexterity = 14,
            Constitution = 14,
            Intelligence = 10,
            Wisdom = 12,
            Charisma = 8,
            AttackName = "Espada Larga",
            DamageFormula = "1d8+3"
        });

        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<CreatureResponseDto>(_jsonOptions))!;
    }

    [Fact]
    public async Task Parties_CreateAndGet_PersistsPartyAndIsolatesAcrossUsers()
    {
        // 1. Crear dos usuarios distintos
        var userA = await AuthenticateUserAsync("party_user_a");
        var userB = await AuthenticateUserAsync("party_user_b");

        // 2. Usuario A crea dos héroes
        var hero1 = await CreateCustomHeroAsync("Paladín de la Luz", userA.Token);
        var hero2 = await CreateCustomHeroAsync("Pícaro de las Sombras", userA.Token);

        // 3. Usuario A crea una Party con esos héroes
        var partyName = $"La Compañía del Alba {Guid.NewGuid():N}";
        var createPartyRequest = new CreatePartyRequestDto(partyName, new List<Guid> { hero1.Id, hero2.Id });

        using var postReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/parties");
        postReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userA.Token);
        postReq.Content = JsonContent.Create(createPartyRequest);

        var createResponse = await _client.SendAsync(postReq);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createdParty = await createResponse.Content.ReadFromJsonAsync<PartyResponseDto>(_jsonOptions);
        createdParty.Should().NotBeNull();
        createdParty!.Name.Should().Be(partyName);
        createdParty.OwnerUserId.Should().Be(userA.UserId);
        createdParty.Members.Should().HaveCount(2);

        // 4. Usuario A consulta sus Partys
        using var getReqA = new HttpRequestMessage(HttpMethod.Get, "/api/v1/parties");
        getReqA.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userA.Token);
        var getResponseA = await _client.SendAsync(getReqA);
        getResponseA.StatusCode.Should().Be(HttpStatusCode.OK);

        var listA = await getResponseA.Content.ReadFromJsonAsync<List<PartyResponseDto>>(_jsonOptions);
        listA.Should().NotBeNull();
        listA!.Should().Contain(p => p.Id == createdParty.Id);

        // 5. Usuario B consulta sus Partys (debe estar vacía o no contener la Party de User A)
        using var getReqB = new HttpRequestMessage(HttpMethod.Get, "/api/v1/parties");
        getReqB.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userB.Token);
        var getResponseB = await _client.SendAsync(getReqB);
        getResponseB.StatusCode.Should().Be(HttpStatusCode.OK);

        var listB = await getResponseB.Content.ReadFromJsonAsync<List<PartyResponseDto>>(_jsonOptions);
        listB.Should().NotBeNull();
        listB!.Should().NotContain(p => p.Id == createdParty.Id);

        // 6. Usuario B intenta obtener la Party de User A por ID -> 403 Forbidden
        using var getByIdReqB = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/parties/{createdParty.Id}");
        getByIdReqB.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userB.Token);
        var getByIdResponseB = await _client.SendAsync(getByIdReqB);
        getByIdResponseB.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<CreatureResponseDto> CreateBestiaryMonsterAsync(string name)
    {
        var request = new CreateCreatureRequestDto(
            Name: name,
            Size: CreatureSize.Medium,
            Type: CreatureType.Monstrosity,
            Alignment: "Neutral Evil",
            ChallengeRating: 2.0,
            SpeedFeet: 30,
            Strength: 16,
            Dexterity: 12,
            Constitution: 14,
            Intelligence: 6,
            Wisdom: 10,
            Charisma: 8,
            MaxHitPoints: 45,
            ArmorClassBase: 14,
            ShieldBonus: 0,
            AllowsDexterityModifier: true,
            MaxDexterityModifier: 2);

        var response = await _client.PostAsJsonAsync("/api/v1/creatures", request);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<CreatureResponseDto>(_jsonOptions))!;
    }

    [Fact]
    public async Task Encounters_ResourceAuthorization_PreventsUserFromModifyingOtherUsersEncounter()
    {
        // 1. Crear dos usuarios
        var userA = await AuthenticateUserAsync("enc_user_a");
        var userB = await AuthenticateUserAsync("enc_user_b");

        // 2. Crear una criatura monstruo para el bestiario
        var monster = await CreateBestiaryMonsterAsync($"Bugbear {Guid.NewGuid():N}");

        // 3. Usuario A crea un Encuentro
        var encounterName = $"Emboscada de Prueba {Guid.NewGuid():N}";
        var createEncRequest = new CreateEncounterRequestDto(encounterName, "Media", new List<Guid> { monster.Id });

        using var postEncReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/encounters");
        postEncReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userA.Token);
        postEncReq.Content = JsonContent.Create(createEncRequest);

        var createEncResp = await _client.SendAsync(postEncReq);
        createEncResp.StatusCode.Should().Be(HttpStatusCode.Created);

        var createdEnc = await createEncResp.Content.ReadFromJsonAsync<EncounterResponseDto>(_jsonOptions);
        createdEnc.Should().NotBeNull();
        createdEnc!.Creatures.Should().ContainSingle(c => c.Id == monster.Id);

        // 4. Usuario B intenta agregar un monstruo al encuentro de Usuario A -> 403 Forbidden
        using var addCreatureReqB = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/encounters/{createdEnc.Id}/creatures/{monster.Id}");
        addCreatureReqB.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userB.Token);

        var addCreatureRespB = await _client.SendAsync(addCreatureReqB);
        addCreatureRespB.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 5. Usuario A sí puede eliminar su propio encuentro
        using var deleteReqA = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/encounters/{createdEnc.Id}");
        deleteReqA.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userA.Token);

        var deleteRespA = await _client.SendAsync(deleteReqA);
        deleteRespA.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
