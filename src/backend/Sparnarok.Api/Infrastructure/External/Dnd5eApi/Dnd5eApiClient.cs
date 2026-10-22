using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Sparnarok.Api.Application.DTOs;

namespace Sparnarok.Api.Infrastructure.External.Dnd5eApi;

/// <summary>
/// Implementación del cliente HTTP tipado para consultar el SRD de D&amp;D 5e (vía dnd5eapi.co).
/// Incorpora estrategias de caché en memoria (IMemoryCache) para mitigar latencia y prevenir saturación de la API externa.
/// </summary>
public class Dnd5eApiClient : IDnd5eApiClient
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<Dnd5eApiClient> _logger;

    private static readonly TimeSpan CatalogCacheDuration = TimeSpan.FromHours(1);
    private static readonly TimeSpan QueryCacheDuration = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan DetailCacheDuration = TimeSpan.FromHours(2);

    private const string AllItemsCacheKey = "dnd5eapi:all-catalog-items";

    public Dnd5eApiClient(HttpClient httpClient, IMemoryCache memoryCache, ILogger<Dnd5eApiClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReferenceItemSummaryDto>> SearchEquipmentAsync(string nameQuery, CancellationToken cancellationToken = default)
    {
        var normalizedQuery = nameQuery?.Trim().ToLowerInvariant() ?? string.Empty;

        var queryCacheKey = $"dnd5eapi:search:{normalizedQuery}";
        if (_memoryCache.TryGetValue(queryCacheKey, out IReadOnlyList<ReferenceItemSummaryDto>? cachedResults) && cachedResults is not null)
        {
            _logger.LogDebug("Cache HIT para búsqueda de equipamiento: '{Query}'", normalizedQuery);
            return cachedResults;
        }

        var allItems = await GetAllCatalogItemsAsync(cancellationToken);

        IReadOnlyList<ReferenceItemSummaryDto> filtered;
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            filtered = allItems.Take(50).ToList();
        }
        else
        {
            filtered = allItems
                .Where(item => item.Name.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                               item.Index.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
                .Take(50)
                .ToList();
        }

        _memoryCache.Set(queryCacheKey, filtered, QueryCacheDuration);
        return filtered;
    }

    /// <inheritdoc />
    public async Task<ReferenceEquipmentDetailDto?> GetEquipmentByIndexAsync(string index, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(index))
        {
            return null;
        }

        var normalizedIndex = index.Trim().ToLowerInvariant();
        var detailCacheKey = $"dnd5eapi:detail:{normalizedIndex}";

        if (_memoryCache.TryGetValue(detailCacheKey, out ReferenceEquipmentDetailDto? cachedDetail) && cachedDetail is not null)
        {
            _logger.LogDebug("Cache HIT para detalle de equipamiento: '{Index}'", normalizedIndex);
            return cachedDetail;
        }

        // Primero intenta consultar en /equipment/{index}
        var detail = await TryFetchItemDetailAsync($"equipment/{normalizedIndex}", normalizedIndex, cancellationToken);

        // Si no se encuentra en equipment (404), intenta en /magic-items/{index}
        if (detail is null)
        {
            detail = await TryFetchItemDetailAsync($"magic-items/{normalizedIndex}", normalizedIndex, cancellationToken);
        }

        if (detail is not null)
        {
            _memoryCache.Set(detailCacheKey, detail, DetailCacheDuration);
        }

        return detail;
    }

    private async Task<IReadOnlyList<ReferenceItemSummaryDto>> GetAllCatalogItemsAsync(CancellationToken cancellationToken)
    {
        if (_memoryCache.TryGetValue(AllItemsCacheKey, out IReadOnlyList<ReferenceItemSummaryDto>? cachedCatalog) && cachedCatalog is not null)
        {
            return cachedCatalog;
        }

        var results = new List<ReferenceItemSummaryDto>();

        // 1. Obtener listado de equipo estándar (/equipment)
        try
        {
            var equipmentList = await FetchSummaryListAsync("equipment", cancellationToken);
            results.AddRange(equipmentList);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al consultar /equipment desde D&D 5e API externa.");
        }

        // 2. Obtener listado de objetos mágicos (/magic-items)
        try
        {
            var magicItemsList = await FetchSummaryListAsync("magic-items", cancellationToken);
            // Evitar duplicados por index
            foreach (var item in magicItemsList)
            {
                if (!results.Any(r => r.Index.Equals(item.Index, StringComparison.OrdinalIgnoreCase)))
                {
                    results.Add(item);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al consultar /magic-items desde D&D 5e API externa.");
        }

        var readOnlyResults = results.AsReadOnly();
        _memoryCache.Set(AllItemsCacheKey, readOnlyResults, CatalogCacheDuration);
        return readOnlyResults;
    }

    private async Task<IReadOnlyList<ReferenceItemSummaryDto>> FetchSummaryListAsync(string endpoint, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(endpoint, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Respuesta fallida de D&D 5e API ({Endpoint}): {StatusCode}", endpoint, response.StatusCode);
            return Array.Empty<ReferenceItemSummaryDto>();
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var items = new List<ReferenceItemSummaryDto>();
        if (root.TryGetProperty("results", out var resultsProp) && resultsProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in resultsProp.EnumerateArray())
            {
                var itemIndex = element.TryGetProperty("index", out var idx) ? idx.GetString() ?? string.Empty : string.Empty;
                var itemName = element.TryGetProperty("name", out var name) ? name.GetString() ?? string.Empty : string.Empty;
                var itemUrl = element.TryGetProperty("url", out var url) ? url.GetString() ?? string.Empty : string.Empty;

                if (!string.IsNullOrWhiteSpace(itemIndex) && !string.IsNullOrWhiteSpace(itemName))
                {
                    items.Add(new ReferenceItemSummaryDto(itemIndex, itemName, itemUrl));
                }
            }
        }

        return items;
    }

    private async Task<ReferenceEquipmentDetailDto?> TryFetchItemDetailAsync(string relativeUri, string fallbackIndex, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.GetAsync(relativeUri, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Error al consultar {Uri}: {StatusCode}", relativeUri, response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            return MapJsonToDetailDto(root, fallbackIndex);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Excepción al consultar detalle en {Uri}", relativeUri);
            return null;
        }
    }

    private static ReferenceEquipmentDetailDto MapJsonToDetailDto(JsonElement root, string fallbackIndex)
    {
        var itemIndex = root.TryGetProperty("index", out var idxProp) ? idxProp.GetString() ?? fallbackIndex : fallbackIndex;
        var itemName = root.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? itemIndex : itemIndex;

        // Peso: Manejo defensivo contra nulos o tipos no numéricos
        decimal weight = 0m;
        if (root.TryGetProperty("weight", out var weightProp))
        {
            if (weightProp.ValueKind == JsonValueKind.Number)
            {
                weight = weightProp.GetDecimal();
            }
        }

        // Descripción: Soportar tanto propiedad 'description' como 'desc' (array o string)
        string description = string.Empty;
        if (root.TryGetProperty("description", out var descListProp) && descListProp.ValueKind == JsonValueKind.Array)
        {
            var paragraphs = descListProp.EnumerateArray()
                .Select(p => p.GetString())
                .Where(s => !string.IsNullOrWhiteSpace(s));
            description = string.Join("\n\n", paragraphs);
        }
        else if (root.TryGetProperty("desc", out var descLegacyProp) && descLegacyProp.ValueKind == JsonValueKind.Array)
        {
            var paragraphs = descLegacyProp.EnumerateArray()
                .Select(p => p.GetString())
                .Where(s => !string.IsNullOrWhiteSpace(s));
            description = string.Join("\n\n", paragraphs);
        }
        else if (root.TryGetProperty("desc", out var descStrProp) && descStrProp.ValueKind == JsonValueKind.String)
        {
            description = descStrProp.GetString() ?? string.Empty;
        }

        // Rareza: Objeto { name: 'Uncommon' } o string directo
        string rarity = "Common";
        if (root.TryGetProperty("rarity", out var rarityProp))
        {
            if (rarityProp.ValueKind == JsonValueKind.Object && rarityProp.TryGetProperty("name", out var rName))
            {
                rarity = rName.GetString() ?? "Common";
            }
            else if (rarityProp.ValueKind == JsonValueKind.String)
            {
                rarity = rarityProp.GetString() ?? "Common";
            }
        }

        // Tipo o Categoría
        string itemType = "Adventuring Gear";
        if (root.TryGetProperty("equipment_categories", out var eqCats) && eqCats.ValueKind == JsonValueKind.Array)
        {
            var firstCat = eqCats.EnumerateArray().FirstOrDefault();
            if (firstCat.ValueKind == JsonValueKind.Object && firstCat.TryGetProperty("name", out var catName))
            {
                itemType = catName.GetString() ?? "Adventuring Gear";
            }
        }
        else if (root.TryGetProperty("equipment_category", out var eqCat) && eqCat.ValueKind == JsonValueKind.Object)
        {
            if (eqCat.TryGetProperty("name", out var catName))
            {
                itemType = catName.GetString() ?? "Adventuring Gear";
            }
        }

        // Costo si está disponible
        string? costStr = null;
        if (root.TryGetProperty("cost", out var costProp) && costProp.ValueKind == JsonValueKind.Object)
        {
            var quantity = costProp.TryGetProperty("quantity", out var q) ? q.GetInt32().ToString() : string.Empty;
            var unit = costProp.TryGetProperty("unit", out var u) ? u.GetString() : string.Empty;
            if (!string.IsNullOrWhiteSpace(quantity) && !string.IsNullOrWhiteSpace(unit))
            {
                costStr = $"{quantity} {unit}";
            }
        }

        return new ReferenceEquipmentDetailDto(
            itemIndex,
            itemName,
            weight,
            description,
            rarity,
            itemType,
            IsOfficialSrd: true,
            costStr
        );
    }
}
