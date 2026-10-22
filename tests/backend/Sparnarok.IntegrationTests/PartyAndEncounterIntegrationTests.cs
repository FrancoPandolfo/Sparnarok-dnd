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

    [Fact]
    public async Task ReferenceEquipment_Endpoints_ShouldReturnSearchResultsAndDetail()
    {
        // Act: Búsqueda con query
        var searchResp = await _client.GetAsync("/api/v1/reference/equipment?name=dagger");
        searchResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var items = await searchResp.Content.ReadFromJsonAsync<IReadOnlyList<ReferenceItemSummaryDto>>(_jsonOptions);
        items.Should().NotBeNull();
        items.Should().NotBeEmpty();

        var firstItem = items!.First();
        firstItem.Index.Should().NotBeNullOrWhiteSpace();

        // Act: Detalle por index
        var detailResp = await _client.GetAsync($"/api/v1/reference/equipment/{firstItem.Index}");
        detailResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await detailResp.Content.ReadFromJsonAsync<ReferenceEquipmentDetailDto>(_jsonOptions);
        detail.Should().NotBeNull();
        detail!.Index.Should().Be(firstItem.Index);
        detail.IsOfficialSrd.Should().BeTrue();
    }

    [Fact]
    public async Task PartyStash_Endpoints_ShouldSupportCrud_AndEnforceResourceAuthorization()
    {
        // Arrange: Usuarios A y B
        var userA = await AuthenticateUserAsync("StashOwner");
        var userB = await AuthenticateUserAsync("StashIntruder");

        var hero = await CreateCustomHeroAsync("Guerrero del Stash", userA.Token);

        // Crear Party para Usuario A
        var createPartyReq = new CreatePartyRequestDto("Party con Alijo", new List<Guid> { hero.Id });
        using var postPartyReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/parties");
        postPartyReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userA.Token);
        postPartyReq.Content = JsonContent.Create(createPartyReq);

        var partyResp = await _client.SendAsync(postPartyReq);
        partyResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var party = await partyResp.Content.ReadFromJsonAsync<PartyResponseDto>(_jsonOptions);

        // 1. Usuario A agrega un ítem al Stash
        var createItemReq = new CreateStashItemRequest(
            Name: "Poción de Invisibilidad",
            Quantity: 2,
            Weight: 0.5m,
            Description: "Otorga invisibilidad durante 1 hora.",
            Rarity: "Very Rare",
            ItemType: "Potion",
            IsOfficialSrd: true,
            ReferenceIndex: "potion-of-invisibility"
        );

        using var postItemReqA = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/parties/{party!.Id}/stash");
        postItemReqA.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userA.Token);
        postItemReqA.Content = JsonContent.Create(createItemReq);

        var postItemRespA = await _client.SendAsync(postItemReqA);
        postItemRespA.StatusCode.Should().Be(HttpStatusCode.Created);

        var createdItem = await postItemRespA.Content.ReadFromJsonAsync<StashItemResponseDto>(_jsonOptions);
        createdItem.Should().NotBeNull();
        createdItem!.Name.Should().Be("Poción de Invisibilidad");
        createdItem.Quantity.Should().Be(2);
        createdItem.Weight.Should().Be(0.5m);

        // 2. Consultar Stash de Usuario A
        using var getStashReqA = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/parties/{party.Id}/stash");
        getStashReqA.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userA.Token);

        var getStashRespA = await _client.SendAsync(getStashReqA);
        getStashRespA.StatusCode.Should().Be(HttpStatusCode.OK);

        var stashA = await getStashRespA.Content.ReadFromJsonAsync<PartyStashResponseDto>(_jsonOptions);
        stashA.Should().NotBeNull();
        stashA!.TotalWeight.Should().Be(1m); // 2 * 0.5m
        stashA.Items.Should().ContainSingle(i => i.Id == createdItem.Id);

        // 3. Usuario B intenta agregar un ítem al Stash de Usuario A -> 403 Forbidden
        using var postItemReqB = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/parties/{party.Id}/stash");
        postItemReqB.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userB.Token);
        postItemReqB.Content = JsonContent.Create(createItemReq);

        var postItemRespB = await _client.SendAsync(postItemReqB);
        postItemRespB.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 4. Usuario B intenta borrar un ítem del Stash de Usuario A -> 403 Forbidden
        using var deleteItemReqB = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/parties/{party.Id}/stash/{createdItem.Id}");
        deleteItemReqB.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userB.Token);

        var deleteItemRespB = await _client.SendAsync(deleteItemReqB);
        deleteItemRespB.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 5. Usuario A borra el ítem con éxito -> 204 NoContent
        using var deleteItemReqA = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/parties/{party.Id}/stash/{createdItem.Id}");
        deleteItemReqA.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userA.Token);

        var deleteItemRespA = await _client.SendAsync(deleteItemReqA);
        deleteItemRespA.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task PartyFunds_CraftingQueue_AdvanceDowntimeDay_And_ActivityLogs_FullFlow_Succeeds()
    {
        // Arrange
        var user = await AuthenticateUserAsync("crafting_hero");

        // Crear Party
        var partyCreateReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/parties");
        partyCreateReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", user.Token);
        partyCreateReq.Content = JsonContent.Create(new CreatePartyRequestDto("Forjadores de Runas", new List<Guid>()));

        var partyResp = await _client.SendAsync(partyCreateReq);
        partyResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var party = await partyResp.Content.ReadFromJsonAsync<PartyResponseDto>(_jsonOptions);

        // 1. Actualizar Fondos de la Party (100 GP)
        var updateFundsReq = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/parties/{party!.Id}/funds");
        updateFundsReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", user.Token);
        updateFundsReq.Content = JsonContent.Create(new UpdatePartyFundsRequest(0, 0, 0, 100, 0));

        var updateFundsResp = await _client.SendAsync(updateFundsReq);
        updateFundsResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var fundsDto = await updateFundsResp.Content.ReadFromJsonAsync<PartyFundDto>(_jsonOptions);
        fundsDto!.GoldPieces.Should().Be(100);

        // 2. Encolar un objeto de fabricación (Coste: 30 GP, 1 día)
        var queueReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/parties/{party.Id}/crafting");
        queueReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", user.Token);
        queueReq.Content = JsonContent.Create(new QueueCraftingItemRequest(
            ItemName: "Poción de Curación Mayor",
            ItemType: "Potion",
            Rarity: "Uncommon",
            GoldCost: 30,
            DaysRequired: 1,
            Weight: 0.5m,
            Description: "Restaura 4d4+4 PG.",
            IsOfficialSrd: true,
            ReferenceIndex: "potion-of-greater-healing",
            CrafterName: "Alquimista"
        ));

        var queueResp = await _client.SendAsync(queueReq);
        queueResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var queuedItem = await queueResp.Content.ReadFromJsonAsync<CraftingQueueItemDto>(_jsonOptions);
        queuedItem!.ItemName.Should().Be("Poción de Curación Mayor");
        queuedItem.DaysRemaining.Should().Be(1);
        queuedItem.Status.Should().Be("InProgress");

        // Verificar que el oro fue descontado (100 - 30 = 70 GP)
        var getFundsReq = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/parties/{party.Id}/funds");
        getFundsReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", user.Token);
        var getFundsResp = await _client.SendAsync(getFundsReq);
        var remainingFunds = await getFundsResp.Content.ReadFromJsonAsync<PartyFundDto>(_jsonOptions);
        remainingFunds!.GoldPieces.Should().Be(70);

        // 3. Avanzar 1 día de Downtime (Acción del DM)
        var advanceReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/parties/{party.Id}/crafting/advance-day");
        advanceReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", user.Token);

        var advanceResp = await _client.SendAsync(advanceReq);
        advanceResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var advanceResult = await advanceResp.Content.ReadFromJsonAsync<DowntimeAdvanceResultDto>(_jsonOptions);
        advanceResult!.DaysAdvanced.Should().Be(1);
        advanceResult.CompletedItemsCount.Should().Be(1);
        advanceResult.CompletedItemNames.Should().Contain("Poción de Curación Mayor");

        // 4. Verificar que el objeto pasó automáticamente al alijo principal (Stash)
        var getStashReq = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/parties/{party.Id}/stash");
        getStashReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", user.Token);
        var getStashResp = await _client.SendAsync(getStashReq);
        var stash = await getStashResp.Content.ReadFromJsonAsync<PartyStashResponseDto>(_jsonOptions);
        stash!.Items.Should().Contain(i => i.Name == "Poción de Curación Mayor");

        // 5. Verificar historial de auditoría (Activity Logs)
        var getLogsReq = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/parties/{party.Id}/stash/logs");
        getLogsReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", user.Token);
        var getLogsResp = await _client.SendAsync(getLogsReq);
        getLogsResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var logs = await getLogsResp.Content.ReadFromJsonAsync<IReadOnlyList<StashActivityLogDto>>(_jsonOptions);
        logs.Should().NotBeNull();
        logs!.Should().Contain(l => l.ActionType == "FundsUpdated");
        logs.Should().Contain(l => l.ActionType == "CraftingStarted");
        logs.Should().Contain(l => l.ActionType == "CraftingCompleted");
        logs.Should().Contain(l => l.ActionType == "DowntimeAdvanced");
    }
}
