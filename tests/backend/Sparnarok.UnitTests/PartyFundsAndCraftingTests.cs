using FluentAssertions;
using Sparnarok.Api.Domain.Entities;
using Xunit;

namespace Sparnarok.UnitTests;

/// <summary>
/// Pruebas unitarias para las reglas de negocio de Party Funds (monedas)
/// y la entidad CraftingQueueItem (Downtime Crafting).
/// </summary>
public class PartyFundsAndCraftingTests
{
    [Fact]
    public void Party_UpdateFunds_CalculatesTotalGoldEquivalentCorrectly()
    {
        // Arrange
        var party = new Party(Guid.NewGuid(), "Aventureros", Guid.NewGuid());

        // 100 PC = 1 GP
        // 50 SP = 5 GP
        // 10 EP = 5 GP
        // 50 GP = 50 GP
        // 2 PP = 20 GP
        // Total esperado: 1 + 5 + 5 + 50 + 20 = 81 GP
        party.UpdateFunds(cp: 100, sp: 50, ep: 10, gp: 50, pp: 2);

        // Assert
        party.CopperPieces.Should().Be(100);
        party.SilverPieces.Should().Be(50);
        party.ElectrumPieces.Should().Be(10);
        party.GoldPieces.Should().Be(50);
        party.PlatinumPieces.Should().Be(2);
        party.TotalGoldEquivalent.Should().Be(81m);
    }

    [Fact]
    public void Party_DeductGold_WhenSufficientGold_DeductsAndRecalculates()
    {
        // Arrange
        var party = new Party(Guid.NewGuid(), "Aventureros", Guid.NewGuid());
        party.UpdateFunds(cp: 0, sp: 0, ep: 0, gp: 100, pp: 0);

        // Act
        party.DeductGold(40);

        // Assert
        party.GoldPieces.Should().Be(60);
        party.TotalGoldEquivalent.Should().Be(60m);
    }

    [Fact]
    public void Party_DeductGold_WhenInsufficientGold_ThrowsInvalidOperationException()
    {
        // Arrange
        var party = new Party(Guid.NewGuid(), "Aventureros", Guid.NewGuid());
        party.UpdateFunds(cp: 0, sp: 0, ep: 0, gp: 25, pp: 0);

        // Act
        var act = () => party.DeductGold(50);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Fondos insuficientes*");
    }

    [Fact]
    public void CraftingQueueItem_AdvanceDay_ReducesDaysRemainingAndMarksCompletedAtZero()
    {
        // Arrange
        var partyId = Guid.NewGuid();
        var item = new CraftingQueueItem(
            Guid.NewGuid(),
            partyId,
            "Potion of Healing",
            "Potion",
            "Common",
            goldCost: 25,
            daysRequired: 2,
            weight: 0.5m,
            description: "Restaura 2d4+2 PG",
            isOfficialSrd: true,
            referenceIndex: "potion-of-healing",
            crafterName: "Clérigo"
        );

        item.Status.Should().Be("InProgress");
        item.DaysRemaining.Should().Be(2);

        // Act 1: Avanzar primer día
        var completedDay1 = item.AdvanceDay();

        // Assert 1
        completedDay1.Should().BeFalse();
        item.DaysRemaining.Should().Be(1);
        item.Status.Should().Be("InProgress");

        // Act 2: Avanzar segundo día (alcanza 0)
        var completedDay2 = item.AdvanceDay();

        // Assert 2
        completedDay2.Should().BeTrue();
        item.DaysRemaining.Should().Be(0);
        item.Status.Should().Be("Completed");

        // Act 3: Avanzar cuando ya está completado
        var completedDay3 = item.AdvanceDay();
        completedDay3.Should().BeFalse();
    }

    [Fact]
    public void CraftingQueueItem_ToStashItem_CreatesValidStashItem()
    {
        // Arrange
        var partyId = Guid.NewGuid();
        var crafting = new CraftingQueueItem(
            Guid.NewGuid(),
            partyId,
            "Cloak of Protection",
            "Wondrous Item",
            "Uncommon",
            goldCost: 200,
            daysRequired: 10,
            weight: 3.0m,
            description: "+1 a la CA y tiros de salvación",
            isOfficialSrd: true,
            referenceIndex: "cloak-of-protection",
            crafterName: "Artífice"
        );

        // Act
        var stashItem = crafting.ToStashItem();

        // Assert
        stashItem.Should().NotBeNull();
        stashItem.PartyId.Should().Be(partyId);
        stashItem.Name.Should().Be("Cloak of Protection");
        stashItem.Quantity.Should().Be(1);
        stashItem.Weight.Should().Be(3.0m);
        stashItem.Rarity.Should().Be("Uncommon");
        stashItem.ItemType.Should().Be("Wondrous Item");
        stashItem.IsOfficialSrd.Should().BeTrue();
        stashItem.ReferenceIndex.Should().Be("cloak-of-protection");
    }

    [Fact]
    public void CraftingQueueItem_Constructor_ThrowsWhenInvalidValues()
    {
        // Nombre vacío
        var actEmptyName = () => new CraftingQueueItem(
            Guid.NewGuid(), Guid.NewGuid(), "", "Potion", "Common", 50, 2, 1, "", false, null, null);
        actEmptyName.Should().Throw<ArgumentException>();

        // Días requeridos < 1
        var actInvalidDays = () => new CraftingQueueItem(
            Guid.NewGuid(), Guid.NewGuid(), "Item", "Potion", "Common", 50, 0, 1, "", false, null, null);
        actInvalidDays.Should().Throw<ArgumentOutOfRangeException>();

        // Costo negativo
        var actNegativeCost = () => new CraftingQueueItem(
            Guid.NewGuid(), Guid.NewGuid(), "Item", "Potion", "Common", -10, 2, 1, "", false, null, null);
        actNegativeCost.Should().Throw<ArgumentOutOfRangeException>();
    }
}
