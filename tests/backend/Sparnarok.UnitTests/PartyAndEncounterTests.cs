using System.Security.Claims;
using FluentAssertions;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Sparnarok.Api.Application.DTOs;
using Sparnarok.Api.Application.Interfaces;
using Sparnarok.Api.Application.Validators;
using Sparnarok.Api.Controllers;
using Sparnarok.Api.Domain.Entities;
using Sparnarok.Api.Domain.Enums;
using Sparnarok.Api.Domain.ValueObjects;
using Xunit;

namespace Sparnarok.UnitTests;

/// <summary>
/// Pruebas unitarias para el gestor de Partys y Encuentros (Fase 3.5),
/// incluyendo validaciones de dominio y verificación de autorización a nivel de recursos.
/// </summary>
public class PartyAndEncounterTests
{
    private static Creature CreateTestCreature(string name, bool isPlayerCharacter, Guid? ownerUserId = null)
    {
        var abilities = new AbilityScores(16, 14, 14, 10, 12, 8);
        var hp = new HitPoints(30);
        var ac = new ArmorClass(16, 0, false);

        return new Creature(
            name,
            CreatureSize.Medium,
            CreatureType.Humanoid,
            "Neutral",
            challengeRating: 1,
            speedFeet: 30,
            abilities,
            hp,
            ac,
            isPlayerCharacter: isPlayerCharacter,
            ownerUserId: ownerUserId);
    }

    [Fact]
    public void Party_AddMember_WhenCreatureIsPlayerCharacter_AddsSuccessfully()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var party = new Party(Guid.NewGuid(), "La Compañía del Anillo", ownerId);
        var hero = CreateTestCreature("Aragorn", isPlayerCharacter: true, ownerUserId: ownerId);

        // Act
        party.AddMember(hero);

        // Assert
        party.Members.Should().ContainSingle(m => m.Id == hero.Id);
    }

    [Fact]
    public void Party_AddMember_WhenCreatureIsNotPlayerCharacter_ThrowsInvalidOperationException()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var party = new Party(Guid.NewGuid(), "La Compañía del Anillo", ownerId);
        var monster = CreateTestCreature("Goblin", isPlayerCharacter: false);

        // Act
        var act = () => party.AddMember(monster);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*no es un Personaje de Jugador*");
    }

    [Fact]
    public async Task EncountersController_AddCreature_WhenEncounterBelongsToDifferentUser_ReturnsForbidden403()
    {
        // Arrange: Encuentro pertenece a UserA, pero la solicitud proviene de UserB
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var encounterId = Guid.NewGuid();
        var creatureId = Guid.NewGuid();

        var encounter = new Encounter(encounterId, "Emboscada Nocturna", userA, "Difícil");
        var monster = CreateTestCreature("Ogro", isPlayerCharacter: false);

        var encounterRepo = Substitute.For<IEncounterRepository>();
        encounterRepo.GetByIdAsync(encounterId, Arg.Any<CancellationToken>())
            .Returns(encounter);

        var creatureRepo = Substitute.For<ICreatureRepository>();
        creatureRepo.GetByIdAsync(creatureId, Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(monster);

        var validator = new CreateEncounterRequestValidator();
        var controller = new EncountersController(encounterRepo, creatureRepo, validator);

        // Simular que el usuario autenticado en la petición es UserB
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userB.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };

        // Act: UserB intenta agregar un monstruo al encuentro de UserA
        var result = await controller.AddCreature(encounterId, creatureId, CancellationToken.None);

        // Assert: Se debe denegar la acción a nivel de recursos con 403 Forbidden
        var objectResult = result.Result as ObjectResult;
        objectResult.Should().NotBeNull();
        objectResult!.StatusCode.Should().Be(StatusCodes.Status403Forbidden);

        // Verificar que el repositorio no guardó cambios
        await encounterRepo.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EncountersController_AddCreature_WhenEncounterBelongsToCurrentUser_AddsCreatureAndReturnsOk()
    {
        // Arrange: El encuentro pertenece al mismo usuario que ejecuta la petición
        var currentUserId = Guid.NewGuid();
        var encounterId = Guid.NewGuid();
        var monster = CreateTestCreature("Bugbear", isPlayerCharacter: false);

        var encounter = new Encounter(encounterId, "Guarida de Bugbears", currentUserId, "Medio");

        var encounterRepo = Substitute.For<IEncounterRepository>();
        encounterRepo.GetByIdAsync(encounterId, Arg.Any<CancellationToken>())
            .Returns(encounter);

        var creatureRepo = Substitute.For<ICreatureRepository>();
        creatureRepo.GetByIdAsync(monster.Id, currentUserId, Arg.Any<CancellationToken>())
            .Returns(monster);

        var validator = new CreateEncounterRequestValidator();
        var controller = new EncountersController(encounterRepo, creatureRepo, validator);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, currentUserId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };

        // Act
        var result = await controller.AddCreature(encounterId, monster.Id, CancellationToken.None);

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(StatusCodes.Status200OK);

        var dto = okResult.Value as EncounterResponseDto;
        dto.Should().NotBeNull();
        dto!.Creatures.Should().ContainSingle(c => c.Id == monster.Id);

        await encounterRepo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PartiesController_Create_WhenCreatureIsNotPlayerCharacter_ReturnsBadRequest()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var monster = CreateTestCreature("Goblin", isPlayerCharacter: false);

        var partyRepo = Substitute.For<IPartyRepository>();
        var creatureRepo = Substitute.For<ICreatureRepository>();
        creatureRepo.GetByIdAsync(monster.Id, userId, Arg.Any<CancellationToken>())
            .Returns(monster);

        var validator = new CreatePartyRequestValidator();
        var stashValidator = new CreateStashItemRequestValidator();
        var controller = new PartiesController(partyRepo, creatureRepo, validator, stashValidator);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };

        var request = new CreatePartyRequestDto("Party Inválida", new List<Guid> { monster.Id });

        // Act
        var result = await controller.Create(request, CancellationToken.None);

        // Assert: No se debe permitir crear una Party con monstruos no-PC
        var badRequestResult = result.Result as BadRequestObjectResult;
        badRequestResult.Should().NotBeNull();
        badRequestResult!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        await partyRepo.DidNotReceive().AddAsync(Arg.Any<Party>(), Arg.Any<CancellationToken>());
    }
}
