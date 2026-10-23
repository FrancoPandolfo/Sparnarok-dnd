using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Sparnarok.Api.Application.DTOs;
using Sparnarok.Api.Application.Interfaces;
using Sparnarok.Api.Application.Validators;
using Sparnarok.Api.Controllers;
using Sparnarok.Api.Domain.Entities;
using Sparnarok.Api.Infrastructure.Persistence;
using Xunit;

namespace Sparnarok.UnitTests;

/// <summary>
/// Pruebas unitarias para la Identidad Global (UsersController) y el Onboarding de Party (PartiesController)
/// correspondientes a la Fase 3.7.
/// </summary>
public class PartyOnboardingAndIdentityTests
{
    private static SparnarokDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<SparnarokDbContext>()
            .UseInMemoryDatabase(databaseName: $"Sparnarok_Onboarding_{Guid.NewGuid():N}")
            .Options;
        return new SparnarokDbContext(options);
    }

    [Fact]
    public async Task UsersController_UpdateProfile_UpdatesGlobalNickname()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var user = ApplicationUser.CreateConventional("test@sparnarok.dnd", "hashed_pwd", "Aventurero Original");
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var controller = new UsersController(context);
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()) };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")) }
        };

        // Act
        var result = await controller.UpdateProfile(new UpdateUserProfileRequestDto("DungeonMaster_Pro"), CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeOfType<UserProfileResponseDto>().Subject;
        dto.GlobalNickname.Should().Be("DungeonMaster_Pro");
        dto.Email.Should().Be(user.Email);

        var refreshedUser = await context.Users.FirstAsync(u => u.Id == user.Id);
        refreshedUser.GlobalNickname.Should().Be("DungeonMaster_Pro");
    }

    [Fact]
    public async Task PartiesController_Create_RegistersCreatorWithRoleAndInGameName()
    {
        // Arrange
        var partyRepoMock = Substitute.For<IPartyRepository>();
        var creatureRepoMock = Substitute.For<ICreatureRepository>();
        var validator = new CreatePartyRequestValidator();
        var stashValidator = Substitute.For<FluentValidation.IValidator<CreateStashItemRequest>>();
        var fundsValidator = Substitute.For<FluentValidation.IValidator<UpdatePartyFundsRequest>>();
        var craftingValidator = Substitute.For<FluentValidation.IValidator<QueueCraftingItemRequest>>();
        var notifService = Substitute.For<IStashNotificationService>();

        Party? savedParty = null;
        await partyRepoMock.AddAsync(Arg.Do<Party>(p => savedParty = p), Arg.Any<CancellationToken>());

        var controller = new PartiesController(
            partyRepoMock,
            creatureRepoMock,
            validator,
            stashValidator,
            fundsValidator,
            craftingValidator,
            notifService);

        var userId = Guid.NewGuid();
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, "El Señor Oscuro")
        };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")) }
        };

        var request = new CreatePartyRequestDto("La Legión Maldita", new List<Guid>(), "DungeonMaster", "El Señor Oscuro");

        // Act
        var result = await controller.Create(request, CancellationToken.None);

        // Assert
        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var dto = createdResult.Value.Should().BeOfType<PartyResponseDto>().Subject;

        savedParty.Should().NotBeNull();
        savedParty!.PartyMembers.Should().HaveCount(1);
        var creatorMember = savedParty.PartyMembers.First();
        creatorMember.Role.Should().Be("DungeonMaster");
        creatorMember.InGameName.Should().Be("El Señor Oscuro");
        creatorMember.UserId.Should().Be(userId);
        savedParty.InviteToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task PartiesController_JoinByToken_AddsInvitedPlayerWithRoleAndCharacterName()
    {
        // Arrange
        var partyRepoMock = Substitute.For<IPartyRepository>();
        var creatureRepoMock = Substitute.For<ICreatureRepository>();
        var validator = new CreatePartyRequestValidator();
        var stashValidator = Substitute.For<FluentValidation.IValidator<CreateStashItemRequest>>();
        var fundsValidator = Substitute.For<FluentValidation.IValidator<UpdatePartyFundsRequest>>();
        var craftingValidator = Substitute.For<FluentValidation.IValidator<QueueCraftingItemRequest>>();
        var notifService = Substitute.For<IStashNotificationService>();

        var dmId = Guid.NewGuid();
        var existingParty = new Party(Guid.NewGuid(), "La Orden del Roble", dmId);
        existingParty.AddPartyMember(new PartyMember(Guid.NewGuid(), existingParty.Id, dmId, "DungeonMaster", "El Cronista"));

        partyRepoMock.GetByInviteTokenAsync(existingParty.InviteToken, Arg.Any<CancellationToken>())
            .Returns(existingParty);

        var controller = new PartiesController(
            partyRepoMock,
            creatureRepoMock,
            validator,
            stashValidator,
            fundsValidator,
            craftingValidator,
            notifService);

        var playerId = Guid.NewGuid();
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, playerId.ToString()),
            new Claim(ClaimTypes.Name, "Player User")
        };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")) }
        };

        var joinRequest = new JoinPartyRequestDto("Player", "Elendur");

        // Act
        var result = await controller.JoinByToken(existingParty.InviteToken, joinRequest, CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeOfType<PartyResponseDto>().Subject;

        existingParty.PartyMembers.Should().HaveCount(2);
        var playerMember = existingParty.PartyMembers.FirstOrDefault(m => m.UserId == playerId);
        playerMember.Should().NotBeNull();
        playerMember!.Role.Should().Be("Player");
        playerMember.InGameName.Should().Be("Elendur");
        await partyRepoMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
