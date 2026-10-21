using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Sparnarok.Api.Application.DTOs;
using Sparnarok.Api.Application.Interfaces;

namespace Sparnarok.Api.Controllers;

/// <summary>
/// Provee acceso de consulta a las propiedades de maestría de armas (Weapon Mastery) de D&amp;D 5e (2024).
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class WeaponMasteriesController : ControllerBase
{
    private readonly IWeaponMasteryRepository _repository;
    private readonly IStringLocalizer<SharedResource> _localizer;

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="WeaponMasteriesController"/>.
    /// </summary>
    public WeaponMasteriesController(
        IWeaponMasteryRepository repository,
        IStringLocalizer<SharedResource> localizer)
    {
        _repository = repository;
        _localizer = localizer;
    }

    /// <summary>
    /// Lista todas las propiedades canónicas de maestría de armas disponibles.
    /// </summary>
    /// <response code="200">Retorna el listado completo de maestrías.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<WeaponMasteryResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<WeaponMasteryResponseDto>>> GetAll(CancellationToken ct)
    {
        var masteries = await _repository.GetAllAsync(ct);
        var dtos = masteries.Select(WeaponMasteryResponseDto.FromEntity).ToList();
        return Ok(dtos);
    }

    /// <summary>
    /// Obtiene los detalles de una maestría de armas por su identificador único.
    /// </summary>
    /// <param name="id">Identificador único de la maestría.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Retorna la maestría solicitada.</response>
    /// <response code="404">Si la maestría no existe.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(WeaponMasteryResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WeaponMasteryResponseDto>> GetById(Guid id, CancellationToken ct)
    {
        var mastery = await _repository.GetByIdAsync(id, ct);
        if (mastery is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = _localizer["MasteryNotFound"],
                Status = StatusCodes.Status404NotFound,
                Detail = $"No se encontró la maestría con id '{id}'."
            });
        }

        return Ok(WeaponMasteryResponseDto.FromEntity(mastery));
    }
}
