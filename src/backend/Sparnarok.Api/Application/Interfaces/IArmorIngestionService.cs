using Sparnarok.Api.Application.DTOs;

namespace Sparnarok.Api.Application.Interfaces;

/// <summary>
/// Contrato del servicio de ingesta masiva y Bulk Upsert para el catálogo de armaduras y escudos de D&amp;D 5e (2024).
/// </summary>
public interface IArmorIngestionService
{
    /// <summary>
    /// Procesa el streaming de un archivo CSV de armaduras, validando cabeceras, valores numéricos y categorías,
    /// persistiendo las nuevas armaduras o actualizando de forma idempotente las existentes.
    /// </summary>
    /// <param name="csvStream">Flujo de datos binario del archivo CSV.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Métricas del proceso de ingesta masiva.</returns>
    Task<IngestionResultDto> IngestArmorsAsync(
        Stream csvStream,
        CancellationToken cancellationToken = default);
}
