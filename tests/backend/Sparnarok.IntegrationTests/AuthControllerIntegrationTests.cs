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
/// Pruebas de integración para autenticación con Google OAuth2, emisión de JWT y aislamiento de datos por usuario.
/// </summary>
public class AuthControllerIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public AuthControllerIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SparnarokDbContext>();
        TestDatabaseInitializer.EnsureCreated(context);
    }

    [Fact]
    public async Task LoginWithGoogle_WithEmptyToken_ReturnsBadRequest()
    {
        // Arrange
        var request = new GoogleLoginRequestDto("");

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/google", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task LoginWithGoogle_WithValidMockToken_RegistersUserAndReturnsJwt()
    {
        // Arrange
        var email = $"architect_{Guid.NewGuid():N}@sparnarok.dnd";
        var googleSub = $"google-sub-{Guid.NewGuid():N}";
        var displayName = "Master Architect";
        var idToken = $"mock-google-token:{email}:{displayName}:{googleSub}:https://avatar.url/avatar.png";

        var request = new GoogleLoginRequestDto(idToken);

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/google", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var authData = await response.Content.ReadFromJsonAsync<AuthResponseDto>(_jsonOptions);
        authData.Should().NotBeNull();
        authData!.Token.Should().NotBeNullOrWhiteSpace();
        authData.Email.Should().Be(email);
        authData.DisplayName.Should().Be(displayName);
        authData.AvatarUrl.Should().Be("https://avatar.url/avatar.png");
        authData.UserId.Should().NotBeEmpty();

        // Verificar persistencia en base de datos PostgreSQL
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SparnarokDbContext>();
        var persistedUser = await db.Users.FirstOrDefaultAsync(u => u.Id == authData.UserId);
        persistedUser.Should().NotBeNull();
        persistedUser!.GoogleSubjectId.Should().Be(googleSub);
        persistedUser.Email.Should().Be(email);

        // Act 2: Probar endpoint protegido /api/auth/me con el token emitido
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authData.Token);
        var meResponse = await _client.GetAsync("/api/auth/me");
        meResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var meData = await meResponse.Content.ReadFromJsonAsync<AuthResponseDto>(_jsonOptions);
        meData.Should().NotBeNull();
        meData!.UserId.Should().Be(authData.UserId);
        meData.Email.Should().Be(email);
    }

    [Fact]
    public async Task GetCurrentUser_WithoutToken_ReturnsUnauthorized()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = null;

        // Act
        var response = await _client.GetAsync("/api/auth/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CustomHeroes_AreIsolatedBetweenDifferentUsers()
    {
        // 1. Iniciar sesión como Usuario A
        var userAEmail = $"user_a_{Guid.NewGuid():N}@test.dnd";
        var loginA = await _client.PostAsJsonAsync(
            "/api/auth/google",
            new GoogleLoginRequestDto($"mock-google-token:{userAEmail}:User A:sub-a-{Guid.NewGuid():N}"));
        var authA = await loginA.Content.ReadFromJsonAsync<AuthResponseDto>(_jsonOptions);
        authA.Should().NotBeNull();

        // 2. Iniciar sesión como Usuario B
        var userBEmail = $"user_b_{Guid.NewGuid():N}@test.dnd";
        var loginB = await _client.PostAsJsonAsync(
            "/api/auth/google",
            new GoogleLoginRequestDto($"mock-google-token:{userBEmail}:User B:sub-b-{Guid.NewGuid():N}"));
        var authB = await loginB.Content.ReadFromJsonAsync<AuthResponseDto>(_jsonOptions);
        authB.Should().NotBeNull();

        // 3. Usuario A crea un Héroe Custom
        var heroAName = $"Sir Lancelot {Guid.NewGuid():N}";
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authA!.Token);
        var createHeroAResponse = await _client.PostAsJsonAsync("/api/v1/creatures/custom", new CreateCustomHeroRequestDto
        {
            Name = heroAName,
            MaxHitPoints = 40,
            ArmorClass = 17,
            Strength = 16,
            Dexterity = 12,
            Constitution = 14,
            Intelligence = 10,
            Wisdom = 10,
            Charisma = 12,
            AttackName = "Lance",
            DamageFormula = "1d12+3",
            AttackModifierAbility = AbilityScoreType.Strength
        });
        createHeroAResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var heroA = await createHeroAResponse.Content.ReadFromJsonAsync<CreatureResponseDto>(_jsonOptions);
        heroA.Should().NotBeNull();
        heroA!.OwnerUserId.Should().Be(authA.UserId);

        // 4. Usuario A lista criaturas -> debe ver su héroe
        var listResponseA = await _client.GetAsync("/api/v1/creatures");
        listResponseA.StatusCode.Should().Be(HttpStatusCode.OK);
        var listA = await listResponseA.Content.ReadFromJsonAsync<List<CreatureResponseDto>>(_jsonOptions);
        listA.Should().Contain(c => c.Name == heroAName);

        // 5. Usuario B lista criaturas -> NO debe ver el héroe de Usuario A
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authB!.Token);
        var listResponseB = await _client.GetAsync("/api/v1/creatures");
        listResponseB.StatusCode.Should().Be(HttpStatusCode.OK);
        var listB = await listResponseB.Content.ReadFromJsonAsync<List<CreatureResponseDto>>(_jsonOptions);
        listB.Should().NotContain(c => c.Name == heroAName);

        // 6. Usuario B intenta obtener el héroe de Usuario A por ID -> debe retornar 404
        var getByIdResponseB = await _client.GetAsync($"/api/v1/creatures/{heroA.Id}");
        getByIdResponseB.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // 7. Usuario no autenticado lista criaturas -> NO debe ver el héroe de Usuario A
        _client.DefaultRequestHeaders.Authorization = null;
        var unauthListResponse = await _client.GetAsync("/api/v1/creatures");
        unauthListResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var unauthList = await unauthListResponse.Content.ReadFromJsonAsync<List<CreatureResponseDto>>(_jsonOptions);
        unauthList.Should().NotContain(c => c.Name == heroAName);

        // Cleanup
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SparnarokDbContext>();
        var dbHeroA = await db.Creatures.Include(c => c.Actions).FirstOrDefaultAsync(c => c.Id == heroA.Id);
        if (dbHeroA != null)
        {
            db.Creatures.Remove(dbHeroA);
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task RegisterAndLogin_WithConventionalCredentials_Succeeds()
    {
        // Arrange
        var email = $"fighter_{Guid.NewGuid():N}@sparnarok.dnd";
        var password = "StrongPassword123!";
        var displayName = "Valeros the Fighter";

        // 1. Registro
        var regResponse = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequestDto(email, password, displayName));
        regResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var regAuth = await regResponse.Content.ReadFromJsonAsync<AuthResponseDto>(_jsonOptions);
        regAuth.Should().NotBeNull();
        regAuth!.Email.Should().Be(email);
        regAuth.DisplayName.Should().Be(displayName);
        regAuth.HasPassword.Should().BeTrue();
        regAuth.HasGoogleLinked.Should().BeFalse();

        // 2. Login con contraseña correcta
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto(email, password));
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var loginAuth = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>(_jsonOptions);
        loginAuth.Should().NotBeNull();
        loginAuth!.UserId.Should().Be(regAuth.UserId);

        // 3. Login con contraseña incorrecta -> Unauthorized
        var failResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto(email, "WrongPassword!"));
        failResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RegisterConventional_ThenLoginWithGoogle_AutoLinksWithoutConflict()
    {
        // Arrange
        var email = $"rogue_{Guid.NewGuid():N}@sparnarok.dnd";
        var password = "ShadowPassword123!";
        var displayName = "Merisiel";

        // 1. Registro convencional
        var regResponse = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequestDto(email, password, displayName));
        regResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var regAuth = await regResponse.Content.ReadFromJsonAsync<AuthResponseDto>(_jsonOptions);
        regAuth!.HasPassword.Should().BeTrue();
        regAuth.HasGoogleLinked.Should().BeFalse();

        // 2. El usuario inicia sesión con Google usando el mismo email
        var googleSub = $"google-sub-{Guid.NewGuid():N}";
        var idToken = $"mock-google-token:{email}:Merisiel Thief:{googleSub}:https://avatar.url/merisiel.png";
        var googleResponse = await _client.PostAsJsonAsync("/api/auth/google", new GoogleLoginRequestDto(idToken));
        googleResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var googleAuth = await googleResponse.Content.ReadFromJsonAsync<AuthResponseDto>(_jsonOptions);
        googleAuth.Should().NotBeNull();
        googleAuth!.UserId.Should().Be(regAuth.UserId);
        googleAuth.HasPassword.Should().BeTrue();
        googleAuth.HasGoogleLinked.Should().BeTrue();

        // 3. Puede seguir iniciando sesión con contraseña convencional
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto(email, password));
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var loginAuth = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>(_jsonOptions);
        loginAuth!.HasGoogleLinked.Should().BeTrue();
        loginAuth.HasPassword.Should().BeTrue();
    }

    [Fact]
    public async Task RegisterWithGoogle_ThenSetPassword_AllowsConventionalLogin()
    {
        // Arrange
        var email = $"wizard_{Guid.NewGuid():N}@sparnarok.dnd";
        var googleSub = $"google-sub-{Guid.NewGuid():N}";
        var idToken = $"mock-google-token:{email}:Ezren The Wizard:{googleSub}:https://avatar.url/ezren.png";

        // 1. Iniciar sesión con Google
        var googleResponse = await _client.PostAsJsonAsync("/api/auth/google", new GoogleLoginRequestDto(idToken));
        googleResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var googleAuth = await googleResponse.Content.ReadFromJsonAsync<AuthResponseDto>(_jsonOptions);
        googleAuth!.HasGoogleLinked.Should().BeTrue();
        googleAuth.HasPassword.Should().BeFalse();

        // 2. Intentar login convencional sin contraseña -> Unauthorized con mensaje explicativo
        var preLoginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto(email, "AnyPassword123!"));
        preLoginResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // 3. Usuario autenticado establece una contraseña
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", googleAuth.Token);
        var setPassResponse = await _client.PostAsJsonAsync("/api/auth/set-password", new SetPasswordRequestDto("MagicPass2026!"));
        setPassResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var setPassAuth = await setPassResponse.Content.ReadFromJsonAsync<AuthResponseDto>(_jsonOptions);
        setPassAuth!.HasPassword.Should().BeTrue();
        setPassAuth.HasGoogleLinked.Should().BeTrue();

        // 4. Ahora puede loguearse convencionalmente con la nueva contraseña
        _client.DefaultRequestHeaders.Authorization = null;
        var postLoginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto(email, "MagicPass2026!"));
        postLoginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var postLoginAuth = await postLoginResponse.Content.ReadFromJsonAsync<AuthResponseDto>(_jsonOptions);
        postLoginAuth!.UserId.Should().Be(googleAuth.UserId);
        postLoginAuth.HasPassword.Should().BeTrue();
        postLoginAuth.HasGoogleLinked.Should().BeTrue();
    }
}

