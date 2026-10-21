using FluentAssertions;
using Sparnarok.Api.Domain.Entities;
using Sparnarok.Api.Domain.Enums;
using Sparnarok.Api.Domain.Services.Combat;
using Sparnarok.Api.Domain.ValueObjects;
using Xunit;

namespace Sparnarok.UnitTests;

/// <summary>
/// Pruebas unitarias rigurosas para validar todas las combinaciones matemáticas del cálculo
/// dinámico de Clase de Armadura (AC) según las reglas de D&amp;D 5e (2024).
/// </summary>
public class ArmorClassCalculatorTests
{
    private readonly Armor _padded = new("Padded Armor", ArmorCategory.Light, "11 + Dex modifier", null, true, 8, "5 GP");
    private readonly Armor _leather = new("Leather Armor", ArmorCategory.Light, "11 + Dex modifier", null, false, 10, "10 GP");
    private readonly Armor _studdedLeather = new("Studded Leather Armor", ArmorCategory.Light, "12 + Dex modifier", null, false, 13, "45 GP");
    private readonly Armor _hide = new("Hide Armor", ArmorCategory.Medium, "12 + Dex modifier (max 2)", null, false, 12, "10 GP");
    private readonly Armor _chainShirt = new("Chain Shirt", ArmorCategory.Medium, "13 + Dex modifier (max 2)", null, false, 20, "50 GP");
    private readonly Armor _breastplate = new("Breastplate", ArmorCategory.Medium, "14 + Dex modifier (max 2)", null, false, 20, "400 GP");
    private readonly Armor _halfPlate = new("Half Plate Armor", ArmorCategory.Medium, "15 + Dex modifier (max 2)", null, true, 40, "750 GP");
    private readonly Armor _ringMail = new("Ring Mail", ArmorCategory.Heavy, "14", null, true, 40, "30 GP");
    private readonly Armor _chainMail = new("Chain Mail", ArmorCategory.Heavy, "16", 13, true, 55, "75 GP");
    private readonly Armor _splint = new("Splint Armor", ArmorCategory.Heavy, "17", 15, true, 60, "200 GP");
    private readonly Armor _plate = new("Plate Armor", ArmorCategory.Heavy, "18", 15, true, 65, "1,500 GP");
    private readonly Armor _shield = new("Shield", ArmorCategory.Shield, "+2", null, false, 6, "10 GP");

    #region Sin Armadura (Unarmored)

    [Theory]
    [InlineData(0, 10)]   // Dex Mod +0 -> CA 10
    [InlineData(2, 12)]   // Dex Mod +2 -> CA 12
    [InlineData(4, 14)]   // Dex Mod +4 -> CA 14
    [InlineData(-1, 9)]   // Dex Mod -1 -> CA 9
    [InlineData(-2, 8)]   // Dex Mod -2 -> CA 8
    public void CalculateAc_Unarmored_WithoutShield_Equals10PlusDexModifier(int dexMod, int expectedAc)
    {
        // Act
        var ac = ArmorClassCalculator.CalculateAc(dexMod, equippedArmor: null, hasShield: false);

        // Assert
        ac.Should().Be(expectedAc);
    }

    [Theory]
    [InlineData(0, 12)]   // Dex Mod +0 + Escudo -> CA 12
    [InlineData(2, 14)]   // Dex Mod +2 + Escudo -> CA 14
    [InlineData(4, 16)]   // Dex Mod +4 + Escudo -> CA 16
    [InlineData(-1, 11)]  // Dex Mod -1 + Escudo -> CA 11
    public void CalculateAc_Unarmored_WithShield_AddsTwoToTotal(int dexMod, int expectedAc)
    {
        var ac = ArmorClassCalculator.CalculateAc(dexMod, equippedArmor: null, hasShield: true);
        ac.Should().Be(expectedAc);
    }

    #endregion

    #region Armadura Ligera (Light Armor)

    [Fact]
    public void CalculateAc_LightArmor_PaddedAndLeather_AddsFullDexModifier()
    {
        // Leather (Base 11) con Dex 16 (+3) -> 14
        ArmorClassCalculator.CalculateAc(dexterityModifier: 3, _leather, hasShield: false)
            .Should().Be(14);

        // Padded (Base 11) con Dex 8 (-1) -> 10
        ArmorClassCalculator.CalculateAc(dexterityModifier: -1, _padded, hasShield: false)
            .Should().Be(10);
    }

    [Fact]
    public void CalculateAc_LightArmor_StuddedLeather_WithHighDexAndShield()
    {
        // Studded Leather (Base 12) con Dex 20 (+5) -> 17
        ArmorClassCalculator.CalculateAc(dexterityModifier: 5, _studdedLeather, hasShield: false)
            .Should().Be(17);

        // Studded Leather (Base 12) con Dex 20 (+5) y Escudo (+2) -> 19
        ArmorClassCalculator.CalculateAc(dexterityModifier: 5, _studdedLeather, hasShield: true)
            .Should().Be(19);
    }

    #endregion

    #region Armadura Media (Medium Armor)

    [Fact]
    public void CalculateAc_MediumArmor_HalfPlateWithDex18_MustBe17Not19()
    {
        // Requisito expreso de especificación:
        // Personaje con Destreza 18 (+4) y Half Plate (Base 15, max 2) debe tener CA 17, no 19.
        int dexModifier = 4; // Destreza 18 -> Modificador +4

        var ac = ArmorClassCalculator.CalculateAc(dexModifier, _halfPlate, hasShield: false);

        ac.Should().Be(17);
    }

    [Fact]
    public void CalculateAc_MediumArmor_HalfPlateWithDex18AndShield_MustBe19()
    {
        // Half Plate (15) + Capped Dex (2) + Shield (2) = 19
        int dexModifier = 4;

        var ac = ArmorClassCalculator.CalculateAc(dexModifier, _halfPlate, hasShield: true);

        ac.Should().Be(19);
    }

    [Theory]
    [InlineData(5, 17)]  // Dex 20 (+5) -> 15 + 2 = 17 (capped)
    [InlineData(4, 17)]  // Dex 18 (+4) -> 15 + 2 = 17 (capped)
    [InlineData(3, 17)]  // Dex 16 (+3) -> 15 + 2 = 17 (capped)
    [InlineData(2, 17)]  // Dex 14 (+2) -> 15 + 2 = 17
    [InlineData(1, 16)]  // Dex 12 (+1) -> 15 + 1 = 16
    [InlineData(0, 15)]  // Dex 10 (+0) -> 15 + 0 = 15
    [InlineData(-1, 14)] // Dex 8 (-1) -> 15 - 1 = 14 (penalizador negativo aplica)
    public void CalculateAc_MediumArmor_HalfPlate_TestsEntireDexSpectrum(int dexMod, int expectedAc)
    {
        var ac = ArmorClassCalculator.CalculateAc(dexMod, _halfPlate, hasShield: false);
        ac.Should().Be(expectedAc);
    }

    [Fact]
    public void CalculateAc_MediumArmor_BreastplateAndChainShirt_RespectsCaps()
    {
        // Breastplate (Base 14) con Dex 16 (+3) -> 16
        ArmorClassCalculator.CalculateAc(dexterityModifier: 3, _breastplate, hasShield: false)
            .Should().Be(16);

        // Chain Shirt (Base 13) con Dex 14 (+2) y Escudo (+2) -> 17
        ArmorClassCalculator.CalculateAc(dexterityModifier: 2, _chainShirt, hasShield: true)
            .Should().Be(17);
    }

    #endregion

    #region Armadura Pesada (Heavy Armor)

    [Theory]
    [InlineData(5, 18)]   // Dex 20 (+5) -> CA 18 (se ignora bono)
    [InlineData(4, 18)]   // Dex 18 (+4) -> CA 18
    [InlineData(0, 18)]   // Dex 10 (+0) -> CA 18
    [InlineData(-1, 18)]  // Dex 8 (-1) -> CA 18 (se ignora penalizador)
    [InlineData(-3, 18)]  // Dex 4 (-3) -> CA 18
    public void CalculateAc_HeavyArmor_PlateArmor_IgnoresDexterityCompletely(int dexMod, int expectedAc)
    {
        var ac = ArmorClassCalculator.CalculateAc(dexMod, _plate, hasShield: false);
        ac.Should().Be(expectedAc);
    }

    [Fact]
    public void CalculateAc_HeavyArmor_WithShield_AddsTwoToPureBase()
    {
        // Plate (18) + Escudo (+2) = 20
        ArmorClassCalculator.CalculateAc(dexterityModifier: 2, _plate, hasShield: true)
            .Should().Be(20);

        // Chain Mail (16) + Escudo (+2) con Dex negativa = 18
        ArmorClassCalculator.CalculateAc(dexterityModifier: -2, _chainMail, hasShield: true)
            .Should().Be(18);
    }

    #endregion

    #region Integración de Entidad Creature y Ciclo Dinámico de Equipamiento

    [Fact]
    public void Creature_EffectiveArmorClass_UpdatesDynamicallyWhenArmorAndShieldAreEquipped()
    {
        // Arrange: Criatura con Destreza 18 (+4)
        var abilityScores = new AbilityScores(10, 18, 14, 10, 10, 10);
        var hero = new Creature(
            "Paladin Hero",
            CreatureSize.Medium,
            CreatureType.Humanoid,
            "Lawful Good",
            challengeRating: 3,
            abilityScores: abilityScores);

        // 1. Inicial: Sin armadura (10 + 4 = 14)
        hero.EffectiveArmorClass.Should().Be(14);
        hero.EquippedArmor.Should().BeNull();
        hero.EquippedShield.Should().BeNull();

        // 2. Equipa Half Plate (Base 15 + max 2 Dex = 17)
        hero.EquipArmor(_halfPlate);
        hero.EffectiveArmorClass.Should().Be(17);
        hero.EquippedArmorId.Should().Be(_halfPlate.Id);

        // 3. Equipa Escudo (+2 -> 19)
        hero.EquipShield(_shield);
        hero.EffectiveArmorClass.Should().Be(19);
        hero.EquippedShieldId.Should().Be(_shield.Id);

        // 4. Cambia a Plate Armor (Base 18 pura + 2 Escudo = 20)
        hero.EquipArmor(_plate);
        hero.EffectiveArmorClass.Should().Be(20);

        // 5. Desequipa Escudo (20 - 2 = 18)
        hero.UnequipShield();
        hero.EffectiveArmorClass.Should().Be(18);
        hero.EquippedShield.Should().BeNull();

        // 6. Desequipa Armadura (Vuelve a sin armadura: 10 + 4 = 14)
        hero.UnequipArmor();
        hero.EffectiveArmorClass.Should().Be(14);
        hero.EquippedArmor.Should().BeNull();
    }

    [Fact]
    public void Creature_EquipArmor_ThrowsExceptionIfShieldPassedToArmorSlot()
    {
        var creature = new Creature("Fighter", CreatureSize.Medium, CreatureType.Humanoid, "Neutral", 1);

        var act = () => creature.EquipArmor(_shield);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*ranura de armadura corporal*");
    }

    [Fact]
    public void Creature_EquipShield_ThrowsExceptionIfBodyArmorPassedToShieldSlot()
    {
        var creature = new Creature("Fighter", CreatureSize.Medium, CreatureType.Humanoid, "Neutral", 1);

        var act = () => creature.EquipShield(_halfPlate);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*categoría Shield*");
    }

    #endregion
}
