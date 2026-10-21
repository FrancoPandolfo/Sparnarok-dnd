using FluentAssertions;
using Sparnarok.Api.Domain.ValueObjects;
using Xunit;

namespace Sparnarok.UnitTests;

public class HitPointsTests
{
    [Fact]
    public void Constructor_InitializesWithCurrentEqualToMax()
    {
        // Act
        var hp = new HitPoints(50);

        // Assert
        hp.Max.Should().Be(50);
        hp.Current.Should().Be(50);
        hp.Temporary.Should().Be(0);
        hp.IsConscious.Should().BeTrue();
        hp.IsBloodied.Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Constructor_WithInvalidMax_ThrowsArgumentOutOfRangeException(int invalidMax)
    {
        var act = () => new HitPoints(invalidMax);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ApplyDamage_ReducesCurrentHpDirectly()
    {
        // Arrange
        var hp = new HitPoints(40);

        // Act
        var damagedHp = hp.ApplyDamage(15);

        // Assert
        damagedHp.Current.Should().Be(25);
        damagedHp.Max.Should().Be(40);
        damagedHp.IsBloodied.Should().BeFalse();
    }

    [Fact]
    public void ApplyDamage_WithTemporaryHp_TemporaryAbsorbsFirst()
    {
        // Arrange
        var hp = new HitPoints(30, current: 30, temporary: 10);

        // Act - Inflict 6 damage (fully absorbed by 10 temp HP)
        var afterFirstDamage = hp.ApplyDamage(6);

        // Assert
        afterFirstDamage.Temporary.Should().Be(4);
        afterFirstDamage.Current.Should().Be(30);

        // Act - Inflict another 8 damage (4 absorbed by temp, 4 from current)
        var afterSecondDamage = afterFirstDamage.ApplyDamage(8);

        // Assert
        afterSecondDamage.Temporary.Should().Be(0);
        afterSecondDamage.Current.Should().Be(26);
    }

    [Fact]
    public void ApplyDamage_ExcessDamageClampsAtZero()
    {
        // Arrange
        var hp = new HitPoints(20);

        // Act
        var result = hp.ApplyDamage(50);

        // Assert
        result.Current.Should().Be(0);
        result.IsConscious.Should().BeFalse();
        result.IsBloodied.Should().BeTrue();
    }

    [Fact]
    public void Heal_RestoresCurrentHpWithoutExceedingMax()
    {
        // Arrange
        var hp = new HitPoints(40, current: 15);

        // Act
        var healedHp = hp.Heal(20);

        // Assert
        healedHp.Current.Should().Be(35);

        // Act - Overhealing
        var overHealedHp = healedHp.Heal(50);

        // Assert
        overHealedHp.Current.Should().Be(40);
    }

    [Fact]
    public void ApplyTemporary_DoesNotStack_KeepsHighestValue()
    {
        // Arrange
        var hp = new HitPoints(30, current: 30, temporary: 10);

        // Act - Offering lower temporary HP (5)
        var firstResult = hp.ApplyTemporary(5);
        firstResult.Temporary.Should().Be(10);

        // Act - Offering higher temporary HP (15)
        var secondResult = hp.ApplyTemporary(15);
        secondResult.Temporary.Should().Be(15);
    }

    [Theory]
    [InlineData(40, 20, true)]   // Exactly 50% -> Bloodied
    [InlineData(40, 19, true)]   // Below 50% -> Bloodied
    [InlineData(40, 21, false)]  // Above 50% -> Not bloodied
    [InlineData(40, 0, true)]    // 0 HP -> Bloodied
    public void IsBloodied_DetectsFiftyPercentThresholdPerDnd2024Rules(int max, int current, bool expectedBloodied)
    {
        var hp = new HitPoints(max, current: current);
        hp.IsBloodied.Should().Be(expectedBloodied);
    }
}
