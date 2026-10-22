using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Sparnarok.Api.Domain.Entities;
using Sparnarok.Api.Infrastructure.External.Dnd5eApi;
using Xunit;

namespace Sparnarok.UnitTests;

/// <summary>
/// Pruebas unitarias para <see cref="Dnd5eApiClient"/> y la entidad <see cref="StashItem"/>.
/// Mockea el <see cref="HttpMessageHandler"/> para validar el procesamiento de payloads de D&amp;D 5e API,
/// el manejo seguro de nulos (objetos ingrávidos) y el comportamiento de la caché en memoria (IMemoryCache).
/// </summary>
public class Dnd5eApiClientTests
{
    private class MockHttpMessageHandler : HttpMessageHandler
    {
        public int CallCount { get; private set; }
        public Func<HttpRequestMessage, HttpResponseMessage> Handler { get; set; }

        public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            Handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(Handler(request));
        }
    }

    [Fact]
    public async Task SearchEquipmentAsync_ShouldQueryBothEndpoints_FilterByTerm_AndReturnSummaryList()
    {
        // Arrange
        var mockHandler = new MockHttpMessageHandler(request =>
        {
            var uri = request.RequestUri!.ToString();
            if (uri.EndsWith("/equipment") || uri.EndsWith("equipment"))
            {
                var json = """
                {
                    "count": 2,
                    "results": [
                        { "index": "dagger", "name": "Dagger", "url": "/api/2024/equipment/dagger" },
                        { "index": "shortsword", "name": "Shortsword", "url": "/api/2024/equipment/shortsword" }
                    ]
                }
                """;
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) };
            }

            if (uri.EndsWith("/magic-items") || uri.EndsWith("magic-items"))
            {
                var json = """
                {
                    "count": 2,
                    "results": [
                        { "index": "dagger-of-venom", "name": "Dagger of Venom", "url": "/api/2024/magic-items/dagger-of-venom" },
                        { "index": "bag-of-holding", "name": "Bag of Holding", "url": "/api/2024/magic-items/bag-of-holding" }
                    ]
                }
                """;
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var httpClient = new HttpClient(mockHandler) { BaseAddress = new Uri("https://www.dnd5eapi.co/api/2024/") };
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var client = new Dnd5eApiClient(httpClient, memoryCache, NullLogger<Dnd5eApiClient>.Instance);

        // Act
        var results = await client.SearchEquipmentAsync("dagger");

        // Assert
        results.Should().HaveCount(2);
        results.Select(r => r.Name).Should().Contain(new[] { "Dagger", "Dagger of Venom" });
        results.Select(r => r.Index).Should().Contain(new[] { "dagger", "dagger-of-venom" });
    }

    [Fact]
    public async Task SearchEquipmentAsync_ShouldUseMemoryCache_AndNotHitHttpOnSubsequentCalls()
    {
        // Arrange
        var mockHandler = new MockHttpMessageHandler(request =>
        {
            var json = """
            {
                "count": 1,
                "results": [
                    { "index": "abacus", "name": "Abacus", "url": "/api/2024/equipment/abacus" }
                ]
            }
            """;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) };
        });

        var httpClient = new HttpClient(mockHandler) { BaseAddress = new Uri("https://www.dnd5eapi.co/api/2024/") };
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var client = new Dnd5eApiClient(httpClient, memoryCache, NullLogger<Dnd5eApiClient>.Instance);

        // Act - Primera llamada
        var firstCall = await client.SearchEquipmentAsync("abacus");
        var callsAfterFirst = mockHandler.CallCount;

        // Act - Segunda llamada con el mismo query
        var secondCall = await client.SearchEquipmentAsync("abacus");
        var callsAfterSecond = mockHandler.CallCount;

        // Assert
        firstCall.Should().HaveCount(1);
        secondCall.Should().HaveCount(1);
        callsAfterSecond.Should().Be(callsAfterFirst, "La segunda llamada debe ser servida íntegramente desde MemoryCache sin invocar el handler HTTP.");
    }

    [Fact]
    public async Task GetEquipmentByIndexAsync_WithNullWeight_ShouldMapSafelyToZeroWeight()
    {
        // Arrange - Simulamos un objeto mágico (ej. Anillo o Amuleto) que no tiene peso o viene con weight: null
        var mockHandler = new MockHttpMessageHandler(request =>
        {
            var json = """
            {
                "index": "ring-of-protection",
                "name": "Ring of Protection",
                "weight": null,
                "desc": [
                    "You gain a +1 bonus to AC and saving throws while wearing this ring."
                ],
                "rarity": {
                    "name": "Rare"
                },
                "equipment_category": {
                    "name": "Ring"
                }
            }
            """;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) };
        });

        var httpClient = new HttpClient(mockHandler) { BaseAddress = new Uri("https://www.dnd5eapi.co/api/2024/") };
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var client = new Dnd5eApiClient(httpClient, memoryCache, NullLogger<Dnd5eApiClient>.Instance);

        // Act
        var detail = await client.GetEquipmentByIndexAsync("ring-of-protection");

        // Assert
        detail.Should().NotBeNull();
        detail!.Index.Should().Be("ring-of-protection");
        detail.Name.Should().Be("Ring of Protection");
        detail.Weight.Should().Be(0m, "Si la API externa no reporta peso o es nulo, debe normalizarse defensivamente a 0");
        detail.Rarity.Should().Be("Rare");
        detail.ItemType.Should().Be("Ring");
        detail.Description.Should().Contain("+1 bonus to AC");
        detail.IsOfficialSrd.Should().BeTrue();
    }

    [Fact]
    public async Task GetEquipmentByIndexAsync_WhenEquipmentNotFound_ShouldFallbackToMagicItemsEndpoint()
    {
        // Arrange
        var mockHandler = new MockHttpMessageHandler(request =>
        {
            var uri = request.RequestUri!.ToString();
            if (uri.Contains("equipment/bag-of-holding"))
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            if (uri.Contains("magic-items/bag-of-holding"))
            {
                var json = """
                {
                    "index": "bag-of-holding",
                    "name": "Bag of Holding",
                    "weight": 15,
                    "desc": [
                        "This bag has an interior space considerably larger than its outside dimensions."
                    ],
                    "rarity": {
                        "name": "Uncommon"
                    },
                    "equipment_category": {
                        "name": "Wondrous Items"
                    }
                }
                """;
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var httpClient = new HttpClient(mockHandler) { BaseAddress = new Uri("https://www.dnd5eapi.co/api/2024/") };
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var client = new Dnd5eApiClient(httpClient, memoryCache, NullLogger<Dnd5eApiClient>.Instance);

        // Act
        var detail = await client.GetEquipmentByIndexAsync("bag-of-holding");

        // Assert
        detail.Should().NotBeNull();
        detail!.Index.Should().Be("bag-of-holding");
        detail.Weight.Should().Be(15m);
        detail.Rarity.Should().Be("Uncommon");
        detail.ItemType.Should().Be("Wondrous Items");
    }

    [Fact]
    public async Task GetEquipmentByIndexAsync_WhenNotFoundInBothEndpoints_ShouldReturnNull()
    {
        // Arrange
        var mockHandler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var httpClient = new HttpClient(mockHandler) { BaseAddress = new Uri("https://www.dnd5eapi.co/api/2024/") };
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var client = new Dnd5eApiClient(httpClient, memoryCache, NullLogger<Dnd5eApiClient>.Instance);

        // Act
        var detail = await client.GetEquipmentByIndexAsync("non-existent-item");

        // Assert
        detail.Should().BeNull();
    }

    [Fact]
    public void StashItem_DomainInvariants_ShouldEnforceValidState()
    {
        // Arrange
        var partyId = Guid.NewGuid();

        // Act: Creación válida
        var item = new StashItem(
            Guid.NewGuid(),
            partyId,
            "Cuerda Élfica",
            quantity: 2,
            weight: 5.5m,
            description: "Cuerda de seda tejida por elfos.",
            rarity: "Uncommon",
            itemType: "Adventuring Gear",
            isOfficialSrd: false
        );

        // Assert
        item.Name.Should().Be("Cuerda Élfica");
        item.Quantity.Should().Be(2);
        item.Weight.Should().Be(5.5m);
        item.IsOfficialSrd.Should().BeFalse();

        // Invariante de Nombre
        var actEmptyName = () => new StashItem(Guid.NewGuid(), partyId, "", 1, 1m, "desc");
        actEmptyName.Should().Throw<ArgumentException>();

        // Invariante de Cantidad
        var actZeroQty = () => new StashItem(Guid.NewGuid(), partyId, "Test", 0, 1m, "desc");
        actZeroQty.Should().Throw<ArgumentOutOfRangeException>();

        // Invariante de Peso
        var actNegativeWeight = () => new StashItem(Guid.NewGuid(), partyId, "Test", 1, -0.5m, "desc");
        actNegativeWeight.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Party_StashCalculations_ShouldSumTotalWeightCorrectly()
    {
        // Arrange
        var party = new Party(Guid.NewGuid(), "Los Exploradores", Guid.NewGuid());
        var item1 = new StashItem(Guid.NewGuid(), party.Id, "Poción de Curación", quantity: 3, weight: 0.5m, "Cura 2d4+2");
        var item2 = new StashItem(Guid.NewGuid(), party.Id, "Mochila Pesada", quantity: 1, weight: 5m, "Capacidad 30 lbs");

        // Act
        party.AddStashItem(item1);
        party.AddStashItem(item2);

        // Assert: 3 * 0.5 + 1 * 5 = 6.5 lbs
        party.StashItems.Should().HaveCount(2);
        party.TotalStashWeight.Should().Be(6.5m);

        // Remoción
        party.RemoveStashItem(item1.Id);
        party.StashItems.Should().HaveCount(1);
        party.TotalStashWeight.Should().Be(5m);
    }
}
