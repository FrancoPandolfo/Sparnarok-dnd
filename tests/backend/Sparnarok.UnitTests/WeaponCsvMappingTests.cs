using System.Text;
using FluentAssertions;
using Sparnarok.Api.Application.DTOs;
using Sparnarok.Api.Domain.Entities;
using Sparnarok.Api.Domain.Enums;
using Sparnarok.Api.Infrastructure.Services;
using Xunit;

namespace Sparnarok.UnitTests;

/// <summary>
/// Pruebas unitarias para el parseo, mapeo y entidades de ingesta masiva de armas mediante CSV.
/// </summary>
public class WeaponCsvMappingTests
{
    private readonly CsvParserService _parser = new();

    [Fact]
    public async Task ParseCsvAsync_WithCanonicalWeaponsCsv_MapsAllColumnsCorrectly()
    {
        // Arrange
        const string csvContent = """
            Name,Damage,DamageType,Properties,Weight,Cost
            Longsword,1d8,Slashing,Versatile (1d10),3.0,15 gp
            Dagger,1d4,Piercing,"Finesse, Light, Thrown (20/60)",1.0,2 gp
            Greatsword,2d6,Slashing,"Heavy, Two-Handed",6.0,50 gp
            """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));

        // Act
        var records = await _parser.ParseCsvAsync<WeaponCsvRecord, WeaponCsvClassMap>(stream);

        // Assert
        records.Should().HaveCount(3);

        var longsword = records.First(r => r.Name == "Longsword");
        longsword.Damage.Should().Be("1d8");
        longsword.DamageType.Should().Be("Slashing");
        longsword.Properties.Should().Be("Versatile (1d10)");
        longsword.Weight.Should().Be(3.0);
        longsword.Cost.Should().Be("15 gp");

        var dagger = records.First(r => r.Name == "Dagger");
        dagger.Damage.Should().Be("1d4");
        dagger.DamageType.Should().Be("Piercing");
        dagger.Properties.Should().Be("Finesse, Light, Thrown (20/60)");
        dagger.Weight.Should().Be(1.0);
        dagger.Cost.Should().Be("2 gp");
    }

    [Fact]
    public async Task ParseCsvAsync_WithMissingOptionalColumns_HandlesDefaultsWithoutErrors()
    {
        // Arrange: Solo columnas requeridas
        const string csvContent = """
            Name,Damage,DamageType
            Shortbow,1d6,Piercing
            Club,1d4,Bludgeoning
            """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));

        // Act
        var records = await _parser.ParseCsvAsync<WeaponCsvRecord, WeaponCsvClassMap>(stream);

        // Assert
        records.Should().HaveCount(2);
        records[0].Name.Should().Be("Shortbow");
        records[0].Damage.Should().Be("1d6");
        records[0].DamageType.Should().Be("Piercing");
        records[0].Properties.Should().BeNullOrEmpty();
        records[0].Weight.Should().BeNull();
        records[0].Cost.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task ParseCsvAsync_WithWhitespaceInHeadersAndValues_TrimsCorrectly()
    {
        // Arrange
        const string csvContent = """
             Name , Damage , DamageType , Properties , Weight , Cost 
              Halberd , 1d10 , Slashing , "Heavy, Reach" , 6.0 , 20 gp 
            """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));

        // Act
        var records = await _parser.ParseCsvAsync<WeaponCsvRecord, WeaponCsvClassMap>(stream);

        // Assert
        records.Should().HaveCount(1);
        var halberd = records[0];
        halberd.Name.Should().Be("Halberd");
        halberd.Damage.Should().Be("1d10");
        halberd.DamageType.Should().Be("Slashing");
        halberd.Properties.Should().Be("Heavy, Reach");
        halberd.Weight.Should().Be(6.0);
        halberd.Cost.Should().Be("20 gp");
    }

    [Fact]
    public void WeaponEntity_ValidConstructorAndModification_WorksCorrectly()
    {
        // Arrange & Act: Creación
        var weapon = new Weapon(
            name: "Morningstar",
            damage: "1d8",
            damageType: DamageType.Piercing,
            properties: "Martial",
            weight: 4.0,
            cost: "15 gp");

        // Assert
        weapon.Id.Should().NotBeEmpty();
        weapon.Name.Should().Be("Morningstar");
        weapon.Damage.Should().Be("1d8");
        weapon.DamageType.Should().Be(DamageType.Piercing);
        weapon.Weight.Should().Be(4.0);
        weapon.Cost.Should().Be("15 gp");

        // Act: Actualización (Upsert)
        weapon.Update(
            damage: "2d4",
            damageType: DamageType.Bludgeoning,
            properties: "Versatile",
            weight: 3.5,
            cost: "18 gp");

        weapon.Damage.Should().Be("2d4");
        weapon.DamageType.Should().Be(DamageType.Bludgeoning);
        weapon.Properties.Should().Be("Versatile");
        weapon.Weight.Should().Be(3.5);
        weapon.Cost.Should().Be("18 gp");
    }

    [Theory]
    [InlineData("", "1d8", "El nombre del arma es obligatorio.")]
    [InlineData("   ", "1d8", "El nombre del arma es obligatorio.")]
    [InlineData("Sword", "", "La fórmula de daño del arma es obligatoria.")]
    [InlineData("Sword", "   ", "La fórmula de daño del arma es obligatoria.")]
    public void WeaponEntity_WithInvalidParameters_ThrowsArgumentException(string name, string damage, string expectedMessage)
    {
        // Act
        var act = () => new Weapon(name, damage, DamageType.Slashing);

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage($"*{expectedMessage}*");
    }

    [Fact]
    public void WeaponEntity_WithNegativeWeight_ThrowsArgumentOutOfRangeException()
    {
        // Act
        var act = () => new Weapon("Warhammer", "1d8", DamageType.Bludgeoning, weight: -2.0);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*peso*");
    }

    [Fact]
    public async Task ParseCsvAsync_WithSeedWeaponsCsvFile_ParsesCorrectly()
    {
        var filePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../../data/seeds/weapons.csv"));
        using var stream = File.OpenRead(filePath);
        var records = await _parser.ParseCsvAsync<WeaponCsvRecord, WeaponCsvClassMap>(stream);

        records.Should().HaveCount(38);

        var club = records.First(r => r.Name == "Club");
        club.Damage.Should().Be("1d4");
        club.DamageType.Should().Be("Bludgeoning");
        club.Weight.Should().Be(2.0);
        club.Cost.Should().Be("1 SP");
        club.Mastery.Should().Be("Slow");

        var dart = records.First(r => r.Name == "Dart");
        dart.Weight.Should().Be(0.25);

        var sling = records.First(r => r.Name == "Sling");
        sling.Weight.Should().BeNull();

        var blowgun = records.First(r => r.Name == "Blowgun");
        blowgun.Damage.Should().Be("1");
        blowgun.DamageType.Should().Be("Piercing");

        var musket = records.First(r => r.Name == "Musket");
        musket.Damage.Should().Be("1d12");
        musket.Cost.Should().Be("500 GP");
    }
}

