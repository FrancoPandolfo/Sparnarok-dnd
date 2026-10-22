using Sparnarok.Api.Application.DTOs;

namespace Sparnarok.Api.Infrastructure.External.Dnd5eApi;

/// <summary>
/// Contrato del cliente HTTP tipado para consultar el compendio oficial SRD de D&amp;D 5e (vía D&amp;D 5e API).
/// </summary>
public interface IDnd5eApiClient
{
    /// <summary>
    /// Consulta los catálogos de equipamiento y objetos mágicos de la API oficial y filtra los resultados por el término de búsqueda proporcionado.
    /// Utiliza caché en memoria para maximizar el rendimiento y evitar rate-limiting.
    /// </summary>
    /// <param name="nameQuery">Término de búsqueda parcial o completo para el nombre del objeto.</param>
    /// <param name="cancellationToken">Token de cancelación cooperativa.</param>
    /// <returns>Lista simplificada de resúmenes de objetos (Index, Name, Url).</returns>
    Task<IReadOnlyList<ReferenceItemSummaryDto>> SearchEquipmentAsync(string nameQuery, CancellationToken cancellationToken = default);

    /// <summary>
    /// Recupera los detalles técnicos completos de un objeto SRD a partir de su índice único (slug).
    /// </summary>
    /// <param name="index">Identificador único o slug del objeto en el SRD (ej. 'bag-of-holding', 'dagger').</param>
    /// <param name="cancellationToken">Token de cancelación cooperativa.</param>
    /// <returns>Detalle del objeto mapeado a un DTO compatible con StashItem, o null si no se encuentra.</returns>
    Task<ReferenceEquipmentDetailDto?> GetEquipmentByIndexAsync(string index, CancellationToken cancellationToken = default);
}
