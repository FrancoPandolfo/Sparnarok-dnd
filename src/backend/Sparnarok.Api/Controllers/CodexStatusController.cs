using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
namespace Sparnarok.Api.Controllers;

/// <summary>
/// Provee información de telemetría, estado del sistema y verificación de localización (i18n).
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class CodexStatusController : ControllerBase
{
    private readonly IStringLocalizer<SharedResource> _localizer;

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="CodexStatusController"/>.
    /// </summary>
    /// <param name="localizer">Servicio de localización i18n para mensajes de sistema.</param>
    public CodexStatusController(IStringLocalizer<SharedResource> localizer)
    {
        _localizer = localizer;
    }

    /// <summary>
    /// Obtiene el estado de salud y el mensaje operativo localizado del Códice de Sparnarok.
    /// </summary>
    /// <remarks>
    /// El idioma de la respuesta se determina mediante el header HTTP `Accept-Language` (soportados: 'es', 'en').
    /// Por defecto, si no se especifica, se devolverá en español ('es').
    /// </remarks>
    /// <response code="200">Retorna el estado operativo y mensaje localizado.</response>
    [HttpGet]
    [ProducesResponseType(typeof(CodexStatusResponse), StatusCodes.Status200OK)]
    public ActionResult<CodexStatusResponse> GetStatus()
    {
        var message = _localizer["SystemReady"];
        var engineMessage = _localizer["SimulationActive"];

        return Ok(new CodexStatusResponse(
            Status: "Operational",
            Culture: CultureInfo.CurrentUICulture.Name,
            SystemMessage: message.Value,
            SimulationEngine: engineMessage.Value,
            TimestampUtc: DateTime.UtcNow
        ));
    }
}

/// <summary>
/// Modelo de datos que describe el estado operativo localizado del sistema.
/// </summary>
/// <param name="Status">Estado general del servicio.</param>
/// <param name="Culture">Cultura activa detectada (ej. 'es', 'en').</param>
/// <param name="SystemMessage">Mensaje de bienvenida localizado según la cultura solicitada.</param>
/// <param name="SimulationEngine">Estado localizado del motor de simulación.</param>
/// <param name="TimestampUtc">Estampa temporal UTC de la respuesta.</param>
public record CodexStatusResponse(
    string Status,
    string Culture,
    string SystemMessage,
    string SimulationEngine,
    DateTime TimestampUtc
);
