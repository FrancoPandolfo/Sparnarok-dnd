using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Sparnarok.Api.Application.DTOs;
using Sparnarok.Api.Domain.Entities;
using Sparnarok.Api.Domain.Enums;
using Sparnarok.Api.Infrastructure.Persistence;
using Sparnarok.Api.Infrastructure.Services;
using Xunit;

namespace Sparnarok.UnitTests;

/// <summary>
/// Pruebas unitarias para el parseo, mapeo declarativo y servicio de ingesta masiva de armaduras y escudos.
/// </summary>
public class ArmorCsvMappingTests
{
    private readonly CsvParserService _parser = new();

    private const string CanonicalArmorsCsv = """
        Name,Category,ArmorClass,Strength,Stealth,Weight,Cost
        Padded Armor,Light,11 + Dex modifier,-,Disadvantage,8 lb.,5 GP
        Leather Armor,Light,11 + Dex modifier,-,-,10 lb.,10 GP
        Studded Leather Armor,Light,12 + Dex modifier,-,-,13 lb.,45 GP
        Hide Armor,Medium,12 + Dex modifier (max 2),-,-,12 lb.,10 GP
        Chain Shirt,Medium,13 + Dex modifier (max 2),-,-,20 lb.,50 GP
        Scale Mail,Medium,14 + Dex modifier (max 2),-,Disadvantage,45 lb.,50 GP
        Breastplate,Medium,14 + Dex modifier (max 2),-,-,20 lb.,400 GP
        Half Plate Armor,Medium,15 + Dex modifier (max 2),-,Disadvantage,40 lb.,750 GP
        Ring Mail,Heavy,14,-,Disadvantage,40 lb.,30 GP
        Chain Mail,Heavy,16,Str 13,Disadvantage,55 lb.,75 GP
        Splint Armor,Heavy,17,Str 15,Disadvantage,60 lb.,200 GP
        Plate Armor,Heavy,18,Str 15,Disadvantage,65 lb.,1,500 GP
        Shield,Shield,+2,-,-,6 lb.,10 GP
        """;

    [Fact]
    public void ParseArmorClass_ParsesAllDndFormatsCorrectly()
    {
        // Light
        var (b1, s1, d1, m1) = Armor.ParseArmorClass("11 + Dex modifier", ArmorCategory.Light);
        b1.Should().Be(11);
        s1.Should().Be(0);
        d1.Should().BeTrue();
        m1.Should().BeNull();

        // Medium
        var (b2, s2, d2, m2) = Armor.ParseArmorClass("15 + Dex modifier (max 2)", ArmorCategory.Medium);
        b2.Should().Be(15);
        s2.Should().Be(0);
        d2.Should().BeTrue();
        m2.Should().Be(2);

        // Heavy
        var (b3, s3, d3, m3) = Armor.ParseArmorClass("18", ArmorCategory.Heavy);
        b3.Should().Be(18);
        s3.Should().Be(0);
        d3.Should().BeFalse();
        m3.Should().BeNull();

        // Shield
        var (b4, s4, d4, m4) = Armor.ParseArmorClass("+2", ArmorCategory.Shield);
        b4.Should().Be(0);
        s4.Should().Be(2);
        d4.Should().BeFalse();
        m4.Should().BeNull();
    }

    [Fact]
    public async Task ParseCsvAsync_WithCanonicalArmorsCsv_MapsAllColumnsAndParsesCorrectly()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(CanonicalArmorsCsv));

        var records = await _parser.ParseCsvAsync<ArmorCsvRecord, ArmorCsvClassMap>(stream);

        records.Should().HaveCount(13);

        var halfPlate = records.First(r => r.Name == "Half Plate Armor");
        halfPlate.Category.Should().Be("Medium");
        halfPlate.ArmorClass.Should().Be("15 + Dex modifier (max 2)");
        halfPlate.Strength.Should().Be("-");
        halfPlate.Stealth.Should().Be("Disadvantage");
        halfPlate.Weight.Should().Be(40.0);
        halfPlate.Cost.Should().Be("750 GP");

        var chainMail = records.First(r => r.Name == "Chain Mail");
        chainMail.Category.Should().Be("Heavy");
        chainMail.Strength.Should().Be("Str 13");
        chainMail.Stealth.Should().Be("Disadvantage");
        chainMail.Weight.Should().Be(55.0);

        var shield = records.First(r => r.Name == "Shield");
        shield.Category.Should().Be("Shield");
        shield.ArmorClass.Should().Be("+2");
        shield.Weight.Should().Be(6.0);
        shield.Cost.Should().Be("10 GP");
    }

    [Fact]
    public async Task ParseCsvAsync_WithDiskSeedArmorsCsvFile_ParsesAll13ArmorsCorrectly()
    {
        var filePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../../data/seeds/armors.csv"));
        using var stream = File.OpenRead(filePath);
        var records = await _parser.ParseCsvAsync<ArmorCsvRecord, ArmorCsvClassMap>(stream);

        records.Should().HaveCount(13);

        var padded = records.First(r => r.Name == "Padded Armor");
        padded.Category.Should().Be("Light");
        padded.ArmorClass.Should().Be("11 + Dex modifier");
        padded.Stealth.Should().Be("Disadvantage");
        padded.Weight.Should().Be(8.0);
        padded.Cost.Should().Be("5 GP");

        var plate = records.First(r => r.Name == "Plate Armor");
        plate.Category.Should().Be("Heavy");
        plate.ArmorClass.Should().Be("18");
        plate.Strength.Should().Be("Str 15");
        plate.Stealth.Should().Be("Disadvantage");
        plate.Weight.Should().Be(65.0);
        plate.Cost.Should().Be("1,500 GP");
    }

    [Fact]
    public void ArmorEntity_Update_ModifiesPropertiesAndRecalculatesParsedAC()
    {
        var armor = new Armor("Custom Plate", ArmorCategory.Heavy, "17", 15, true, 60, "200 GP");
        armor.BaseArmorClass.Should().Be(17);
        armor.AllowsDexterityModifier.Should().BeFalse();

        // Update to Medium
        armor.Update(ArmorCategory.Medium, "14 + Dex modifier (max 2)", 13, false, 40, "400 GP");

        armor.Category.Should().Be(ArmorCategory.Medium);
        armor.BaseArmorClass.Should().Be(14);
        armor.AllowsDexterityModifier.Should().BeTrue();
        armor.MaxDexterityModifier.Should().Be(2);
        armor.StrengthRequirement.Should().Be(13);
        armor.StealthDisadvantage.Should().BeFalse();
        armor.Weight.Should().Be(40);
        armor.Cost.Should().Be("400 GP");
    }

    [Theory]
    [InlineData("", "18", "El nombre de la armadura es obligatorio.")]
    [InlineData("   ", "18", "El nombre de la armadura es obligatorio.")]
    [InlineData("Plate", "", "La fórmula de Clase de Armadura es obligatoria.")]
    [InlineData("Plate", "   ", "La fórmula de Clase de Armadura es obligatoria.")]
    public void ArmorEntity_WithInvalidParameters_ThrowsArgumentException(string name, string acFormula, string expectedMessage)
    {
        var act = () => new Armor(name, ArmorCategory.Heavy, acFormula);
        act.Should().Throw<ArgumentException>().WithMessage($"*{expectedMessage}*");
    }

    [Fact]
    public void ArmorEntity_WithNegativeWeight_ThrowsArgumentOutOfRangeException()
    {
        var act = () => new Armor("Chain Mail", ArmorCategory.Heavy, "16", weight: -5.0);
        act.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*peso*");
    }
}
