using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sparnarok.Api.Application.DTOs;
using Sparnarok.Api.Application.Interfaces;
using Sparnarok.Api.Domain.Entities;
using Sparnarok.Api.Domain.Enums;
using Sparnarok.Api.Domain.ValueObjects;
using Sparnarok.Api.Infrastructure.Persistence;
using Sparnarok.Api.Infrastructure.Services;
using Xunit;

namespace Sparnarok.UnitTests;

/// <summary>
/// Pruebas unitarias para el sistema de Lazy Ingestion, búsqueda proxy a Open5e,
/// parsing algebraico de damageFormula y asignación heurística de Weapon Masteries 2024.
/// </summary>
public class CreatureLazyIngestionTests
{
    private static SparnarokDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<SparnarokDbContext>()
            .UseInMemoryDatabase(databaseName: $"Sparnarok_LazyIngestion_{Guid.NewGuid()}")
            .Options;

        var context = new SparnarokDbContext(options);
        // Seed canonical masteries
        context.WeaponMasteries.AddRange(
            new WeaponMastery(MasteryProperty.Cleave, "Cleave", "Cleave desc", "Melee, Heavy", Guid.Parse("00000000-0000-0000-0000-000000000001")),
            new WeaponMastery(MasteryProperty.Graze, "Graze", "Graze desc", "Melee, Heavy", Guid.Parse("00000000-0000-0000-0000-000000000002")),
            new WeaponMastery(MasteryProperty.Nick, "Nick", "Nick desc", "Light", Guid.Parse("00000000-0000-0000-0000-000000000003")),
            new WeaponMastery(MasteryProperty.Push, "Push", "Push desc", "Heavy", Guid.Parse("00000000-0000-0000-0000-000000000004")),
            new WeaponMastery(MasteryProperty.Sap, "Sap", "Sap desc", "Versatile", Guid.Parse("00000000-0000-0000-0000-000000000005")),
            new WeaponMastery(MasteryProperty.Slow, "Slow", "Slow desc", "Ranged", Guid.Parse("00000000-0000-0000-0000-000000000006")),
            new WeaponMastery(MasteryProperty.Topple, "Topple", "Topple desc", "Heavy", Guid.Parse("00000000-0000-0000-0000-000000000007")),
            new WeaponMastery(MasteryProperty.Vex, "Vex", "Vex desc", "Finesse", Guid.Parse("00000000-0000-0000-0000-000000000008"))
        );
        context.SaveChanges();
        return context;
    }

    [Theory]
    [InlineData("Greataxe Strike", "Melee Weapon Attack with a greataxe", MasteryProperty.Cleave)]
    [InlineData("Greatsword Slash", "Hit with a two-handed greatsword", MasteryProperty.Graze)]
    [InlineData("Scimitar Slash", "Finesse slashing weapon", MasteryProperty.Nick)]
    [InlineData("Dagger Thrust", "Light dagger attack", MasteryProperty.Nick)]
    [InlineData("Morningstar Smash", "Hit with morningstar", MasteryProperty.Sap)]
    [InlineData("Longsword Attack", "Slashing with longsword", MasteryProperty.Sap)]
    [InlineData("Battleaxe Chop", "Versatile battleaxe swing", MasteryProperty.Topple)]
    [InlineData("Bite", "Fierce bite capable of tripping the target", MasteryProperty.Topple)]
    [InlineData("Gore", "Charges with heavy horns", MasteryProperty.Topple)]
    [InlineData("Claw", "Rips flesh with sharp claws", MasteryProperty.Vex)]
    [InlineData("Rapier Thrust", "Precise rapier piercing", MasteryProperty.Vex)]
    [InlineData("Warhammer Bash", "Crushes with heavy warhammer", MasteryProperty.Push)]
    [InlineData("Slam", "Brutal slam with fist or tail", MasteryProperty.Push)]
    [InlineData("Longbow Shot", "Ranged ammunition attack with longbow", MasteryProperty.Slow)]
    public void DetermineWeaponMastery_HeuristicallyIdentifiesMasteryProperty(
        string actionName, string description, MasteryProperty expectedMastery)
    {
        var result = Open5eSyncService.DetermineWeaponMastery(actionName, description);
        result.Should().Be(expectedMastery);
    }

    [Fact]
    public void MapToCreature_IsolatesDamageFormulaAndAssignsMasteriesCorrectly()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var mockClient = Substitute.For<IOpen5eBestiaryClient>();
        var syncService = new Open5eSyncService(mockClient, context, NullLogger<Open5eSyncService>.Instance);
        var masteries = context.WeaponMasteries.ToDictionary(m => m.Property);

        var monsterDto = new Open5eMonsterDto
        {
            Name = "Owlbear Alpha",
            Size = "Large",
            Type = "Monstrosity",
            ArmorClass = 13,
            HitPoints = 59,
            ChallengeRating = "3",
            Cr = 3.0,
            Actions = new List<Open5eActionDto>
            {
                new()
                {
                    Name = "Beak",
                    Desc = "Melee Weapon Attack: +7 to hit, reach 5 ft., one target. Hit: 10 (1d10 + 5) piercing damage.",
                    AttackBonus = 7,
                    DamageDice = null // Debería aislar "1d10+5" del texto descriptivo
                },
                new()
                {
                    Name = "Claws",
                    Desc = "Melee Weapon Attack: +7 to hit, reach 5 ft., one target. Hit: 14 (2d8 + 5) slashing damage.",
                    AttackBonus = 7,
                    DamageDice = "2d8" // Debería aislar "2d8+5" de la fórmula completa entre paréntesis
                }
            }
        };

        // Act
        var creature = syncService.MapToCreature(monsterDto, masteries);

        // Assert
        creature.Should().NotBeNull();
        creature.Name.Should().Be("Owlbear Alpha");
        creature.Actions.Should().HaveCount(2);

        var beak = creature.Actions.First(a => a.Name == "Beak");
        beak.DamageFormula.Should().Be("1d10+5");
        beak.DamageType.Should().Be(DamageType.Piercing);

        var claws = creature.Actions.First(a => a.Name == "Claws");
        claws.DamageFormula.Should().Be("2d8+5");
        claws.DamageType.Should().Be(DamageType.Slashing);
        claws.AssociatedMastery.Should().NotBeNull();
        claws.AssociatedMastery!.Property.Should().Be(MasteryProperty.Vex);

        creature.KnownMasteries.Should().Contain(m => m.Property == MasteryProperty.Vex);
    }

    [Fact]
    public async Task SearchCreaturesAsync_WhenLocalResultsAreInsufficient_FetchesFromOpen5eAndPersistsSilently()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var mockOpen5eClient = Substitute.For<IOpen5eBestiaryClient>();
        var syncService = new Open5eSyncService(mockOpen5eClient, context, NullLogger<Open5eSyncService>.Instance);
        var logger = NullLogger<CreatureSearchService>.Instance;

        // DB local vacía para término "Beholder"
        mockOpen5eClient
            .SearchMonstersAsync("Beholder", 15, Arg.Any<CancellationToken>())
            .Returns(new Open5eMonsterListResponse
            {
                Count = 1,
                Results = new List<Open5eMonsterDto>
                {
                    new()
                    {
                        Name = "Beholder Mage",
                        Size = "Large",
                        Type = "Aberration",
                        Alignment = "Lawful Evil",
                        ArmorClass = 18,
                        HitPoints = 180,
                        ChallengeRating = "13",
                        Cr = 13.0,
                        Actions = new List<Open5eActionDto>
                        {
                            new()
                            {
                                Name = "Bite",
                                Desc = "Melee Weapon Attack: +5 to hit, reach 5 ft., one target. Hit: 14 (4d6) piercing damage.",
                                AttackBonus = 5,
                                DamageDice = "4d6"
                            }
                        }
                    }
                }
            });

        var searchService = new CreatureSearchService(context, mockOpen5eClient, syncService, logger);

        // Act
        var results = await searchService.SearchCreaturesAsync("Beholder");

        // Assert
        results.Should().NotBeEmpty();
        results.Should().Contain(c => c.Name == "Beholder Mage");
        await mockOpen5eClient.Received(1).SearchMonstersAsync("Beholder", 15, Arg.Any<CancellationToken>());

        // Verificar persistencia silenciosa en la base de datos (PostgreSQL/InMemory)
        var persistedInDb = await context.Creatures
            .Include(c => c.Actions)
            .FirstOrDefaultAsync(c => c.Name == "Beholder Mage");

        persistedInDb.Should().NotBeNull();
        persistedInDb!.EffectiveArmorClass.Should().Be(18);
        persistedInDb.HitPoints.Max.Should().Be(180);
        persistedInDb.Actions.Should().HaveCount(1);
    }

    [Fact]
    public async Task SearchCreaturesAsync_WhenLocalResultsAreSufficient_DoesNotCallOpen5e()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();

        // Seed 4 creatures locally containing "Goblin"
        for (int i = 1; i <= 4; i++)
        {
            var goblin = new Creature(
                $"Goblin Warrior {i}",
                CreatureSize.Small,
                CreatureType.Humanoid,
                "Neutral Evil",
                0.25,
                30,
                new AbilityScores(8, 14, 10, 10, 8, 8),
                new HitPoints(7, 7),
                new ArmorClass(15, 0, false));
            context.Creatures.Add(goblin);
        }
        await context.SaveChangesAsync();

        var mockOpen5eClient = Substitute.For<IOpen5eBestiaryClient>();
        var syncService = new Open5eSyncService(mockOpen5eClient, context, NullLogger<Open5eSyncService>.Instance);
        var searchService = new CreatureSearchService(context, mockOpen5eClient, syncService, NullLogger<CreatureSearchService>.Instance);

        // Act
        var results = await searchService.SearchCreaturesAsync("Goblin");

        // Assert
        results.Should().HaveCount(4);
        await mockOpen5eClient.DidNotReceive().SearchMonstersAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }
}
