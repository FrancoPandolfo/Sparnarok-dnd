using Sparnarok.Api.Application.DTOs;

namespace Sparnarok.Api.Application.Interfaces;

/// <summary>
/// Contrato del servicio de búsqueda inteligente y Lazy Ingestion bajo demanda de criaturas.
/// Busca primero en la base de datos local y, si hay pocos o nulos resultados,
/// consulta transparentemente la API externa de Open5e, mapea las criaturas a DTOs tácticos
/// y las persiste asíncronamente en PostgreSQL para futuras simulaciones.
/// </summary>
public interface ICreatureSearchService
{
    /// <summary>
    /// Ejecuta la búsqueda de criaturas con proxy transparente a Open5e y persistencia silenciosa.
    /// </summary>
    /// <param name="term">Término de búsqueda (nombre o fragmento).</param>
    /// <param name="currentUserId">ID del usuario autenticado actual (para RLS de héroes custom).</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Lista unificada de criaturas coincidentes disponibles para el usuario y compatibles con Monte Carlo.</returns>
    Task<IReadOnlyList<CreatureResponseDto>> SearchCreaturesAsync(
        string term,
        Guid? currentUserId = null,
        CancellationToken ct = default);
}
