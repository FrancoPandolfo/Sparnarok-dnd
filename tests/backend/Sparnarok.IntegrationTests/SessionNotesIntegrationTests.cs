using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Sparnarok.Api.Application.DTOs;
using Sparnarok.Api.Infrastructure.Persistence;
using Xunit;

namespace Sparnarok.IntegrationTests;

/// <summary>
/// Pruebas de integración para las Notas Privadas de Sesión y Onboarding de Party (Fase 3.7)
/// usando PostgreSQL real con Testcontainers.
/// Valida el ciclo CRUD completo y el aislamiento estricto (Logical Row-Level Security):
/// ni el DM ni el Jugador pueden leer o modificar las notas privadas del otro en el mismo grupo.
/// </summary>
public class SessionNotesIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public SessionNotesIntegrationTests(CustomWebApplicationFactory factory)
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

    private async Task<PartyResponseDto> CreatePartyAsync(string partyName, string token, string role = "DungeonMaster", string inGameName = "El Cronista")
    {
        using var postReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/parties");
        postReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        postReq.Content = JsonContent.Create(new CreatePartyRequestDto(partyName, new List<Guid>(), role, inGameName));

        var createResponse = await _client.SendAsync(postReq);
        createResponse.EnsureSuccessStatusCode();

        return (await createResponse.Content.ReadFromJsonAsync<PartyResponseDto>(_jsonOptions))!;
    }

    [Fact]
    public async Task SessionNotes_PrivateNotesAndPartyOnboarding_EnforcesStrictDataIsolationBetweenUsers()
    {
        // 1. Crear DM (User A) y Jugador (User B)
        var dm = await AuthenticateUserAsync("dm_notes");
        var player = await AuthenticateUserAsync("player_notes");

        // 2. DM crea la Party especificando rol y apodo en partida
        var party = await CreatePartyAsync("Campaña de la Mina Perdida", dm.Token, "DungeonMaster", "El Señor Oscuro");
        party.InviteToken.Should().NotBeNullOrWhiteSpace();

        // 3. Jugador se une a la Party mediante el token de invitación (Onboarding Fase 3.7)
        using var joinReq = new HttpRequestMessage(HttpMethod.Post, $"/api/parties/join/{party.InviteToken}");
        joinReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", player.Token);
        joinReq.Content = JsonContent.Create(new JoinPartyRequestDto("Player", "Elendur"));

        var joinResponse = await _client.SendAsync(joinReq);
        var body = await joinResponse.Content.ReadAsStringAsync();
        joinResponse.StatusCode.Should().Be(HttpStatusCode.OK, because: body);
        var joinedParty = await joinResponse.Content.ReadFromJsonAsync<PartyResponseDto>(_jsonOptions);
        joinedParty.Should().NotBeNull();
        joinedParty!.PartyMembers.Should().Contain(m => m.InGameName == "Elendur" && m.Role == "Player");

        // 4. DM crea una nota privada en la Party
        var dmNoteRequest = new CreateSessionNoteRequest(
            "Secretos del DM: Trampa en la tumba",
            "## Trampa mortal\n- Aguja con veneno púrpura CD 15.");

        using var dmCreateNoteReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/parties/{party.Id}/notes");
        dmCreateNoteReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", dm.Token);
        dmCreateNoteReq.Content = JsonContent.Create(dmNoteRequest);

        var dmCreateResponse = await _client.SendAsync(dmCreateNoteReq);
        dmCreateResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var dmNote = await dmCreateResponse.Content.ReadFromJsonAsync<SessionNoteResponseDto>(_jsonOptions);
        dmNote.Should().NotBeNull();
        dmNote!.Title.Should().Be(dmNoteRequest.Title);
        dmNote.OwnerUserId.Should().Be(dm.UserId);

        // 5. Jugador crea su propia nota privada en la misma Party
        var playerNoteRequest = new CreateSessionNoteRequest(
            "Diario de Elendur: Sospechas",
            "## Notas personales\n- Debo vigilar al clérigo.");

        using var playerCreateNoteReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/parties/{party.Id}/notes");
        playerCreateNoteReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", player.Token);
        playerCreateNoteReq.Content = JsonContent.Create(playerNoteRequest);

        var playerCreateResponse = await _client.SendAsync(playerCreateNoteReq);
        playerCreateResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var playerNote = await playerCreateResponse.Content.ReadFromJsonAsync<SessionNoteResponseDto>(_jsonOptions);
        playerNote.Should().NotBeNull();
        playerNote!.Title.Should().Be(playerNoteRequest.Title);
        playerNote.OwnerUserId.Should().Be(player.UserId);

        // 6. AISLAMIENTO GET ALL: DM consulta notas -> sólo ve la nota del DM
        using var dmGetReq = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/parties/{party.Id}/notes");
        dmGetReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", dm.Token);
        var dmGetResponse = await _client.SendAsync(dmGetReq);
        dmGetResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var dmNotesList = await dmGetResponse.Content.ReadFromJsonAsync<List<SessionNoteResponseDto>>(_jsonOptions);
        dmNotesList.Should().NotBeNull();
        dmNotesList!.Should().ContainSingle(n => n.Id == dmNote.Id);
        dmNotesList.Should().NotContain(n => n.Id == playerNote.Id);

        // 7. AISLAMIENTO GET ALL: Jugador consulta notas -> sólo ve la nota del Jugador
        using var playerGetReq = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/parties/{party.Id}/notes");
        playerGetReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", player.Token);
        var playerGetResponse = await _client.SendAsync(playerGetReq);
        playerGetResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var playerNotesList = await playerGetResponse.Content.ReadFromJsonAsync<List<SessionNoteResponseDto>>(_jsonOptions);
        playerNotesList.Should().NotBeNull();
        playerNotesList!.Should().ContainSingle(n => n.Id == playerNote.Id);
        playerNotesList.Should().NotContain(n => n.Id == dmNote.Id);

        // 8. AISLAMIENTO GET BY ID: Jugador intenta leer la nota del DM por ID directo -> 403 Forbidden
        using var playerDirectGetReq = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/parties/{party.Id}/notes/{dmNote.Id}");
        playerDirectGetReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", player.Token);
        var playerDirectGetResponse = await _client.SendAsync(playerDirectGetReq);
        playerDirectGetResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 9. AISLAMIENTO PUT: Jugador intenta modificar la nota del DM -> 403 Forbidden
        using var playerPutReq = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/parties/{party.Id}/notes/{dmNote.Id}");
        playerPutReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", player.Token);
        playerPutReq.Content = JsonContent.Create(new UpdateSessionNoteRequest("Alterado", "Hacked"));
        var playerPutResponse = await _client.SendAsync(playerPutReq);
        playerPutResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 10. AISLAMIENTO DELETE: Jugador intenta borrar la nota del DM -> 403 Forbidden
        using var playerDeleteReq = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/parties/{party.Id}/notes/{dmNote.Id}");
        playerDeleteReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", player.Token);
        var playerDeleteResponse = await _client.SendAsync(playerDeleteReq);
        playerDeleteResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 11. DM elimina exitosamente su propia nota -> 204 NoContent
        using var dmDeleteReq = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/parties/{party.Id}/notes/{dmNote.Id}");
        dmDeleteReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", dm.Token);
        var dmDeleteResponse = await _client.SendAsync(dmDeleteReq);
        dmDeleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task SessionNotes_CreateModifyAndQueryDatabase_PersistsModifiedContentSuccessfully()
    {
        // 1. Crear y autenticar usuario
        var user = await AuthenticateUserAsync("note_persister");

        // 2. Crear Party
        var party = await CreatePartyAsync("Party Crónicas de la Persistencia", user.Token, "DungeonMaster", "El Escriba");

        // 3. Crear una nota privada inicial
        var createRequest = new CreateSessionNoteRequest(
            "Sesión 1: Llegada a Phandalin",
            "Los aventureros llegaron al pueblo y hablaron con Barthen.");

        using var postReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/parties/{party.Id}/notes");
        postReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", user.Token);
        postReq.Content = JsonContent.Create(createRequest);

        var postResponse = await _client.SendAsync(postReq);
        postResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdNote = await postResponse.Content.ReadFromJsonAsync<SessionNoteResponseDto>(_jsonOptions);
        createdNote.Should().NotBeNull();
        createdNote!.Title.Should().Be(createRequest.Title);
        createdNote.Content.Should().Be(createRequest.Content);

        // 4. Modificar el contenido de la nota
        var updateRequest = new UpdateSessionNoteRequest(
            "Sesión 1: Llegada a Phandalin (Actualizado)",
            "Los aventureros descubrieron que los Redbrands custodian la mansión Tresendar. Recompensa: 100 PO.");

        using var putReq = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/parties/{party.Id}/notes/{createdNote.Id}");
        putReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", user.Token);
        putReq.Content = JsonContent.Create(updateRequest);

        var putResponse = await _client.SendAsync(putReq);
        putResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedNote = await putResponse.Content.ReadFromJsonAsync<SessionNoteResponseDto>(_jsonOptions);
        updatedNote.Should().NotBeNull();
        updatedNote!.Title.Should().Be(updateRequest.Title);
        updatedNote.Content.Should().Be(updateRequest.Content);

        // 5. Consultar directamente la base de datos PostgreSQL mediante un nuevo DbContext scope
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<SparnarokDbContext>();
            var noteInDb = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
                dbContext.SessionNotes, n => n.Id == createdNote.Id);

            noteInDb.Should().NotBeNull();
            noteInDb!.PartyId.Should().Be(party.Id);
            noteInDb.OwnerUserId.Should().Be(user.UserId);
            noteInDb.Title.Should().Be("Sesión 1: Llegada a Phandalin (Actualizado)");
            noteInDb.Content.Should().Be("Los aventureros descubrieron que los Redbrands custodian la mansión Tresendar. Recompensa: 100 PO.");
            noteInDb.UpdatedAt.Should().BeOnOrAfter(noteInDb.CreatedAt);
        }

        // 6. Consultar mediante el endpoint GET /notes/{id}
        using var getReq = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/parties/{party.Id}/notes/{createdNote.Id}");
        getReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", user.Token);
        var getResponse = await _client.SendAsync(getReq);
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var retrievedNote = await getResponse.Content.ReadFromJsonAsync<SessionNoteResponseDto>(_jsonOptions);
        retrievedNote.Should().NotBeNull();
        retrievedNote!.Title.Should().Be("Sesión 1: Llegada a Phandalin (Actualizado)");
        retrievedNote.Content.Should().Be("Los aventureros descubrieron que los Redbrands custodian la mansión Tresendar. Recompensa: 100 PO.");
    }
}
