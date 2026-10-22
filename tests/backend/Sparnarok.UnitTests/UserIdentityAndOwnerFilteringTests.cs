using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Sparnarok.Api.Application.Extensions;
using Sparnarok.Api.Domain.Entities;
using Sparnarok.Api.Domain.Enums;
using Sparnarok.Api.Infrastructure.Persistence;
using Sparnarok.Api.Infrastructure.Repositories;
using Sparnarok.Api.Infrastructure.Services;
using Xunit;

namespace Sparnarok.UnitTests;

/// <summary>
/// Pruebas unitarias para la infraestructura de identidad, resolución de ClaimsPrincipal
/// y filtrado seguro por OwnerUserId en repositorios.
/// </summary>
public class UserIdentityAndOwnerFilteringTests
{
    [Fact]
    public void GetUserId_WhenNameIdentifierClaimExists_ReturnsExpectedGuid()
    {
        // Arrange
        var expectedId = Guid.NewGuid();
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, expectedId.ToString()),
            new Claim(ClaimTypes.Email, "architect@sparnarok.dnd")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        // Act
        var result = principal.GetUserId();

        // Assert
        result.Should().Be(expectedId);
    }

    [Fact]
    public void GetUserId_WhenSubClaimExists_ReturnsExpectedGuid()
    {
        // Arrange
        var expectedId = Guid.NewGuid();
        var claims = new[]
        {
            new Claim("sub", expectedId.ToString())
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        // Act
        var result = principal.GetUserId();

        // Assert
        result.Should().Be(expectedId);
    }

    [Fact]
    public void GetUserId_WhenNoUserIdClaimsOrNull_ReturnsNull()
    {
        // Arrange
        ClaimsPrincipal? nullPrincipal = null;
        var emptyPrincipal = new ClaimsPrincipal(new ClaimsIdentity());
        var invalidPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "not-a-guid")
        }));

        // Act & Assert
        nullPrincipal.GetUserId().Should().BeNull();
        emptyPrincipal.GetUserId().Should().BeNull();
        invalidPrincipal.GetUserId().Should().BeNull();
    }

    [Fact]
    public void JwtTokenService_GeneratesValidToken_ContainingExpectedClaimsAndExpiration()
    {
        // Arrange
        var key = "Sparnarok_Testing_Key_For_Unit_Tests_Minimum_256_Bits_2026!";
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "Authentication:Jwt:Key", key },
            { "Authentication:Jwt:Issuer", "SparnarokApi" },
            { "Authentication:Jwt:Audience", "SparnarokWeb" },
            { "Authentication:Jwt:ExpiryDays", "3" }
        };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var jwtService = new JwtTokenService(configuration);
        var user = ApplicationUser.CreateFromGoogle(
            googleSubjectId: "google-sub-998877",
            email: "valeria@heroes.dnd",
            displayName: "Valeria Paladin",
            avatarUrl: "https://avatar.url/valeria.png");

        // Act
        var tokenString = jwtService.GenerateToken(user);

        // Assert
        tokenString.Should().NotBeNullOrWhiteSpace();

        var handler = new JwtSecurityTokenHandler();
        var validationParams = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            ValidateIssuer = true,
            ValidIssuer = "SparnarokApi",
            ValidateAudience = true,
            ValidAudience = "SparnarokWeb",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        var principal = handler.ValidateToken(tokenString, validationParams, out var validatedToken);
        principal.Should().NotBeNull();
        principal.GetUserId().Should().Be(user.Id);
        principal.FindFirst(ClaimTypes.Email)?.Value.Should().Be("valeria@heroes.dnd");
        principal.FindFirst(ClaimTypes.Name)?.Value.Should().Be("Valeria Paladin");
        principal.FindFirst("picture")?.Value.Should().Be("https://avatar.url/valeria.png");

        validatedToken.ValidTo.Should().BeAfter(DateTime.UtcNow.AddDays(2));
    }

    [Fact]
    public void Creature_OwnerUserId_AssignmentAndValidation()
    {
        // Arrange
        var publicCreature = new Creature("Goblin Sentry", CreatureSize.Small, CreatureType.Humanoid, "Neutral Evil", 0.25);
        publicCreature.OwnerUserId.Should().BeNull();

        var userId = Guid.NewGuid();
        var customHero = new Creature(
            "Sir Galahad",
            CreatureSize.Medium,
            CreatureType.Humanoid,
            "Heroic",
            1.0,
            ownerUserId: userId);

        customHero.OwnerUserId.Should().Be(userId);

        // Act - Reassign owner
        var newOwnerId = Guid.NewGuid();
        customHero.SetOwnerUser(newOwnerId);
        customHero.OwnerUserId.Should().Be(newOwnerId);

        // Assert Guid.Empty throws
        var act = () => customHero.SetOwnerUser(Guid.Empty);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task CreatureRepository_GetAllAndGetById_FiltersCorrectlyByOwnerUserId()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<SparnarokDbContext>()
            .UseInMemoryDatabase(databaseName: $"CreatureRepoFilterTest_{Guid.NewGuid()}")
            .Options;

        using var context = new SparnarokDbContext(options);

        var userAId = Guid.NewGuid();
        var userBId = Guid.NewGuid();

        var publicMonster1 = new Creature("Bugbear Stalker", CreatureSize.Medium, CreatureType.Humanoid, "Chaotic Evil", 1);
        var publicMonster2 = new Creature("Gorgon Vanguard", CreatureSize.Large, CreatureType.Monstrosity, "Neutral Evil", 5);

        var heroUserA = new Creature("User A Hero", CreatureSize.Medium, CreatureType.Humanoid, "Heroic", 1, isPlayerCharacter: true, ownerUserId: userAId);
        var heroUserB = new Creature("User B Hero", CreatureSize.Medium, CreatureType.Humanoid, "Heroic", 1, isPlayerCharacter: true, ownerUserId: userBId);

        await context.Creatures.AddRangeAsync(publicMonster1, publicMonster2, heroUserA, heroUserB);
        await context.SaveChangesAsync();

        var repository = new CreatureRepository(context);

        // Act 1: Unauthenticated user (currentUserId = null)
        var unauthenticatedCreatures = await repository.GetAllAsync(null);
        unauthenticatedCreatures.Should().HaveCount(2);
        unauthenticatedCreatures.Select(c => c.Name).Should().Contain(["Bugbear Stalker", "Gorgon Vanguard"]);
        unauthenticatedCreatures.Should().NotContain(c => c.OwnerUserId != null);

        // Act 2: User A queries GetAllAsync
        var userACreatures = await repository.GetAllAsync(userAId);
        userACreatures.Should().HaveCount(3);
        userACreatures.Select(c => c.Name).Should().Contain(["Bugbear Stalker", "Gorgon Vanguard", "User A Hero"]);
        userACreatures.Should().NotContain(c => c.Name == "User B Hero");

        // Act 3: User A queries GetByIdAsync for their own hero -> Should succeed
        var fetchedUserAHero = await repository.GetByIdAsync(heroUserA.Id, userAId);
        fetchedUserAHero.Should().NotBeNull();
        fetchedUserAHero!.Name.Should().Be("User A Hero");

        // Act 4: User A queries GetByIdAsync for User B's hero -> Should return null
        var fetchedUserBHeroAsUserA = await repository.GetByIdAsync(heroUserB.Id, userAId);
        fetchedUserBHeroAsUserA.Should().BeNull();

        // Act 5: Unauthenticated queries public monster by id -> Should succeed
        var fetchedPublicMonster = await repository.GetByIdAsync(publicMonster1.Id, null);
        fetchedPublicMonster.Should().NotBeNull();
        fetchedPublicMonster!.Name.Should().Be("Bugbear Stalker");
    }
}
