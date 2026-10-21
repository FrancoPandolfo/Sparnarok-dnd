using FluentAssertions;
using Sparnarok.Api.Domain.Enums;
using Sparnarok.Api.Domain.ValueObjects;
using Xunit;

namespace Sparnarok.UnitTests;

public class AbilityScoresTests
{
    [Theory]
    [InlineData(1, -5)]
    [InlineData(2, -4)]
    [InlineData(3, -4)]
    [InlineData(4, -3)]
    [InlineData(5, -3)]
    [InlineData(6, -2)]
    [InlineData(7, -2)]
    [InlineData(8, -1)]
    [InlineData(9, -1)]
    [InlineData(10, 0)]
    [InlineData(11, 0)]
    [InlineData(12, 1)]
    [InlineData(13, 1)]
    [InlineData(14, 2)]
    [InlineData(15, 2)]
    [InlineData(16, 3)]
    [InlineData(18, 4)]
    [InlineData(20, 5)]
    [InlineData(30, 10)]
    public void CalculateModifier_ComputesExactDndCanonicalModifier(int score, int expectedModifier)
    {
        // Act
        var modifier = AbilityScores.CalculateModifier(score);

        // Assert
        modifier.Should().Be(expectedModifier);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(31)]
    [InlineData(100)]
    public void CalculateModifier_WithOutOfRangeScore_ThrowsArgumentOutOfRangeException(int invalidScore)
    {
        // Act
        var act = () => AbilityScores.CalculateModifier(invalidScore);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_WithValidScores_AssignsAndReturnsModifiersCorrectly()
    {
        // Arrange
        var scores = new AbilityScores(
            strength: 18,
            dexterity: 14,
            constitution: 16,
            intelligence: 10,
            wisdom: 12,
            charisma: 8);

        // Assert
        scores.GetModifier(AbilityScoreType.Strength).Should().Be(4);
        scores.GetModifier(AbilityScoreType.Dexterity).Should().Be(2);
        scores.GetModifier(AbilityScoreType.Constitution).Should().Be(3);
        scores.GetModifier(AbilityScoreType.Intelligence).Should().Be(0);
        scores.GetModifier(AbilityScoreType.Wisdom).Should().Be(1);
        scores.GetModifier(AbilityScoreType.Charisma).Should().Be(-1);
    }
}
