using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using NSubstitute;
using Sparnarok.Api.Application.DTOs;
using Sparnarok.Api.Infrastructure.Hubs;
using Xunit;

namespace Sparnarok.UnitTests;

/// <summary>
/// Pruebas unitarias para el Hub de SignalR <see cref="StashHub"/>
/// y el servicio de notificación <see cref="StashNotificationService"/>.
/// </summary>
public class StashHubTests
{
    [Fact]
    public async Task JoinPartyGroup_WhenValidPartyGuid_AddsConnectionIdToGroup()
    {
        // Arrange
        var partyId = Guid.NewGuid();
        var connectionId = "conn-12345";
        var groupManager = Substitute.For<IGroupManager>();
        var hubCallerContext = Substitute.For<HubCallerContext>();
        hubCallerContext.ConnectionId.Returns(connectionId);

        var hub = new StashHub
        {
            Context = hubCallerContext,
            Groups = groupManager
        };

        // Act
        await hub.JoinPartyGroup(partyId.ToString());

        // Assert
        var expectedGroup = StashHub.GetGroupName(partyId);
        await groupManager.Received(1).AddToGroupAsync(connectionId, expectedGroup, default);
    }

    [Fact]
    public async Task LeavePartyGroup_WhenValidPartyGuid_RemovesConnectionIdFromGroup()
    {
        // Arrange
        var partyId = Guid.NewGuid();
        var connectionId = "conn-99999";
        var groupManager = Substitute.For<IGroupManager>();
        var hubCallerContext = Substitute.For<HubCallerContext>();
        hubCallerContext.ConnectionId.Returns(connectionId);

        var hub = new StashHub
        {
            Context = hubCallerContext,
            Groups = groupManager
        };

        // Act
        await hub.LeavePartyGroup(partyId.ToString());

        // Assert
        var expectedGroup = StashHub.GetGroupName(partyId);
        await groupManager.Received(1).RemoveFromGroupAsync(connectionId, expectedGroup, default);
    }

    [Fact]
    public async Task JoinPartyGroup_WhenInvalidPartyId_DoesNotCallGroupManager()
    {
        // Arrange
        var groupManager = Substitute.For<IGroupManager>();
        var hubCallerContext = Substitute.For<HubCallerContext>();
        hubCallerContext.ConnectionId.Returns("conn-123");

        var hub = new StashHub
        {
            Context = hubCallerContext,
            Groups = groupManager
        };

        // Act
        await hub.JoinPartyGroup("not-a-guid");

        // Assert
        await groupManager.DidNotReceiveWithAnyArgs().AddToGroupAsync(default!, default!, default);
    }

    [Fact]
    public async Task LeavePartyGroup_WhenInvalidPartyId_DoesNotCallGroupManager()
    {
        // Arrange
        var groupManager = Substitute.For<IGroupManager>();
        var hubCallerContext = Substitute.For<HubCallerContext>();
        hubCallerContext.ConnectionId.Returns("conn-123");

        var hub = new StashHub
        {
            Context = hubCallerContext,
            Groups = groupManager
        };

        // Act
        await hub.LeavePartyGroup("invalid-guid");

        // Assert
        await groupManager.DidNotReceiveWithAnyArgs().RemoveFromGroupAsync(default!, default!, default);
    }

    [Fact]
    public void GetGroupName_ReturnsFormattedStringWithPrefix()
    {
        // Arrange
        var partyId = Guid.Parse("11111111-2222-3333-4444-555555555555");

        // Act
        var groupName = StashHub.GetGroupName(partyId);

        // Assert
        groupName.Should().Be("party_11111111-2222-3333-4444-555555555555");
    }

    [Fact]
    public async Task StashNotificationService_NotifyItemAddedAsync_SendsItemAddedMessageToGroup()
    {
        // Arrange
        var partyId = Guid.NewGuid();
        var hubContext = Substitute.For<IHubContext<StashHub>>();
        var hubClients = Substitute.For<IHubClients>();
        var clientProxy = Substitute.For<IClientProxy>();

        hubContext.Clients.Returns(hubClients);
        hubClients.Group(StashHub.GetGroupName(partyId)).Returns(clientProxy);

        var service = new StashNotificationService(hubContext);
        var itemDto = new StashItemResponseDto(
            Guid.NewGuid(), partyId, "Bag of Holding", 1, 15m, "Mágico", "Uncommon", "Wondrous Item", true, "bag-of-holding", null, DateTime.UtcNow
        );

        // Act
        await service.NotifyItemAddedAsync(partyId, itemDto);

        // Assert
        await clientProxy.Received(1).SendCoreAsync(
            "ItemAdded",
            Arg.Is<object?[]>(args => args.Length == 1 && ReferenceEquals(args[0], itemDto)),
            default);
    }

    [Fact]
    public async Task StashNotificationService_NotifyFundsUpdatedAsync_SendsFundsUpdatedMessageToGroup()
    {
        // Arrange
        var partyId = Guid.NewGuid();
        var hubContext = Substitute.For<IHubContext<StashHub>>();
        var hubClients = Substitute.For<IHubClients>();
        var clientProxy = Substitute.For<IClientProxy>();

        hubContext.Clients.Returns(hubClients);
        hubClients.Group(StashHub.GetGroupName(partyId)).Returns(clientProxy);

        var service = new StashNotificationService(hubContext);
        var fundDto = new PartyFundDto(partyId, 10, 20, 5, 250, 2, 280.1m);

        // Act
        await service.NotifyFundsUpdatedAsync(partyId, fundDto);

        // Assert
        await clientProxy.Received(1).SendCoreAsync(
            "FundsUpdated",
            Arg.Is<object?[]>(args => args.Length == 1 && ReferenceEquals(args[0], fundDto)),
            default);
    }
}
