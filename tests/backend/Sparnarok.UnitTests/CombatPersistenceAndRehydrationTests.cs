using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sparnarok.Api.Application.DTOs;
using Sparnarok.Api.Application.Interfaces;
using Sparnarok.Api.Domain.Entities;
using Sparnarok.Api.Domain.Models;
using Sparnarok.Api.Infrastructure.Hubs;
using Sparnarok.Api.Infrastructure.Persistence;
using Sparnarok.Api.Infrastructure.Repositories;
using Sparnarok.Api.Infrastructure.Services;
using Xunit;

namespace Sparnarok.UnitTests;

/// <summary>
/// Pruebas unitarias para la persistencia y rehidratación del estado de combate en tiempo real
/// y la gestión de sesiones efímeras sin Party en SignalR (LiveCombatHub y LiveCombatManager).
/// </summary>
public class CombatPersistenceAndRehydrationTests
{
    private static SparnarokDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<SparnarokDbContext>()
            .UseInMemoryDatabase(databaseName: $"Sparnarok_CombatPersistence_{Guid.NewGuid():N}")
            .Options;
        return new SparnarokDbContext(options);
    }

    [Fact]
    public async Task CombatState_SaveToDatabase_ClearMemory_AndRehydrate_RestoresStateCompletely()
    {
        // -------------------------------------------------------------
        // 1. Arrange: Base de datos y servicios
        // -------------------------------------------------------------
        using var dbContext = CreateInMemoryDbContext();
        var ownerId = Guid.NewGuid();
        var party = new Party(Guid.NewGuid(), "Los Custodios de Gondor", ownerId);
        dbContext.Parties.Add(party);
        await dbContext.SaveChangesAsync();

        var combatManager = new LiveCombatManager();

        // Configurar DI ScopeFactory para que el Hub acceda al DbContext
        var services = new ServiceCollection();
        services.AddScoped<IPartyRepository>(_ => new PartyRepository(dbContext));
        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        var callerProxy = Substitute.For<ISingleClientProxy>();
        var hubClients = Substitute.For<IHubCallerClients>();
        hubClients.Caller.Returns(callerProxy);

        var groups = Substitute.For<IGroupManager>();
        var hubContext = Substitute.For<HubCallerContext>();
        hubContext.ConnectionId.Returns("dm-conn-1");
        hubContext.Items.Returns(new Dictionary<object, object?>());

        var hub = new LiveCombatHub(combatManager, scopeFactory, NullLogger<LiveCombatHub>.Instance)
        {
            Clients = hubClients,
            Groups = groups,
            Context = hubContext
        };

        // -------------------------------------------------------------
        // 2. Act: Inicializar combate, modificar HP, avanzar turno y ronda
        // -------------------------------------------------------------
        var hero = new LiveCombatant(Guid.NewGuid(), "Aragorn", maxHp: 50, armorClass: 16, isPlayerCharacter: true, initiative: 18);
        var monster = new LiveCombatant(Guid.NewGuid(), "Troll de las Cavernas", maxHp: 84, armorClass: 15, isPlayerCharacter: false, isVisibleToPlayers: true, initiative: 8);

        await hub.InitializeCombat(party.Id.ToString(), new InitializeLiveCombatRequest(
            party.Name,
            new List<InitializeLiveCombatantRequest>
            {
                new(hero.Id, hero.Name, hero.MaxHp, hero.ArmorClass, hero.IsPlayerCharacter, hero.IsVisibleToPlayers, hero.AssignedPlayerUserId, hero.Initiative),
                new(monster.Id, monster.Name, monster.MaxHp, monster.ArmorClass, monster.IsPlayerCharacter, monster.IsVisibleToPlayers, monster.AssignedPlayerUserId, monster.Initiative),
            }
        ));

        // Iniciar combate formalmente (Ronda 1)
        await hub.StartCombat(party.Id.ToString());

        // Dañar al Troll en 24 HP
        await hub.ModifyHp(party.Id.ToString(), monster.Id, -24);

        // Pasar turno al Troll (Turno Index 1)
        await hub.NextTurn(party.Id.ToString());

        // Pasar turno nuevamente: completa ciclo y pasa a Ronda 2 (Turno Index 0)
        await hub.NextTurn(party.Id.ToString());

        // Verificar que el estado en DB fue persistido
        var persistedParty = await dbContext.Parties.AsNoTracking().FirstAsync(p => p.Id == party.Id);
        persistedParty.CurrentCombatState.Should().NotBeNullOrWhiteSpace();
        persistedParty.CurrentCombatState.Should().Contain("Aragorn");
        persistedParty.CurrentCombatState.Should().Contain("Troll de las Cavernas");

        // -------------------------------------------------------------
        // 3. Simular reinicio de servidor / limpieza de memoria RAM
        // -------------------------------------------------------------
        combatManager.ClearState(party.Id);
        combatManager.GetState(party.Id).Should().BeNull("La memoria debe estar vacía para simular pérdida de sesión");

        // -------------------------------------------------------------
        // 4. Rehidratar: El DM se reconecta (ej. F5 en navegador)
        // -------------------------------------------------------------
        LiveCombatDmProjectionDto? rehydratedProjection = null;
        await callerProxy.SendCoreAsync(
            "CombatStateUpdated",
            Arg.Do<object?[]>(args =>
            {
                rehydratedProjection = args[0] as LiveCombatDmProjectionDto;
            }),
            default);

        await hub.JoinCombat(party.Id.ToString(), "DungeonMaster");

        // -------------------------------------------------------------
        // 5. Assert: Verificar que la iniciativa, HPs, turno y ronda están intactos
        // -------------------------------------------------------------
        var restoredState = combatManager.GetState(party.Id);
        restoredState.Should().NotBeNull();
        restoredState!.Status.Should().Be(LiveCombatStatus.Active);
        restoredState.CurrentRound.Should().Be(2, "La ronda 2 debió ser rehidratada intacta");
        restoredState.CurrentTurnIndex.Should().Be(0, "El índice de turno debió ser restaurado");

        var restoredTroll = restoredState.Combatants.First(c => c.Id == monster.Id);
        restoredTroll.CurrentHp.Should().Be(60, "El Troll recibió 24 de daño de sus 84 HP máximos");
        restoredTroll.Initiative.Should().Be(8);

        var restoredAragorn = restoredState.Combatants.First(c => c.Id == hero.Id);
        restoredAragorn.CurrentHp.Should().Be(50);
        restoredAragorn.Initiative.Should().Be(18);

        rehydratedProjection.Should().NotBeNull();
        rehydratedProjection!.CurrentRound.Should().Be(2);
        rehydratedProjection.ActiveCombatantId.Should().Be(hero.Id);
        rehydratedProjection.Combatants.Should().HaveCount(2);
    }

    [Fact]
    public async Task QuickSessionMode_AllowsCombatWithoutDatabasePersistence()
    {
        // Arrange
        var combatManager = new LiveCombatManager();
        var hubClients = Substitute.For<IHubCallerClients>();
        var callerProxy = Substitute.For<ISingleClientProxy>();
        hubClients.Caller.Returns(callerProxy);

        var groups = Substitute.For<IGroupManager>();
        var hubContext = Substitute.For<HubCallerContext>();
        hubContext.ConnectionId.Returns("quick-dm-conn");
        hubContext.Items.Returns(new Dictionary<object, object?>());

        // No DB scope factory provided for quick mode
        var hub = new LiveCombatHub(combatManager, null, NullLogger<LiveCombatHub>.Instance)
        {
            Clients = hubClients,
            Groups = groups,
            Context = hubContext
        };

        // Act: Join combat with null / quick-session partyId
        await hub.JoinCombat(null, "DungeonMaster");

        // Add a quick combatant in hot mode
        await hub.AddQuickCombatant("quick-session", new AddQuickCombatantRequest(
            "Goblin Espía",
            MaxHp: 15,
            ArmorClass: 13,
            IsPlayerCharacter: false,
            IsVisibleToPlayers: true,
            Initiative: 12
        ));

        // Assert: Resolved to QuickSessionGuid in memory
        var quickState = combatManager.GetState(LiveCombatHub.QuickSessionGuid);
        quickState.Should().NotBeNull();
        quickState!.Combatants.Should().ContainSingle(c => c.Name == "Goblin Espía");
        quickState.Combatants[0].CurrentHp.Should().Be(15);
    }
}
