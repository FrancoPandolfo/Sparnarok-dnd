using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sparnarok.Api.Application.DTOs;
using Sparnarok.Api.Application.Interfaces;

namespace Sparnarok.Api.Infrastructure.Services;

/// <summary>
/// Implementación de cliente HTTP tipado para Open5e usando HttpClientFactory.
/// </summary>
public class Open5eBestiaryClient : IOpen5eBestiaryClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<Open5eBestiaryClient> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public Open5eBestiaryClient(HttpClient httpClient, ILogger<Open5eBestiaryClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Open5eMonsterListResponse> GetMonstersAsync(int limit = 50, int page = 1, CancellationToken ct = default)
    {
        var url = $"monsters/?limit={limit}&page={page}";
        _logger.LogInformation("Consultando Open5e Bestiary en {Url}...", url);

        try
        {
            var response = await _httpClient.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            var data = await response.Content.ReadFromJsonAsync<Open5eMonsterListResponse>(JsonOptions, ct);
            return data ?? new Open5eMonsterListResponse();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al consultar monstruos de Open5e en {Url}", url);
            throw;
        }
    }
}
