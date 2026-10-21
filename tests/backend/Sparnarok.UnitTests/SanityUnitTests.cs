using FluentAssertions;
using Xunit;

namespace Sparnarok.UnitTests;

public class SanityUnitTests
{
    [Fact]
    public void CoreEngine_ShouldPassBaselineAssertion()
    {
        // Arrange
        const int standardDifficultyClass = 15;
        const int abilityModifier = 3;
        const int proficiencyBonus = 2;

        // Act
        var passiveScore = 10 + abilityModifier + proficiencyBonus;

        // Assert
        passiveScore.Should().Be(standardDifficultyClass);
    }
}
