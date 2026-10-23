using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using NSubstitute;
using Sparnarok.Api.Application.DTOs;
using Sparnarok.Api.Domain.Models;
using Sparnarok.Api.Infrastructure.Hubs;
using Sparnarok.Api.Infrastructure.Services;
using Xunit;

namespace Sparnarok.UnitTests;

/// <summary>
/// Pruebas unitarias para <see cref="LiveCombatHub"/> y el gestor de estado <see cref="LiveCombatManager"/>.
/// Valida estrictamente que las proyecciones para Jugadores no filtren monstruos ocultos ni CA/HP exactos (anti-metagaming).
/// </summary>
public class LiveCombatHubTests
{
    [Fact]
    public async Task JoinCombat_WhenRoleIsPlayer_SendsRedactedProjectionOmitingHiddenMonsters()
    {
        // Arrange
        var partyId = Guid.NewGuid();
        var manager = new LiveCombatManager();

        var hero = new LiveCombatant(Guid.NewGuid(), "Aragorn", maxHp: 45, armorClass: 16, isPlayerCharacter: true, isVisibleToPlayers: true);
        var visibleGoblin = new LiveCombatant(Guid.NewGuid(), "Goblin Scout", maxHp: 12, armorClass: 13, isPlayerCharacter: false, isVisibleToPlayers: true);
        var hiddenBoss = new LiveCombatant(Guid.NewGuid(), "Goblin Boss", maxHp: 35, armorClass: 17, isPlayerCharacter: false, isVisibleToPlayers: false);

        manager.InitializeCombat(partyId, "La Compañía", new[] { hero, visibleGoblin, hiddenBoss });

        var callerProxy = Substitute.For<ISingleClientProxy>();
        var hubClients = Substitute.For<IHubCallerClients>();
        hubClients.Caller.Returns(callerProxy);

        var groups = Substitute.For<IGroupManager>();
        var context = Substitute.For<HubCallerContext>();
        context.ConnectionId.Returns("conn-player-1");

        var hub = new LiveCombatHub(manager)
        {
            Clients = hubClients,
            Groups = groups,
            Context = context
        };

        // Act
        await hub.JoinCombat(partyId.ToString(), "Player");

        // Assert: Se agregó al grupo de jugadores
        await groups.Received(1).AddToGroupAsync("conn-player-1", LiveCombatHub.GetPlayerGroupName(partyId), default);

        // Capturar la proyección enviada al llamador
        await callerProxy.Received(1).SendCoreAsync(
            "CombatStateUpdated",
            Arg.Do<object?[]>(args =>
            {
                args.Should().ContainSingle();
                var projection = args[0] as LiveCombatPlayerProjectionDto;
                projection.Should().NotBeNull();

                // Verificar combatientes visibles
                projection!.Combatants.Should().HaveCount(2); // Hero + Visible Goblin
                projection.Combatants.Should().Contain(c => c.Name == "Aragorn");
                projection.Combatants.Should().Contain(c => c.Name == "Goblin Scout");
                projection.Combatants.Should().NotContain(c => c.Name == "Goblin Boss");

                // Serializar a JSON para verificar estanqueidad absoluta contra filtraciones
                var json = JsonSerializer.Serialize(projection);

                json.Should().NotContain("Goblin Boss", "El payload JSON para jugadores jamás debe filtrar el nombre de monstruos ocultos.");
                json.Should().NotContain("ArmorClass", "El payload para jugadores no debe exponer la Clase de Armadura de enemigos.");
                json.Should().NotContain("\"currentHp\":12", "Los monstruos visibles no deben exponer sus puntos de golpe exactos a los jugadores.");

                // El héroe sí expone sus propios puntos de golpe
                var heroInJson = projection.Combatants.First(c => c.Name == "Aragorn");
                heroInJson.CurrentHp.Should().Be(45);
                heroInJson.MaxHp.Should().Be(45);

                // El monstruo visible tiene HP numérico nulo y estado saludable
                var goblinInJson = projection.Combatants.First(c => c.Name == "Goblin Scout");
                goblinInJson.CurrentHp.Should().BeNull();
                goblinInJson.MaxHp.Should().BeNull();
                goblinInJson.HealthStatus.Should().Be("Healthy");
            }),
            default);
    }

    [Fact]
    public async Task JoinCombat_WhenRoleIsDungeonMaster_SendsFullUncensoredProjection()
    {
        // Arrange
        var partyId = Guid.NewGuid();
        var manager = new LiveCombatManager();

        var hero = new LiveCombatant(Guid.NewGuid(), "Legolas", maxHp: 38, armorClass: 15, isPlayerCharacter: true);
        var hiddenBoss = new LiveCombatant(Guid.NewGuid(), "Goblin Boss", maxHp: 50, armorClass: 17, isPlayerCharacter: false, isVisibleToPlayers: false);

        manager.InitializeCombat(partyId, "La Compañía", new[] { hero, hiddenBoss });

        var callerProxy = Substitute.For<ISingleClientProxy>();
        var hubClients = Substitute.For<IHubCallerClients>();
        hubClients.Caller.Returns(callerProxy);

        var groups = Substitute.For<IGroupManager>();
        var context = Substitute.For<HubCallerContext>();
        context.ConnectionId.Returns("conn-dm-1");

        var hub = new LiveCombatHub(manager)
        {
            Clients = hubClients,
            Groups = groups,
            Context = context
        };

        // Act
        await hub.JoinCombat(partyId.ToString(), "DungeonMaster");

        // Assert: Se agregó al grupo del DM
        await groups.Received(1).AddToGroupAsync("conn-dm-1", LiveCombatHub.GetDmGroupName(partyId), default);

        await callerProxy.Received(1).SendCoreAsync(
            "CombatStateUpdated",
            Arg.Do<object?[]>(args =>
            {
                var projection = args[0] as LiveCombatDmProjectionDto;
                projection.Should().NotBeNull();
                projection!.Combatants.Should().HaveCount(2);

                var boss = projection.Combatants.First(c => c.Name == "Goblin Boss");
                boss.CurrentHp.Should().Be(50);
                boss.MaxHp.Should().Be(50);
                boss.ArmorClass.Should().Be(17);
                boss.IsVisibleToPlayers.Should().BeFalse();
            }),
            default);
    }

    [Fact]
    public void LiveCombatManager_WhenActiveTurnIsHiddenMonster_RedactsActiveCombatantNameForPlayers()
    {
        // Arrange
        var partyId = Guid.NewGuid();
        var manager = new LiveCombatManager();

        var hiddenBoss = new LiveCombatant(Guid.NewGuid(), "Goblin Boss", maxHp: 60, armorClass: 17, isPlayerCharacter: false, isVisibleToPlayers: false, initiative: 25);
        var hero = new LiveCombatant(Guid.NewGuid(), "Gimli", maxHp: 50, armorClass: 18, isPlayerCharacter: true, isVisibleToPlayers: true, initiative: 12);

        manager.InitializeCombat(partyId, "Party", new[] { hiddenBoss, hero });
        manager.StartCombat(partyId); // Boss está en la posición 0, actúa primero en el turno 0

        // Act
        var dmProj = manager.CreateDmProjection(partyId);
        var playerProj = manager.CreatePlayerProjection(partyId);

        // Assert
        dmProj.ActiveCombatantName.Should().Be("Goblin Boss");
        dmProj.ActiveCombatantId.Should().Be(hiddenBoss.Id);

        playerProj.ActiveCombatantName.Should().Be("Turno del Enemigo");
        playerProj.ActiveCombatantId.Should().BeNull();
    }

    [Fact]
    public void LiveCombatManager_AutoRollMonsters_OnlyRollsForMonstersAndPreservesHeroes()
    {
        // Arrange
        var partyId = Guid.NewGuid();
        var manager = new LiveCombatManager();

        var hero = new LiveCombatant(Guid.NewGuid(), "Aragorn", maxHp: 40, armorClass: 16, isPlayerCharacter: true, initiative: null);
        var monster1 = new LiveCombatant(Guid.NewGuid(), "Orco 1", maxHp: 20, armorClass: 13, isPlayerCharacter: false, initiative: null);
        var monster2 = new LiveCombatant(Guid.NewGuid(), "Orco 2", maxHp: 20, armorClass: 13, isPlayerCharacter: false, initiative: null);

        manager.InitializeCombat(partyId, "Party", new[] { hero, monster1, monster2 });

        // Act
        manager.AutoRollMonsters(partyId, () => 15);

        // Assert
        var state = manager.GetState(partyId)!;
        state.Combatants.First(c => c.Name == "Aragorn").Initiative.Should().BeNull("El auto-roll no debe sobreescribir la iniciativa de los héroes.");
        state.Combatants.First(c => c.Name == "Orco 1").Initiative.Should().Be(15);
        state.Combatants.First(c => c.Name == "Orco 2").Initiative.Should().Be(15);
    }

    [Fact]
    public void LiveCombatManager_TurnAdvancement_CyclesCombatantsAndIncrementsRounds()
    {
        // Arrange
        var partyId = Guid.NewGuid();
        var manager = new LiveCombatManager();

        var c1 = new LiveCombatant(Guid.NewGuid(), "A", 20, 10, true, initiative: 20);
        var c2 = new LiveCombatant(Guid.NewGuid(), "B", 20, 10, true, initiative: 15);

        manager.InitializeCombat(partyId, "Party", new[] { c1, c2 });
        manager.StartCombat(partyId);

        var state = manager.GetState(partyId)!;
        state.CurrentRound.Should().Be(1);
        state.CurrentTurnIndex.Should().Be(0);
        state.GetCurrentCombatant()!.Name.Should().Be("A");

        // Act 1: Siguiente turno
        manager.NextTurn(partyId);
        state.CurrentRound.Should().Be(1);
        state.CurrentTurnIndex.Should().Be(1);
        state.GetCurrentCombatant()!.Name.Should().Be("B");

        // Act 2: Vuelve al primer combatiente e incrementa la ronda
        manager.NextTurn(partyId);
        state.CurrentRound.Should().Be(2);
        state.CurrentTurnIndex.Should().Be(0);
        state.GetCurrentCombatant()!.Name.Should().Be("A");

        // Act 3: Turno anterior retrocede a ronda 1 combatiente B
        manager.PreviousTurn(partyId);
        state.CurrentRound.Should().Be(1);
        state.CurrentTurnIndex.Should().Be(1);
        state.GetCurrentCombatant()!.Name.Should().Be("B");
    }

    [Fact]
    public void LiveCombatManager_ModifyCombatantHp_ClampsBetweenZeroAndMaxHp()
    {
        // Arrange
        var partyId = Guid.NewGuid();
        var manager = new LiveCombatManager();
        var combatant = new LiveCombatant(Guid.NewGuid(), "Tanque", maxHp: 30, armorClass: 18, isPlayerCharacter: true);

        manager.InitializeCombat(partyId, "Party", new[] { combatant });

        // Act 1: Daño masivo mayor a MaxHp
        manager.ModifyCombatantHp(partyId, combatant.Id, -50);
        combatant.CurrentHp.Should().Be(0);

        // Act 2: Curación que excede MaxHp
        manager.ModifyCombatantHp(partyId, combatant.Id, +100);
        combatant.CurrentHp.Should().Be(30);
    }

    [Fact]
    public void LiveCombatManager_ReorderCombatant_MovesElementFromIndex0ToIndex3_ShiftsOthersAndRecalculatesSortOrder()
    {
        // Arrange
        var partyId = Guid.NewGuid();
        var manager = new LiveCombatManager();

        var c0 = new LiveCombatant(Guid.NewGuid(), "Combatant A", 20, 10, true);
        var c1 = new LiveCombatant(Guid.NewGuid(), "Combatant B", 20, 10, true);
        var c2 = new LiveCombatant(Guid.NewGuid(), "Combatant C", 20, 10, true);
        var c3 = new LiveCombatant(Guid.NewGuid(), "Combatant D", 20, 10, true);

        manager.InitializeCombat(partyId, "Party Test", new[] { c0, c1, c2, c3 });

        // Act: Mover el combatiente en el índice 0 (A) al índice 3
        var reorderResult = manager.ReorderCombatant(partyId, c0.Id, 3);

        // Assert
        reorderResult.Should().BeTrue();
        var state = manager.GetState(partyId)!;
        state.Combatants.Should().HaveCount(4);

        // Validar orden posicional desplazado
        state.Combatants[0].Id.Should().Be(c1.Id);
        state.Combatants[0].Name.Should().Be("Combatant B");
        state.Combatants[0].SortOrder.Should().Be(0);

        state.Combatants[1].Id.Should().Be(c2.Id);
        state.Combatants[1].Name.Should().Be("Combatant C");
        state.Combatants[1].SortOrder.Should().Be(1);

        state.Combatants[2].Id.Should().Be(c3.Id);
        state.Combatants[2].Name.Should().Be("Combatant D");
        state.Combatants[2].SortOrder.Should().Be(2);

        state.Combatants[3].Id.Should().Be(c0.Id);
        state.Combatants[3].Name.Should().Be("Combatant A");
        state.Combatants[3].SortOrder.Should().Be(3);
    }

    [Fact]
    public void LiveCombatManager_ReorderCombatant_PreservesActiveCombatantTurn()
    {
        // Arrange
        var partyId = Guid.NewGuid();
        var manager = new LiveCombatManager();

        var c0 = new LiveCombatant(Guid.NewGuid(), "Hero 1", 30, 12, true);
        var c1 = new LiveCombatant(Guid.NewGuid(), "Hero 2 (Active)", 30, 12, true);
        var c2 = new LiveCombatant(Guid.NewGuid(), "Monster 1", 30, 12, false);
        var c3 = new LiveCombatant(Guid.NewGuid(), "Monster 2", 30, 12, false);

        manager.InitializeCombat(partyId, "Party Test", new[] { c0, c1, c2, c3 });
        manager.StartCombat(partyId);
        manager.NextTurn(partyId); // CurrentTurnIndex = 1 -> "Hero 2 (Active)"

        var stateBefore = manager.GetState(partyId)!;
        stateBefore.GetCurrentCombatant()!.Name.Should().Be("Hero 2 (Active)");
        stateBefore.CurrentTurnIndex.Should().Be(1);

        // Act: Mover c0 (índice 0) al índice 3 -> c1 pasa al índice 0
        manager.ReorderCombatant(partyId, c0.Id, 3);

        // Assert: El combatiente activo sigue siendo Hero 2, y CurrentTurnIndex se actualizó a 0
        var stateAfter = manager.GetState(partyId)!;
        stateAfter.GetCurrentCombatant()!.Name.Should().Be("Hero 2 (Active)");
        stateAfter.CurrentTurnIndex.Should().Be(0);
    }

    [Fact]
    public void LiveCombatManager_StartCombat_DoesNotSortByInitiativeAndAllowsNullInitiatives()
    {
        // Arrange
        var partyId = Guid.NewGuid();
        var manager = new LiveCombatManager();

        var c0 = new LiveCombatant(Guid.NewGuid(), "No Initiative Rogue", 25, 14, true, initiative: null);
        var c1 = new LiveCombatant(Guid.NewGuid(), "High Initiative Wizard", 18, 12, true, initiative: 22);
        var c2 = new LiveCombatant(Guid.NewGuid(), "Mid Initiative Fighter", 35, 16, true, initiative: 14);

        manager.InitializeCombat(partyId, "Party Test", new[] { c0, c1, c2 });

        // Act: Iniciar combate
        manager.StartCombat(partyId);

        // Assert: El orden posicional original se preserva intacto y la iniciativa nula no es alterada
        var state = manager.GetState(partyId)!;
        state.Status.Should().Be(LiveCombatStatus.Active);
        state.Combatants.Should().HaveCount(3);

        state.Combatants[0].Name.Should().Be("No Initiative Rogue");
        state.Combatants[0].Initiative.Should().BeNull();
        state.Combatants[0].SortOrder.Should().Be(0);

        state.Combatants[1].Name.Should().Be("High Initiative Wizard");
        state.Combatants[1].Initiative.Should().Be(22);
        state.Combatants[1].SortOrder.Should().Be(1);

        state.Combatants[2].Name.Should().Be("Mid Initiative Fighter");
        state.Combatants[2].Initiative.Should().Be(14);
        state.Combatants[2].SortOrder.Should().Be(2);

        state.GetCurrentCombatant()!.Name.Should().Be("No Initiative Rogue");
    }

    [Fact]
    public async Task LiveCombatHub_ReorderCombatant_BroadcastsToBothDmAndPlayerGroups()
    {
        // Arrange
        var partyId = Guid.NewGuid();
        var manager = new LiveCombatManager();

        var c0 = new LiveCombatant(Guid.NewGuid(), "Alpha", 20, 10, true);
        var c1 = new LiveCombatant(Guid.NewGuid(), "Beta", 20, 10, true);
        manager.InitializeCombat(partyId, "Party", new[] { c0, c1 });

        var dmGroupProxy = Substitute.For<IClientProxy>();
        var playerGroupProxy = Substitute.For<IClientProxy>();

        var hubClients = Substitute.For<IHubCallerClients>();
        hubClients.Group(LiveCombatHub.GetDmGroupName(partyId)).Returns(dmGroupProxy);
        hubClients.Group(LiveCombatHub.GetPlayerGroupName(partyId)).Returns(playerGroupProxy);

        var context = Substitute.For<HubCallerContext>();
        context.Items.Returns(new Dictionary<object, object?> { ["PartyId"] = partyId.ToString() });

        var hub = new LiveCombatHub(manager)
        {
            Clients = hubClients,
            Context = context
        };

        // Act: Invocación con sobrecarga (combatantId, newIndex)
        await hub.ReorderCombatant(c0.Id.ToString(), 1);

        // Assert: Se envió la actualización de estado a ambos grupos
        await dmGroupProxy.Received(1).SendCoreAsync(
            "CombatStateUpdated",
            Arg.Any<object?[]>(),
            default);

        await playerGroupProxy.Received(1).SendCoreAsync(
            "CombatStateUpdated",
            Arg.Any<object?[]>(),
            default);

        var state = manager.GetState(partyId)!;
        state.Combatants[0].Id.Should().Be(c1.Id);
        state.Combatants[1].Id.Should().Be(c0.Id);
    }

    [Fact]
    public async Task ForceAddPartyMemberToCombat_WhenCalledByDm_AssignsNullInitiativeAndAppendsToEndOfSortOrder()
    {
        // Arrange
        var partyId = Guid.NewGuid();
        var playerUserId = Guid.NewGuid();
        var manager = new LiveCombatManager();

        var orc1 = new LiveCombatant(Guid.NewGuid(), "Orco 1", maxHp: 30, armorClass: 13, isPlayerCharacter: false, isVisibleToPlayers: true, initiative: 15) { SortOrder = 0 };
        var orc2 = new LiveCombatant(Guid.NewGuid(), "Orco 2", maxHp: 30, armorClass: 13, isPlayerCharacter: false, isVisibleToPlayers: true, initiative: 12) { SortOrder = 1 };

        manager.InitializeCombat(partyId, "La Compañía del Dragón", new List<LiveCombatant> { orc1, orc2 });

        var dmGroupProxy = Substitute.For<IClientProxy>();
        var playerGroupProxy = Substitute.For<IClientProxy>();

        var hubClients = Substitute.For<IHubCallerClients>();
        hubClients.Group(LiveCombatHub.GetDmGroupName(partyId)).Returns(dmGroupProxy);
        hubClients.Group(LiveCombatHub.GetPlayerGroupName(partyId)).Returns(playerGroupProxy);

        var hub = new LiveCombatHub(manager)
        {
            Clients = hubClients
        };

        // Act
        await hub.ForceAddPartyMemberToCombat(partyId.ToString(), playerUserId.ToString());

        // Assert: El combatiente se agregó en memoria con iniciativa null y al final del SortOrder
        var state = manager.GetState(partyId);
        state.Should().NotBeNull();
        state!.Combatants.Should().HaveCount(3);

        var addedPlayer = state.Combatants[2];
        addedPlayer.AssignedPlayerUserId.Should().Be(playerUserId);
        addedPlayer.IsPlayerCharacter.Should().BeTrue();
        addedPlayer.Initiative.Should().BeNull("el jugador debe agregarse con iniciativa nula para que pueda ingresarla voluntariamente o el DM posicionarlo");
        addedPlayer.SortOrder.Should().Be(2, "debe posicionarse al final de la lista existente");

        // Assert: Se emitió el broadcast CombatStateUpdated a ambos grupos
        await dmGroupProxy.Received(1).SendCoreAsync("CombatStateUpdated", Arg.Any<object?[]>(), default);
        await playerGroupProxy.Received(1).SendCoreAsync("CombatStateUpdated", Arg.Any<object?[]>(), default);
    }

    [Fact]
    public async Task JoinEncounter_WhenPlayerJoins_AddsCombatantToStateAndBroadcastsUpdate()
    {
        // Arrange
        var partyId = Guid.NewGuid();
        var manager = new LiveCombatManager();
        manager.InitializeCombat(partyId, "La Compañía del Dragón", new List<LiveCombatant>());

        var dmGroupProxy = Substitute.For<IClientProxy>();
        var playerGroupProxy = Substitute.For<IClientProxy>();

        var hubClients = Substitute.For<IHubCallerClients>();
        hubClients.Group(LiveCombatHub.GetDmGroupName(partyId)).Returns(dmGroupProxy);
        hubClients.Group(LiveCombatHub.GetPlayerGroupName(partyId)).Returns(playerGroupProxy);

        var playerUserId = Guid.NewGuid();
        var claims = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
                new[]
                {
                    new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, playerUserId.ToString()),
                    new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, "Gimli")
                },
                "TestAuth"));

        var context = Substitute.For<HubCallerContext>();
        context.User.Returns(claims);

        var hub = new LiveCombatHub(manager)
        {
            Clients = hubClients,
            Context = context
        };

        var playerDto = new LiveCombatantPlayerDto(
            Id: Guid.NewGuid(),
            Name: "Gimli",
            Initiative: 14,
            IsPlayerCharacter: true,
            CurrentHp: 35,
            MaxHp: 35,
            HealthStatus: "Healthy",
            Conditions: null
        );

        // Act
        await hub.JoinEncounter(partyId.ToString(), playerDto);

        // Assert: El combatiente se agregó en memoria
        var state = manager.GetState(partyId);
        state.Should().NotBeNull();
        state!.Combatants.Should().ContainSingle(c => c.Name == "Gimli" && c.Initiative == 14 && c.IsPlayerCharacter);

        // Assert: Se emitió la actualización a ambos grupos
        await dmGroupProxy.Received(1).SendCoreAsync("CombatStateUpdated", Arg.Any<object?[]>(), default);
        await playerGroupProxy.Received(1).SendCoreAsync("CombatStateUpdated", Arg.Any<object?[]>(), default);
    }

    [Fact]
    public async Task UpdateCombatantInitiative_WhenCalledByPlayerForOtherPlayerCombatant_IsRejectedAndDoesNotUpdate()
    {
        // Arrange
        var partyId = Guid.NewGuid();
        var manager = new LiveCombatManager();

        var playerAUserId = Guid.NewGuid();
        var playerBUserId = Guid.NewGuid();

        var combatantA = new LiveCombatant(Guid.NewGuid(), "Legolas", 30, 15, true, assignedPlayerUserId: playerAUserId, initiative: 10);
        var combatantB = new LiveCombatant(Guid.NewGuid(), "Gimli", 40, 14, true, assignedPlayerUserId: playerBUserId, initiative: 8);

        manager.InitializeCombat(partyId, "La Compañía", new[] { combatantA, combatantB });

        var dmGroupProxy = Substitute.For<IClientProxy>();
        var playerGroupProxy = Substitute.For<IClientProxy>();
        var hubClients = Substitute.For<IHubCallerClients>();
        hubClients.Group(LiveCombatHub.GetDmGroupName(partyId)).Returns(dmGroupProxy);
        hubClients.Group(LiveCombatHub.GetPlayerGroupName(partyId)).Returns(playerGroupProxy);

        var claims = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
                new[] { new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, playerAUserId.ToString()) },
                "TestAuth"));

        var context = Substitute.For<HubCallerContext>();
        context.User.Returns(claims);
        context.Items.Returns(new Dictionary<object, object?> { ["PartyId"] = partyId, ["Role"] = "Player" });

        var hub = new LiveCombatHub(manager)
        {
            Clients = hubClients,
            Context = context
        };

        // Act: Jugador A intenta cambiar la iniciativa del combatiente del Jugador B a 99
        await hub.UpdateCombatantInitiative(partyId.ToString(), combatantB.Id.ToString(), 99);

        // Assert: La iniciativa de B permanece inalterada en 8
        var state = manager.GetState(partyId)!;
        state.Combatants.First(c => c.Id == combatantB.Id).Initiative.Should().Be(8);

        // No se debe emitir broadcast debido al rechazo por falta de autorización
        await dmGroupProxy.DidNotReceive().SendCoreAsync("CombatStateUpdated", Arg.Any<object?[]>(), default);
        await playerGroupProxy.DidNotReceive().SendCoreAsync("CombatStateUpdated", Arg.Any<object?[]>(), default);
    }

    [Fact]
    public async Task UpdateCombatantInitiative_WhenCalledByPlayerForOwnCombatant_UpdatesInitiativeAndBroadcasts()
    {
        // Arrange
        var partyId = Guid.NewGuid();
        var manager = new LiveCombatManager();

        var playerAUserId = Guid.NewGuid();
        var combatantA = new LiveCombatant(Guid.NewGuid(), "Legolas", 30, 15, true, assignedPlayerUserId: playerAUserId, initiative: null);

        manager.InitializeCombat(partyId, "La Compañía", new[] { combatantA });

        var dmGroupProxy = Substitute.For<IClientProxy>();
        var playerGroupProxy = Substitute.For<IClientProxy>();
        var hubClients = Substitute.For<IHubCallerClients>();
        hubClients.Group(LiveCombatHub.GetDmGroupName(partyId)).Returns(dmGroupProxy);
        hubClients.Group(LiveCombatHub.GetPlayerGroupName(partyId)).Returns(playerGroupProxy);

        var claims = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
                new[] { new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, playerAUserId.ToString()) },
                "TestAuth"));

        var context = Substitute.For<HubCallerContext>();
        context.User.Returns(claims);
        context.Items.Returns(new Dictionary<object, object?> { ["PartyId"] = partyId, ["Role"] = "Player" });

        var hub = new LiveCombatHub(manager)
        {
            Clients = hubClients,
            Context = context
        };

        // Act: Jugador A actualiza su propia iniciativa a 18
        await hub.UpdateCombatantInitiative(partyId.ToString(), combatantA.Id.ToString(), 18);

        // Assert: Su iniciativa se actualiza correctamente
        var state = manager.GetState(partyId)!;
        state.Combatants.First(c => c.Id == combatantA.Id).Initiative.Should().Be(18);

        // Se emite el broadcast a ambos grupos
        await dmGroupProxy.Received(1).SendCoreAsync("CombatStateUpdated", Arg.Any<object?[]>(), default);
        await playerGroupProxy.Received(1).SendCoreAsync("CombatStateUpdated", Arg.Any<object?[]>(), default);
    }

    [Fact]
    public async Task UpdateCombatantInitiative_WhenCalledByDm_UpdatesAnyCombatantAndBroadcasts()
    {
        // Arrange
        var partyId = Guid.NewGuid();
        var manager = new LiveCombatManager();

        var combatantMonster = new LiveCombatant(Guid.NewGuid(), "Orco Jefe", 30, 13, false, initiative: null);
        manager.InitializeCombat(partyId, "La Compañía", new[] { combatantMonster });

        var dmGroupProxy = Substitute.For<IClientProxy>();
        var playerGroupProxy = Substitute.For<IClientProxy>();
        var hubClients = Substitute.For<IHubCallerClients>();
        hubClients.Group(LiveCombatHub.GetDmGroupName(partyId)).Returns(dmGroupProxy);
        hubClients.Group(LiveCombatHub.GetPlayerGroupName(partyId)).Returns(playerGroupProxy);

        var context = Substitute.For<HubCallerContext>();
        context.Items.Returns(new Dictionary<object, object?> { ["PartyId"] = partyId, ["Role"] = "DungeonMaster" });

        var hub = new LiveCombatHub(manager)
        {
            Clients = hubClients,
            Context = context
        };

        // Act: DM actualiza iniciativa del monstruo a 15
        await hub.UpdateCombatantInitiative(partyId.ToString(), combatantMonster.Id.ToString(), 15);

        // Assert
        var state = manager.GetState(partyId)!;
        state.Combatants.First(c => c.Id == combatantMonster.Id).Initiative.Should().Be(15);

        await dmGroupProxy.Received(1).SendCoreAsync("CombatStateUpdated", Arg.Any<object?[]>(), default);
        await playerGroupProxy.Received(1).SendCoreAsync("CombatStateUpdated", Arg.Any<object?[]>(), default);
    }

    [Fact]
    public async Task RemoveCombatant_WhenCalledByPlayer_IsRejectedAndDoesNotRemove()
    {
        // Arrange
        var partyId = Guid.NewGuid();
        var manager = new LiveCombatManager();

        var c0 = new LiveCombatant(Guid.NewGuid(), "Legolas", 30, 15, true) { SortOrder = 0 };
        var c1 = new LiveCombatant(Guid.NewGuid(), "Orco", 20, 13, false) { SortOrder = 1 };
        manager.InitializeCombat(partyId, "La Compañía", new[] { c0, c1 });

        var dmGroupProxy = Substitute.For<IClientProxy>();
        var playerGroupProxy = Substitute.For<IClientProxy>();
        var hubClients = Substitute.For<IHubCallerClients>();
        hubClients.Group(LiveCombatHub.GetDmGroupName(partyId)).Returns(dmGroupProxy);
        hubClients.Group(LiveCombatHub.GetPlayerGroupName(partyId)).Returns(playerGroupProxy);

        var context = Substitute.For<HubCallerContext>();
        context.Items.Returns(new Dictionary<object, object?> { ["PartyId"] = partyId, ["Role"] = "Player" });

        var hub = new LiveCombatHub(manager)
        {
            Clients = hubClients,
            Context = context
        };

        // Act: Jugador intenta eliminar al Orco
        await hub.RemoveCombatant(partyId.ToString(), c1.Id.ToString());

        // Assert: El combatiente NO se eliminó
        var state = manager.GetState(partyId)!;
        state.Combatants.Should().HaveCount(2);

        await dmGroupProxy.DidNotReceive().SendCoreAsync("CombatStateUpdated", Arg.Any<object?[]>(), default);
        await playerGroupProxy.DidNotReceive().SendCoreAsync("CombatStateUpdated", Arg.Any<object?[]>(), default);
    }

    [Fact]
    public async Task RemoveCombatant_WhenCalledByDm_RemovesCombatantRecalculatesSortOrderAndBroadcasts()
    {
        // Arrange
        var partyId = Guid.NewGuid();
        var manager = new LiveCombatManager();

        var c0 = new LiveCombatant(Guid.NewGuid(), "Legolas", 30, 15, true) { SortOrder = 0 };
        var c1 = new LiveCombatant(Guid.NewGuid(), "Orco 1", 20, 13, false) { SortOrder = 1 };
        var c2 = new LiveCombatant(Guid.NewGuid(), "Orco 2", 20, 13, false) { SortOrder = 2 };
        manager.InitializeCombat(partyId, "La Compañía", new[] { c0, c1, c2 });

        var dmGroupProxy = Substitute.For<IClientProxy>();
        var playerGroupProxy = Substitute.For<IClientProxy>();
        var hubClients = Substitute.For<IHubCallerClients>();
        hubClients.Group(LiveCombatHub.GetDmGroupName(partyId)).Returns(dmGroupProxy);
        hubClients.Group(LiveCombatHub.GetPlayerGroupName(partyId)).Returns(playerGroupProxy);

        var context = Substitute.For<HubCallerContext>();
        context.Items.Returns(new Dictionary<object, object?> { ["PartyId"] = partyId, ["Role"] = "DungeonMaster" });

        var hub = new LiveCombatHub(manager)
        {
            Clients = hubClients,
            Context = context
        };

        // Act: DM elimina Orco 1 (índice 1)
        await hub.RemoveCombatant(partyId.ToString(), c1.Id.ToString());

        // Assert: Quedan 2 combatientes y el SortOrder se recalculó (0 y 1)
        var state = manager.GetState(partyId)!;
        state.Combatants.Should().HaveCount(2);
        state.Combatants[0].Id.Should().Be(c0.Id);
        state.Combatants[0].SortOrder.Should().Be(0);
        state.Combatants[1].Id.Should().Be(c2.Id);
        state.Combatants[1].SortOrder.Should().Be(1);

        // Se emite el broadcast
        await dmGroupProxy.Received(1).SendCoreAsync("CombatStateUpdated", Arg.Any<object?[]>(), default);
        await playerGroupProxy.Received(1).SendCoreAsync("CombatStateUpdated", Arg.Any<object?[]>(), default);
    }
}

