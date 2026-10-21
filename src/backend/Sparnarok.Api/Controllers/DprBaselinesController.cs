using Microsoft.AspNetCore.Mvc;
using Sparnarok.Api.Application.Interfaces;

namespace Sparnarok.Api.Controllers;

/// <summary>
/// Controlador para consulta de matrices de daño esperado por ronda (Expected DPR Baselines)
/// por arquetipo y nivel de D&amp;D 5e (2024).
/// </summary>
[ApiController]
[Route("api/v1/dpr-baselines")]
[Produces("application/json")]
public class DprBaselinesController : ControllerBase
{
    private readonly IDprBaselineService _dprBaselineService;

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="DprBaselinesController"/>.
    /// </summary>
    public DprBaselinesController(IDprBaselineService dprBaselineService)
    {
        _dprBaselineService = dprBaselineService ?? throw new ArgumentNullException(nameof(dprBaselineService));
    }

    /// <summary>
    /// Retorna todas las entradas del catálogo de matrices de DPR (arquetipo, nivel, DPR esperado y descripción).
    /// </summary>
    /// <response code="200">Lista completa de baselines de DPR.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<DprBaselineEntry>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<DprBaselineEntry>> GetAll()
    {
        return Ok(_dprBaselineService.GetAllBaselines());
    }

    /// <summary>
    /// Retorna la lista de nombres de arquetipos soportados (ej. 'Fighter 2H', 'Ranger Ranged', 'Paladin 2H').
    /// </summary>
    /// <response code="200">Nombres de arquetipos disponibles.</response>
    [HttpGet("archetypes")]
    [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<string>> GetArchetypes()
    {
        return Ok(_dprBaselineService.GetArchetypes());
    }

    /// <summary>
    /// Consulta el daño esperado por ronda (Expected DPR) para un arquetipo y nivel dados.
    /// </summary>
    /// <param name="archetype">Nombre del arquetipo.</param>
    /// <param name="level">Nivel del personaje (1 a 20).</param>
    /// <response code="200">DPR esperado encontrado.</response>
    /// <response code="404">Si el arquetipo o nivel no existen.</response>
    [HttpGet("calculate")]
    [ProducesResponseType(typeof(DprCalculationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<DprCalculationResponse> Calculate(
        [FromQuery] string archetype,
        [FromQuery] int level)
    {
        var dpr = _dprBaselineService.GetExpectedDpr(archetype, level);
        if (!dpr.HasValue)
        {
            return NotFound(new { message = $"No se encontró DPR baseline para el arquetipo '{archetype}' a nivel {level}." });
        }

        return Ok(new DprCalculationResponse(archetype, level, dpr.Value));
    }
}

/// <summary>
/// Respuesta del cálculo de DPR esperado para un arquetipo y nivel.
/// </summary>
public record DprCalculationResponse(string Archetype, int Level, double ExpectedDpr);
