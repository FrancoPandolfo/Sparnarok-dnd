using Microsoft.AspNetCore.Mvc;
using Sparnarok.Api.Application.DTOs;
using Sparnarok.Api.Infrastructure.External.Dnd5eApi;

namespace Sparnarok.Api.Controllers;

/// <summary>
/// Endpoint proxy para consultar el compendio oficial de equipamiento y objetos mágicos del SRD de D&amp;D 5e.
/// Integra caché en memoria para optimizar tiempos de respuesta y resiliencia ante límites de tasa de la API externa.
/// </summary>
[ApiController]
[Route("api/v1/reference/equipment")]
[Produces("application/json")]
public class ReferenceEquipmentController : ControllerBase
{
    private readonly IDnd5eApiClient _dnd5eApiClient;

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="ReferenceEquipmentController"/>.
    /// </summary>
    public ReferenceEquipmentController(IDnd5eApiClient dnd5eApiClient)
    {
        _dnd5eApiClient = dnd5eApiClient ?? throw new ArgumentNullException(nameof(dnd5eApiClient));
    }

    /// <summary>
    /// Busca equipamiento u objetos mágicos en el compendio oficial de D&amp;D 5e a partir de un término de búsqueda.
    /// </summary>
    /// <param name="name">Término de búsqueda para filtrar por nombre del objeto (opcional).</param>
    /// <param name="cancellationToken">Token de cancelación cooperativa.</param>
    /// <response code="200">Retorna la lista de objetos coincidentes en formato simplificado (Index, Name, Url).</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ReferenceItemSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ReferenceItemSummaryDto>>> SearchEquipment(
        [FromQuery] string? name,
        CancellationToken cancellationToken)
    {
        var items = await _dnd5eApiClient.SearchEquipmentAsync(name ?? string.Empty, cancellationToken);
        return Ok(items);
    }

    /// <summary>
    /// Obtiene los detalles completos de un objeto del SRD mapeados a un formato compatible con StashItem.
    /// </summary>
    /// <param name="index">Identificador único o slug del objeto en el SRD (ej. 'bag-of-holding', 'dagger').</param>
    /// <param name="cancellationToken">Token de cancelación cooperativa.</param>
    /// <response code="200">Retorna los detalles completos del objeto con peso normalizado, descripción, rareza y tipo.</response>
    /// <response code="404">Si el objeto no fue encontrado en el catálogo de equipamiento ni objetos mágicos.</response>
    [HttpGet("{index}")]
    [ProducesResponseType(typeof(ReferenceEquipmentDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReferenceEquipmentDetailDto>> GetEquipmentByIndex(
        [FromRoute] string index,
        CancellationToken cancellationToken)
    {
        var detail = await _dnd5eApiClient.GetEquipmentByIndexAsync(index, cancellationToken);
        if (detail is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Objeto no encontrado",
                Detail = $"No se encontró ningún objeto con el índice '{index}' en el compendio oficial de D&D 5e.",
                Status = StatusCodes.Status404NotFound
            });
        }

        return Ok(detail);
    }
}
