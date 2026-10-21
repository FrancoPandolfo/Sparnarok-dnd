using FluentAssertions;
using Sparnarok.Api.Domain.Services.Dice;
using Sparnarok.Api.Domain.Services.Rng;
using Xunit;

namespace Sparnarok.UnitTests;

/// <summary>
/// Pruebas unitarias para los generadores de números aleatorios (RNG) y el evaluador de expresiones de dados de D&amp;D 5e.
/// </summary>
public class DiceRollerTests
{
    [Fact]
    public void DeterministicRandomProvider_WithFixedSeed_ProducesRepeatableSequence()
    {
        // Arrange
        var rng1 = new DeterministicRandomProvider(seed: 42);
        var rng2 = new DeterministicRandomProvider(seed: 42);

        // Act & Assert
        for (int i = 0; i < 10; i++)
        {
            rng1.RollDie(20).Should().Be(rng2.RollDie(20));
        }
    }

    [Fact]
    public void DeterministicRandomProvider_WithPredefinedQueue_YieldsExactValues()
    {
        // Arrange
        var expectedRolls = new[] { 20, 1, 15, 8, 4 };
        var rng = new DeterministicRandomProvider(expectedRolls);

        // Act & Assert
        foreach (var expected in expectedRolls)
        {
            rng.RollDie(20).Should().Be(expected);
        }
    }

    [Theory]
    [InlineData("1d8", 8, 8, 0, 8)]
    [InlineData("2d6", 6, 12, 0, 12)]
    [InlineData("1d4+2", 4, 4, 2, 6)]
    [InlineData("1d6-1", 6, 6, -1, 5)]
    [InlineData("1", 0, 0, 1, 1)]
    public void Roll_StandardFormulas_CalculatesExpectedTotal(
        string formula,
        int dieMax,
        int expectedDiceTotal,
        int expectedModifier,
        int expectedTotal)
    {
        // Arrange: RNG siempre retorna el valor máximo para el dado
        var rng = new DeterministicRandomProvider(new[] { dieMax, dieMax });
        var roller = new DiceRoller(rng);

        // Act
        var result = roller.Roll(formula);

        // Assert
        result.DiceTotal.Should().Be(expectedDiceTotal);
        result.Modifier.Should().Be(expectedModifier);
        result.Total.Should().Be(expectedTotal);
        result.IsCritical.Should().BeFalse();
    }

    [Fact]
    public void Roll_CriticalHit_DoublesDiceCountPreservingModifier()
    {
        // Arrange: Para 1d8+3 en crítico, se deben tirar 2 dados d8
        // Simular tiradas de [8, 7]
        var rng = new DeterministicRandomProvider(new[] { 8, 7 });
        var roller = new DiceRoller(rng);

        // Act: 1d8 con +3 de característica en impacto crítico
        var result = roller.Roll("1d8", additionalModifier: 3, isCritical: true);

        // Assert
        result.IsCritical.Should().BeTrue();
        result.IndividualRolls.Should().HaveCount(2);
        result.IndividualRolls.Should().ContainInOrder(8, 7);
        result.DiceTotal.Should().Be(15);
        result.Modifier.Should().Be(3);
        result.Total.Should().Be(18); // 8 + 7 + 3
    }

    [Fact]
    public void Roll_FlatDamage_InCriticalHit_DoesNotDuplicateFlatValue()
    {
        // Arrange: Cerbatana (Blowgun) tiene daño plano "1"
        var rng = new DeterministicRandomProvider(new[] { 1 });
        var roller = new DiceRoller(rng);

        // Act
        var result = roller.Roll("1", additionalModifier: 2, isCritical: true);

        // Assert
        result.IndividualRolls.Should().BeEmpty();
        result.DiceTotal.Should().Be(0);
        result.Modifier.Should().Be(3); // 1 plano de la fórmula + 2 adicional
        result.Total.Should().Be(3);
    }

    [Fact]
    public void Roll_WhenResultIsNegative_TruncatesToZero()
    {
        // Arrange: 1d4 con penalizador -10
        var rng = new DeterministicRandomProvider(new[] { 1 });
        var roller = new DiceRoller(rng);

        // Act
        var result = roller.Roll("1d4", additionalModifier: -10);

        // Assert
        result.Total.Should().Be(0);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Roll_WithEmptyFormula_ThrowsArgumentException(string invalid)
    {
        var roller = new DiceRoller(new DefaultRandomProvider());
        var act = () => roller.Roll(invalid);
        act.Should().Throw<ArgumentException>();
    }
}
