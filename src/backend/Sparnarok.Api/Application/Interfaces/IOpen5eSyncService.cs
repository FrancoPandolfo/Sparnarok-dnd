using Sparnarok.Api.Application.DTOs;
using Sparnarok.Api.Domain.Entities;

namespace Sparnarok.Api.Application.Interfaces;

/// <summary>
/// Servicio de sincronización e ingesta masiva de criaturas desde el Bestiario de Open5e hacia PostgreSQL.
/// </summary>
public interface IOpen5eSyncService
{
    /// <summary>
    /// Sincroniza la primera página de monstruos desde la API de Open5e hacia la base de datos local.
    /// </summary>
    /// <param name="limit">Cantidad máxima de monstruos a sincronizar (por defecto 50).</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Resumen métrico de la operación de sincronización (procesados, insertados, actualizados y errores).</returns>
    Task<IngestionResultDto> SyncMonstersAsync(int limit = 50, CancellationToken ct = default);

    /// <summary>
    /// Mapea un DTO de monstruo proveniente de Open5e a una entidad de dominio <see cref="Creature"/>.
    /// </summary>
    /// <param name="dto">DTO crudo de Open5e.</param>
    /// <returns>Entidad agregada <see cref="Creature"/> completamente inicializada.</returns>
    Creature MapToCreature(Open5eMonsterDto dto);
}
