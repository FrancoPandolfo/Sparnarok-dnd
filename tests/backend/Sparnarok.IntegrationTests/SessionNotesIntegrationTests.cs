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
/// Pruebas de integración para las Notas de Sesión (SessionNotesController) usando PostgreSQL real con Testcontainers.
/// Valida el ciclo CRUD completo y el aislamiento de seguridad: sólo el DM (creador de la Party)
/// puede acceder o modificar notas de sesión.
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

    private async Task<PartyResponseDto> CreatePartyAsync(string partyName, string token)
    {
        using var postReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/parties");
        postReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        postReq.Content = JsonContent.Create(new CreatePartyRequestDto(partyName, new List<Guid>()));

        var createResponse = await _client.SendAsync(postReq);
        createResponse.EnsureSuccessStatusCode();

        return (await createResponse.Content.ReadFromJsonAsync<PartyResponseDto>(_jsonOptions))!;
    }

    [Fact]
    public async Task SessionNotes_CrudAndDMRoleEnforcement_PersistsAndProtectsAgainstPlayers()
    {
        // 1. Crear DM (User A) y Jugador (User B)
        var dm = await AuthenticateUserAsync("dm_notes");
        var player = await AuthenticateUserAsync("player_notes");

        // 2. DM crea una Party
        var party = await CreatePartyAsync("Campaña de la Mina Perdida", dm.Token);

        // 3. DM crea una nota de sesión
        var createNoteRequest = new CreateSessionNoteRequest(
            "Sesión 1: Emboscada Goblin",
            "## Pistas\n- Se encontraron huellas que llevan a la guarida.\n- Sildar Hallwinter está cautivo.");

        using var createNoteReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/parties/{party.Id}/notes");
        createNoteReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", dm.Token);
        createNoteReq.Content = JsonContent.Create(createNoteRequest);

        var createResponse = await _client.SendAsync(createNoteReq);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createdNote = await createResponse.Content.ReadFromJsonAsync<SessionNoteResponseDto>(_jsonOptions);
        createdNote.Should().NotBeNull();
        createdNote!.Title.Should().Be(createNoteRequest.Title);
        createdNote.Content.Should().Be(createNoteRequest.Content);
        createdNote.PartyId.Should().Be(party.Id);

        // 4. Jugador intenta leer las notas de la Party -> 403 Forbidden (Anti-metagaming / DM-only)
        using var playerGetReq = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/parties/{party.Id}/notes");
        playerGetReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", player.Token);
        var playerGetResponse = await _client.SendAsync(playerGetReq);
        playerGetResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 5. Jugador intenta crear una nota -> 403 Forbidden
        using var playerCreateReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/parties/{party.Id}/notes");
        playerCreateReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", player.Token);
        playerCreateReq.Content = JsonContent.Create(new CreateSessionNoteRequest("Intento hacker", "No soy DM"));
        var playerCreateResponse = await _client.SendAsync(playerCreateReq);
        playerCreateResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 6. DM consulta las notas -> 200 OK
        using var dmGetReq = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/parties/{party.Id}/notes");
        dmGetReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", dm.Token);
        var dmGetResponse = await _client.SendAsync(dmGetReq);
        dmGetResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var notesList = await dmGetResponse.Content.ReadFromJsonAsync<List<SessionNoteResponseDto>>(_jsonOptions);
        notesList.Should().NotBeNull();
        notesList.Should().ContainSingle(n => n.Id == createdNote.Id);

        // 7. DM actualiza la nota
        var updateNoteRequest = new UpdateSessionNoteRequest(
            "Sesión 1: Emboscada Goblin (Finalizada)",
            "## Pistas\n- Sildar rescatado. El mapa lleva al Castillo Cragmaw.");

        using var dmUpdateReq = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/parties/{party.Id}/notes/{createdNote.Id}");
        dmUpdateReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", dm.Token);
        dmUpdateReq.Content = JsonContent.Create(updateNoteRequest);

        var dmUpdateResponse = await _client.SendAsync(dmUpdateReq);
        dmUpdateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var updatedNote = await dmUpdateResponse.Content.ReadFromJsonAsync<SessionNoteResponseDto>(_jsonOptions);
        updatedNote.Should().NotBeNull();
        updatedNote!.Title.Should().Be(updateNoteRequest.Title);
        updatedNote.Content.Should().Be(updateNoteRequest.Content);

        // 8. DM elimina la nota
        using var dmDeleteReq = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/parties/{party.Id}/notes/{createdNote.Id}");
        dmDeleteReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", dm.Token);
        var dmDeleteResponse = await _client.SendAsync(dmDeleteReq);
        dmDeleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 9. Verificar que ya no existe
        using var dmGetAfterDelete = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/parties/{party.Id}/notes/{createdNote.Id}");
        dmGetAfterDelete.Headers.Authorization = new AuthenticationHeaderValue("Bearer", dm.Token);
        var dmGetAfterDeleteResponse = await _client.SendAsync(dmGetAfterDelete);
        dmGetAfterDeleteResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
