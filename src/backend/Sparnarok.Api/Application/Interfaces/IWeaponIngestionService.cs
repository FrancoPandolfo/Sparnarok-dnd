using Sparnarok.Api.Application.DTOs;

namespace Sparnarok.Api.Application.Interfaces;

/// <summary>
/// Contrato del servicio de ingesta y reconciliación masiva (Bulk Upsert) de armas.
/// </summary>
public interface IWeaponIngestionService
{
    /// <summary>
    /// Procesa un archivo CSV de armas, valida cada registro y realiza un Upsert en la base de datos.
    /// </summary>
    /// <param name="csvStream">Flujo del archivo CSV subido.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Estadísticas del procesamiento con conteo de insertados, actualizados y errores.</returns>
    Task<IngestionResultDto> IngestWeaponsAsync(Stream csvStream, CancellationToken cancellationToken = default);
}
