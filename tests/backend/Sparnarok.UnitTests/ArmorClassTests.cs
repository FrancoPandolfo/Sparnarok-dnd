using FluentAssertions;
using Sparnarok.Api.Domain.ValueObjects;
using Xunit;

namespace Sparnarok.UnitTests;

public class ArmorClassTests
{
    [Fact]
    public void CalculateTotal_Unarmored_AddsDexterityModifierToBase10()
    {
        // Arrange
        var ac = ArmorClass.Unarmored;

        // Act & Assert
        ac.CalculateTotal(dexterityModifier: 3).Should().Be(13);
        ac.CalculateTotal(dexterityModifier: -1).Should().Be(9);
    }

    [Fact]
    public void CalculateTotal_WithShieldAndMaxDexterityCap_RespectsMaxDexLimit()
    {
        // Arrange: Medium armor (Base 14, Shield +2, Max Dex +2)
        var ac = new ArmorClass(baseValue: 14, shieldBonus: 2, allowsDexterityModifier: true, maxDexterityModifier: 2);

        // Act & Assert
        // With Dex modifier +4, it must cap at +2 -> 14 + 2 (shield) + 2 (capped dex) = 18
        ac.CalculateTotal(dexterityModifier: 4).Should().Be(18);

        // With Dex modifier +1, it uses +1 -> 14 + 2 + 1 = 17
        ac.CalculateTotal(dexterityModifier: 1).Should().Be(17);
    }
}
