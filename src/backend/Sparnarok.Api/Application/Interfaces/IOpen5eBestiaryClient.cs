using Sparnarok.Api.Application.DTOs;

namespace Sparnarok.Api.Application.Interfaces;

/// <summary>
/// Cliente HTTP tipado para consultar el Códice / Bestiario externo de Open5e API v1.
/// </summary>
public interface IOpen5eBestiaryClient
{
    /// <summary>
    /// Obtiene una lista paginada de monstruos desde la API de Open5e.
    /// </summary>
    /// <param name="limit">Cantidad máxima de registros a traer (ej. 50).</param>
    /// <param name="page">Número de página solicitada.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Respuesta con total y colección de monstruos.</returns>
    Task<Open5eMonsterListResponse> GetMonstersAsync(int limit = 50, int page = 1, CancellationToken ct = default);

    /// <summary>
    /// Realiza una búsqueda por término en el Bestiario de Open5e (Lazy Ingestion proxy).
    /// </summary>
    /// <param name="search">Término de búsqueda (ej. 'dragon', 'goblin').</param>
    /// <param name="limit">Cantidad máxima de resultados solicitados.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Colección de monstruos coincidentes devueltos por Open5e.</returns>
    Task<Open5eMonsterListResponse> SearchMonstersAsync(string search, int limit = 20, CancellationToken ct = default);
}
