using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sparnarok.Api.Application.DTOs;
using Sparnarok.Api.Domain.Entities;
using Sparnarok.Api.Domain.Enums;
using Sparnarok.Api.Domain.ValueObjects;
using Sparnarok.Api.Infrastructure.Persistence;
using Xunit;

namespace Sparnarok.IntegrationTests;

public record ArmorResponseTestDto(
    Guid Id,
    string Name,
    ArmorCategory Category,
    string RawArmorClass,
    int BaseArmorClass,
    int ShieldBonus,
    bool AllowsDexterityModifier,
    int? MaxDexterityModifier,
    int? StrengthRequirement,
    bool StealthDisadvantage,
    double Weight,
    string Cost);

/// <summary>
/// Pruebas de integración relacional para el pipeline de ingesta masiva de armaduras y escudos
/// (CSV Bulk Ingestion &amp; Dynamic AC) persistiendo en la base de datos administrada por <see cref="CustomWebApplicationFactory"/>.
/// </summary>
public class ArmorIngestionIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;

    public ArmorIngestionIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SparnarokDbContext>();
        TestDatabaseInitializer.EnsureCreated(context);
    }

    [Fact]
    public async Task IngestArmors_WithValidCsv_PersistsEntitiesInDatabase()
    {
        // Arrange: Limpieza previa de armaduras de prueba para aislamiento determinista
        using (var setupScope = _factory.Services.CreateScope())
        {
            var db = setupScope.ServiceProvider.GetRequiredService<SparnarokDbContext>();
            var toRemove = await db.Armors.Where(a => a.Name == "Leather Armor" || a.Name == "Half Plate Armor" || a.Name == "Plate Armor" || a.Name == "Shield").ToListAsync();
            if (toRemove.Count > 0)
            {
                var armorIds = toRemove.Select(a => a.Id).ToList();
                var creaturesWithArmor = await db.Creatures.Where(c => (c.EquippedArmorId != null && armorIds.Contains(c.EquippedArmorId.Value)) || (c.EquippedShieldId != null && armorIds.Contains(c.EquippedShieldId.Value))).ToListAsync();
                foreach (var c in creaturesWithArmor)
                {
                    if (c.EquippedArmorId != null && armorIds.Contains(c.EquippedArmorId.Value)) c.UnequipArmor();
                    if (c.EquippedShieldId != null && armorIds.Contains(c.EquippedShieldId.Value)) c.UnequipShield();
                }
                db.Armors.RemoveRange(toRemove);
                await db.SaveChangesAsync();
            }
        }

        // Mini CSV con una de cada categoría y guiones "-"
        const string csvData = """
            Name,Category,ArmorClass,Strength,Stealth,Weight,Cost
            Leather Armor,Light,11 + Dex modifier,-,-,10 lb.,10 GP
            Half Plate Armor,Medium,15 + Dex modifier (max 2),-,Disadvantage,40 lb.,750 GP
            Plate Armor,Heavy,18,Str 15,Disadvantage,65 lb.,"1,500 GP"
            Shield,Shield,+2,-,-,6 lb.,10 GP
            """;

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(csvData));
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("text/csv");
        content.Add(fileContent, "file", "armors_sample.csv");

        // Act
        var response = await _client.PostAsync("/api/ingestion/armors", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<IngestionResultDto>(_jsonOptions);
        result.Should().NotBeNull();
        result!.TotalProcessed.Should().Be(4);
        result.Inserted.Should().Be(4);
        result.Updated.Should().Be(0);
        result.Errors.Should().BeEmpty();

        // Verificar directamente en la base de datos relacional
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SparnarokDbContext>();

        var leather = await dbContext.Armors.FirstOrDefaultAsync(a => a.Name == "Leather Armor");
        leather.Should().NotBeNull();
        leather!.Category.Should().Be(ArmorCategory.Light);
        leather.BaseArmorClass.Should().Be(11);
        leather.AllowsDexterityModifier.Should().BeTrue();
        leather.MaxDexterityModifier.Should().BeNull();
        leather.StrengthRequirement.Should().BeNull();
        leather.StealthDisadvantage.Should().BeFalse();
        leather.Weight.Should().Be(10.0);

        var halfPlate = await dbContext.Armors.FirstOrDefaultAsync(a => a.Name == "Half Plate Armor");
        halfPlate.Should().NotBeNull();
        halfPlate!.Category.Should().Be(ArmorCategory.Medium);
        halfPlate.BaseArmorClass.Should().Be(15);
        halfPlate.AllowsDexterityModifier.Should().BeTrue();
        halfPlate.MaxDexterityModifier.Should().Be(2);
        halfPlate.StealthDisadvantage.Should().BeTrue();

        var plate = await dbContext.Armors.FirstOrDefaultAsync(a => a.Name == "Plate Armor");
        plate.Should().NotBeNull();
        plate!.Category.Should().Be(ArmorCategory.Heavy);
        plate.BaseArmorClass.Should().Be(18);
        plate.AllowsDexterityModifier.Should().BeFalse();
        plate.StrengthRequirement.Should().Be(15);
        plate.StealthDisadvantage.Should().BeTrue();
        plate.Cost.Should().Be("1,500 GP");

        var shield = await dbContext.Armors.FirstOrDefaultAsync(a => a.Name == "Shield");
        shield.Should().NotBeNull();
        shield!.Category.Should().Be(ArmorCategory.Shield);
        shield.ShieldBonus.Should().Be(2);
    }

    [Fact]
    public async Task IngestArmors_WithRealSeedFile_IngestsAll13ItemsAndAllowsGet()
    {
        // Arrange
        var root = Directory.GetCurrentDirectory();
        while (!File.Exists(Path.Combine(root, "Sparnarok.slnx")) && Directory.GetParent(root) != null)
        {
            root = Directory.GetParent(root)!.FullName;
        }
        var seedPath = Path.Combine(root, "data", "seeds", "armors.csv");
        File.Exists(seedPath).Should().BeTrue("El archivo data/seeds/armors.csv debe existir");

        var fileBytes = await File.ReadAllBytesAsync(seedPath);
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("text/csv");
        content.Add(fileContent, "file", "armors.csv");

        // Act
        var postResponse = await _client.PostAsync("/api/ingestion/armors", content);
        postResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var getResponse = await _client.GetAsync("/api/ingestion/armors");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var armors = await getResponse.Content.ReadFromJsonAsync<List<ArmorResponseTestDto>>(_jsonOptions);
        armors.Should().NotBeNull();
        armors!.Count.Should().BeGreaterThanOrEqualTo(13);
        armors.Should().Contain(a => a.Name == "Plate Armor" && a.BaseArmorClass == 18);
        armors.Should().Contain(a => a.Name == "Shield" && a.ShieldBonus == 2);
    }

    [Fact]
    public async Task EquipArmorAndShield_UpdatesCreatureEffectiveAcDynamically()
    {
        // 1. Crear una criatura con Dex 18 (+4)
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SparnarokDbContext>();

        var existingCreature = await dbContext.Creatures.FirstOrDefaultAsync(c => c.Name == "Dynamic AC Fighter");
        if (existingCreature != null) dbContext.Creatures.Remove(existingCreature);

        var existingArmors = await dbContext.Armors.Where(a => a.Name == "Test Half Plate" || a.Name == "Test Shield").ToListAsync();
        if (existingArmors.Count > 0) dbContext.Armors.RemoveRange(existingArmors);
        await dbContext.SaveChangesAsync();

        var abilities = new AbilityScores(strength: 16, dexterity: 18, constitution: 14, intelligence: 10, wisdom: 10, charisma: 10);
        var hp = new HitPoints(max: 30, current: 30);
        var ac = new ArmorClass(baseValue: 10, shieldBonus: 0, allowsDexterityModifier: true);

        var creature = new Creature(
            name: "Dynamic AC Fighter",
            size: CreatureSize.Medium,
            type: CreatureType.Humanoid,
            alignment: "Neutral",
            challengeRating: 1,
            speedFeet: 30,
            abilityScores: abilities,
            hitPoints: hp,
            armorClass: ac);

        // Sin armadura: 10 + Dex (4) = 14
        creature.EffectiveArmorClass.Should().Be(14);

        dbContext.Creatures.Add(creature);

        // Crear armadura Half Plate (Medium, Base 15, max 2) y Shield (+2)
        var halfPlate = new Armor(
            name: "Test Half Plate",
            category: ArmorCategory.Medium,
            rawArmorClass: "15 + Dex modifier (max 2)",
            strengthRequirement: null,
            stealthDisadvantage: true,
            weight: 40,
            cost: "750 GP");

        var shield = new Armor(
            name: "Test Shield",
            category: ArmorCategory.Shield,
            rawArmorClass: "+2",
            strengthRequirement: null,
            stealthDisadvantage: false,
            weight: 6,
            cost: "10 GP");

        dbContext.Armors.AddRange(halfPlate, shield);
        await dbContext.SaveChangesAsync();

        // 2. Equipar Half Plate: 15 + min(4, 2) = 17
        creature.EquipArmor(halfPlate);
        creature.EffectiveArmorClass.Should().Be(17);

        // 3. Equipar Escudo: 17 + 2 = 19
        creature.EquipShield(shield);
        creature.EffectiveArmorClass.Should().Be(19);

        // 4. Desequipar armadura: 10 + 4 + 2 (escudo) = 16
        creature.UnequipArmor();
        creature.EffectiveArmorClass.Should().Be(16);

        // 5. Desequipar escudo: 10 + 4 = 14
        creature.UnequipShield();
        creature.EffectiveArmorClass.Should().Be(14);

        // Teardown
        dbContext.Creatures.Remove(creature);
        dbContext.Armors.RemoveRange(halfPlate, shield);
        await dbContext.SaveChangesAsync();
    }
}
