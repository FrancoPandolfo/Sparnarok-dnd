using System.Security.Claims;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Sparnarok.Api.Application.DTOs;
using Sparnarok.Api.Application.Interfaces;
using Sparnarok.Api.Controllers;
using Sparnarok.Api.Domain.Entities;
using Xunit;

namespace Sparnarok.UnitTests;

/// <summary>
/// Pruebas unitarias de autorización multi-inquilino (Multi-Tenant & Security)
/// para el alijo colaborativo (Party Stash) y tesoro común (Party Fund).
/// </summary>
public class PartyStashMultiplayerAuthTests
{
    private readonly IPartyRepository _partyRepoMock = Substitute.For<IPartyRepository>();
    private readonly ICreatureRepository _creatureRepoMock = Substitute.For<ICreatureRepository>();
    private readonly IValidator<CreatePartyRequestDto> _partyValidatorMock = Substitute.For<IValidator<CreatePartyRequestDto>>();
    private readonly IValidator<CreateStashItemRequest> _stashItemValidatorMock = Substitute.For<IValidator<CreateStashItemRequest>>();
    private readonly IValidator<UpdatePartyFundsRequest> _fundsValidatorMock = Substitute.For<IValidator<UpdatePartyFundsRequest>>();
    private readonly IValidator<QueueCraftingItemRequest> _craftingValidatorMock = Substitute.For<IValidator<QueueCraftingItemRequest>>();
    private readonly IStashNotificationService _notifServiceMock = Substitute.For<IStashNotificationService>();

    private PartiesController CreateControllerForUser(Guid userId, string userName = "TestUser")
    {
        var controller = new PartiesController(
            _partyRepoMock,
            _creatureRepoMock,
            _partyValidatorMock,
            _stashItemValidatorMock,
            _fundsValidatorMock,
            _craftingValidatorMock,
            _notifServiceMock);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, userName)
        };

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
            }
        };

        return controller;
    }

    [Fact]
    public async Task ModifyStash_WhenUserDoesNotBelongToParty_Returns403Forbidden()
    {
        // Arrange: Party de Alice con Bob como jugador invitado. Charlie es un intruso ajeno.
        var aliceDmId = Guid.NewGuid();
        var bobPlayerId = Guid.NewGuid();
        var charlieIntruderId = Guid.NewGuid();

        var party = new Party(Guid.NewGuid(), "Mesa de Alice", aliceDmId);
        party.AddPartyMember(new PartyMember(Guid.NewGuid(), party.Id, aliceDmId, "DungeonMaster", "DM Alice"));
        party.AddPartyMember(new PartyMember(Guid.NewGuid(), party.Id, bobPlayerId, "Player", "Bob el Guerrero"));

        _partyRepoMock.GetByIdAsync(party.Id, Arg.Any<CancellationToken>()).Returns(party);
        _stashItemValidatorMock.ValidateAsync(Arg.Any<CreateStashItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());
        _fundsValidatorMock.ValidateAsync(Arg.Any<UpdatePartyFundsRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        var intruderController = CreateControllerForUser(charlieIntruderId, "Charlie Intruder");

        // Act 1: Intento de agregar objeto
        var addResult = await intruderController.AddStashItem(
            party.Id,
            new CreateStashItemRequest("Espada Robada", 1, 3m, "Objeto ajeno", "Rare", "Weapon"),
            CancellationToken.None);

        // Assert 1: Debe retornar 403 Forbidden
        var statusResult1 = addResult.Result.Should().BeOfType<ObjectResult>().Subject;
        statusResult1.StatusCode.Should().Be(StatusCodes.Status403Forbidden);

        // Act 2: Intento de modificar fondos
        var fundsResult = await intruderController.UpdateFunds(
            party.Id,
            new UpdatePartyFundsRequest(0, 0, 0, 9999, 0),
            CancellationToken.None);

        // Assert 2: Debe retornar 403 Forbidden
        var statusResult2 = fundsResult.Result.Should().BeOfType<ObjectResult>().Subject;
        statusResult2.StatusCode.Should().Be(StatusCodes.Status403Forbidden);

        // Act 3: Intento de leer el alijo
        var getStashResult = await intruderController.GetStash(party.Id, CancellationToken.None);

        // Assert 3: Debe retornar 403 Forbidden
        var statusResult3 = getStashResult.Result.Should().BeOfType<ObjectResult>().Subject;
        statusResult3.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task PartyMemberPlayer_CanAccessAndModifyStash_Successfully()
    {
        // Arrange: Bob es jugador en la party de Alice
        var aliceDmId = Guid.NewGuid();
        var bobPlayerId = Guid.NewGuid();

        var party = new Party(Guid.NewGuid(), "Mesa de Alice", aliceDmId);
        party.AddPartyMember(new PartyMember(Guid.NewGuid(), party.Id, aliceDmId, "DungeonMaster", "DM Alice"));
        party.AddPartyMember(new PartyMember(Guid.NewGuid(), party.Id, bobPlayerId, "Player", "Bob el Guerrero"));

        _partyRepoMock.GetByIdAsync(party.Id, Arg.Any<CancellationToken>()).Returns(party);
        _partyRepoMock.GetCraftingQueueAsync(party.Id, Arg.Any<CancellationToken>())
            .Returns(new List<CraftingQueueItem>());
        _stashItemValidatorMock.ValidateAsync(Arg.Any<CreateStashItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        var bobController = CreateControllerForUser(bobPlayerId, "Bob el Guerrero");

        // Act: Bob agrega una Poción de Curación
        var addResult = await bobController.AddStashItem(
            party.Id,
            new CreateStashItemRequest("Potion of Healing", 2, 0.5m, "Cura 2d4+2", "Common", "Potion"),
            CancellationToken.None);

        // Assert: 201 Created y objeto persistido en repositorio
        var createdResult = addResult.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.StatusCode.Should().Be(StatusCodes.Status201Created);
        await _partyRepoMock.Received(1).AddStashItemAsync(Arg.Is<StashItem>(i => i.Name == "Potion of Healing"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IdentifyItem_WhenPlayerAttemptsToIdentify_Returns403Forbidden()
    {
        // Arrange: Bob (Player) intenta identificar un objeto no identificado
        var aliceDmId = Guid.NewGuid();
        var bobPlayerId = Guid.NewGuid();

        var party = new Party(Guid.NewGuid(), "Mesa de Alice", aliceDmId);
        party.AddPartyMember(new PartyMember(Guid.NewGuid(), party.Id, aliceDmId, "DungeonMaster", "DM Alice"));
        party.AddPartyMember(new PartyMember(Guid.NewGuid(), party.Id, bobPlayerId, "Player", "Bob el Guerrero"));

        var magicItem = new StashItem(
            Guid.NewGuid(), party.Id, "Espada Rúnica Desconocida", 1, 3m, "Gemas brillantes", "Rare", "Weapon", false, null, null, isIdentified: false
        );
        party.AddStashItem(magicItem);

        _partyRepoMock.GetByIdAsync(party.Id, Arg.Any<CancellationToken>()).Returns(party);

        var bobController = CreateControllerForUser(bobPlayerId, "Bob el Guerrero");

        // Act: Bob intenta identificar
        var identifyResult = await bobController.IdentifyItem(party.Id, magicItem.Id, CancellationToken.None);

        // Assert: 403 Forbidden (solo Dungeon Master puede identificar)
        var forbiddenResult = identifyResult.Result.Should().BeOfType<ObjectResult>().Subject;
        forbiddenResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        magicItem.IsIdentified.Should().BeFalse();
    }

    [Fact]
    public async Task IdentifyItem_WhenDungeonMasterIdentifies_SucceedsAndEmitsBroadcast()
    {
        // Arrange: Alice (DM) identifica el objeto
        var aliceDmId = Guid.NewGuid();

        var party = new Party(Guid.NewGuid(), "Mesa de Alice", aliceDmId);
        party.AddPartyMember(new PartyMember(Guid.NewGuid(), party.Id, aliceDmId, "DungeonMaster", "DM Alice"));

        var magicItem = new StashItem(
            Guid.NewGuid(), party.Id, "Anillo Misterioso", 1, 0.1m, "Poder arcano", "Rare", "Ring", false, null, null, isIdentified: false
        );
        party.AddStashItem(magicItem);

        _partyRepoMock.GetByIdAsync(party.Id, Arg.Any<CancellationToken>()).Returns(party);

        var aliceController = CreateControllerForUser(aliceDmId, "DM Alice");

        // Act
        var identifyResult = await aliceController.IdentifyItem(party.Id, magicItem.Id, CancellationToken.None);

        // Assert
        var okResult = identifyResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeOfType<StashItemResponseDto>().Subject;
        dto.IsIdentified.Should().BeTrue();
        magicItem.IsIdentified.Should().BeTrue();

        await _notifServiceMock.Received(1).NotifyStashUpdatedAsync(party.Id, Arg.Any<PartyStashResponseDto>(), Arg.Any<CancellationToken>());
    }
}
