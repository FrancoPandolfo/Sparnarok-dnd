using FluentAssertions;
using Sparnarok.Api.Application.DTOs;
using Sparnarok.Api.Application.Validators;
using Sparnarok.Api.Domain.Enums;
using Xunit;

namespace Sparnarok.UnitTests;

public class CreatureValidatorTests
{
    private readonly CreateCreatureRequestValidator _validator = new();

    [Fact]
    public async Task Validate_WithValidRequest_PassesValidation()
    {
        // Arrange
        var request = new CreateCreatureRequestDto(
            Name: "Hill Giant",
            Size: CreatureSize.Huge,
            Type: CreatureType.Giant,
            Alignment: "Chaotic Evil",
            ChallengeRating: 5,
            SpeedFeet: 40,
            Strength: 21,
            Dexterity: 8,
            Constitution: 19,
            Intelligence: 5,
            Wisdom: 9,
            Charisma: 6,
            MaxHitPoints: 105,
            ArmorClassBase: 13);

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Validate_WithEmptyName_FailsValidation(string emptyName)
    {
        // Arrange
        var request = new CreateCreatureRequestDto(
            Name: emptyName,
            Size: CreatureSize.Medium,
            Type: CreatureType.Humanoid,
            Alignment: "Neutral",
            ChallengeRating: 1);

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateCreatureRequestDto.Name));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    [InlineData(-5)]
    public async Task Validate_WithInvalidAbilityScore_FailsValidation(int invalidScore)
    {
        // Arrange
        var request = new CreateCreatureRequestDto(
            Name: "Goblin",
            Size: CreatureSize.Small,
            Type: CreatureType.Humanoid,
            Alignment: "Neutral Evil",
            ChallengeRating: 0.25,
            Strength: invalidScore);

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateCreatureRequestDto.Strength));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(31)]
    public async Task Validate_WithInvalidChallengeRating_FailsValidation(double invalidCr)
    {
        // Arrange
        var request = new CreateCreatureRequestDto(
            Name: "Ancient Red Dragon",
            Size: CreatureSize.Gargantuan,
            Type: CreatureType.Dragon,
            Alignment: "Chaotic Evil",
            ChallengeRating: invalidCr);

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateCreatureRequestDto.ChallengeRating));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public async Task Validate_WithInvalidMaxHp_FailsValidation(int invalidHp)
    {
        // Arrange
        var request = new CreateCreatureRequestDto(
            Name: "Ghost",
            Size: CreatureSize.Medium,
            Type: CreatureType.Undead,
            Alignment: "Any",
            ChallengeRating: 4,
            MaxHitPoints: invalidHp);

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateCreatureRequestDto.MaxHitPoints));
    }
}
