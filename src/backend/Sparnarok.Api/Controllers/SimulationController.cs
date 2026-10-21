using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Sparnarok.Api.Application.DTOs;
using Sparnarok.Api.Application.Interfaces;

namespace Sparnarok.Api.Controllers;

/// <summary>
/// Controlador de la API de simulación matemática y análisis probabilístico de combate D&amp;D 5e (2024).
/// </summary>
[ApiController]
[Route("api/simulation")]
[Produces("application/json")]
public class SimulationController : ControllerBase
{
    private readonly IMontecarloSimulationService _montecarloService;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly ILogger<SimulationController> _logger;

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="SimulationController"/>.
    /// </summary>
    public SimulationController(
        IMontecarloSimulationService montecarloService,
        IStringLocalizer<SharedResource> localizer,
        ILogger<SimulationController> logger)
    {
        _montecarloService = montecarloService;
        _localizer = localizer;
        _logger = logger;
    }

    /// <summary>
    /// Ejecuta una simulación de combate masiva mediante el método de Monte Carlo entre dos criaturas armadas.
    /// </summary>
    /// <param name="request">Identificadores de combatientes, armas e iteraciones solicitadas (hasta 50,000).</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Distribución probabilística de Win Rates, promedio de turnos y daño por ronda (DPR).</returns>
    /// <response code="200">Simulación completada con éxito y resultados consolidados.</response>
    /// <response code="400">Parámetros inválidos o iteraciones fuera de rango permitido.</response>
    /// <response code="404">Criatura o arma especificada no encontrada en la base de datos.</response>
    [HttpPost("montecarlo")]
    [ProducesResponseType(typeof(MontecarloResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MontecarloResultDto>> RunMontecarlo(
        [FromBody] MontecarloRequestDto request,
        CancellationToken cancellationToken)
    {
        if (request.Iterations <= 0 || request.Iterations > 50000)
        {
            ModelState.AddModelError(nameof(request.Iterations), "El número de iteraciones debe ser entre 1 y 50,000.");
            return ValidationProblem(ModelState);
        }

        try
        {
            var result = await _montecarloService.SimulateAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException knf)
        {
            _logger.LogWarning(knf, "Entidad de combate no encontrada para simulación.");
            return Problem(
                title: "Entidad no encontrada",
                detail: knf.Message,
                statusCode: StatusCodes.Status404NotFound);
        }
    }
}
