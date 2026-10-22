using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sparnarok.Api.Application.DTOs;
using Sparnarok.Api.Application.Extensions;
using Sparnarok.Api.Application.Interfaces;
using Sparnarok.Api.Domain.Entities;

namespace Sparnarok.Api.Controllers;

/// <summary>
/// Gestiona la persistencia y operaciones CRUD de Encuentros tácticos y monstruos por usuario.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class EncountersController : ControllerBase
{
    private readonly IEncounterRepository _encounterRepository;
    private readonly ICreatureRepository _creatureRepository;
    private readonly IValidator<CreateEncounterRequestDto> _encounterValidator;

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="EncountersController"/>.
    /// </summary>
    public EncountersController(
        IEncounterRepository encounterRepository,
        ICreatureRepository creatureRepository,
        IValidator<CreateEncounterRequestDto> encounterValidator)
    {
        _encounterRepository = encounterRepository;
        _creatureRepository = creatureRepository;
        _encounterValidator = encounterValidator;
    }

    /// <summary>
    /// Lista todos los Encuentros guardados pertenecientes al usuario autenticado.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<EncounterResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<EncounterResponseDto>>> GetAll(CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue)
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "No autorizado",
                Detail = "No se pudo identificar al usuario en el token de autorización.",
                Status = StatusCodes.Status401Unauthorized
            });
        }

        var encounters = await _encounterRepository.GetAllByUserIdAsync(userId.Value, ct);
        var dtos = encounters.Select(EncounterResponseDto.FromEntity).ToList();
        return Ok(dtos);
    }

    /// <summary>
    /// Obtiene un Encuentro por su identificador único asegurando que pertenezca al usuario autenticado.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(EncounterResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EncounterResponseDto>> GetById(Guid id, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        var encounter = await _encounterRepository.GetByIdAsync(id, ct);
        if (encounter is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Encuentro no encontrado",
                Status = StatusCodes.Status404NotFound,
                Detail = $"No se encontró el Encuentro con ID '{id}'."
            });
        }

        if (encounter.OwnerUserId != userId.Value)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Title = "Acceso denegado",
                Status = StatusCodes.Status403Forbidden,
                Detail = "No tiene autorización para visualizar un Encuentro que pertenece a otro usuario."
            });
        }

        return Ok(EncounterResponseDto.FromEntity(encounter));
    }

    /// <summary>
    /// Crea un nuevo Encuentro táctico asignado al usuario autenticado.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(EncounterResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EncounterResponseDto>> Create(
        [FromBody] CreateEncounterRequestDto request,
        CancellationToken ct)
    {
        var validationResult = await _encounterValidator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validationResult.ToDictionary()));
        }

        var userId = User.GetUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        var encounter = new Encounter(Guid.NewGuid(), request.Name, userId.Value, request.DifficultyNotes);

        if (request.CreatureIds != null && request.CreatureIds.Count > 0)
        {
            foreach (var creatureId in request.CreatureIds)
            {
                var creature = await _creatureRepository.GetByIdAsync(creatureId, userId, ct);
                if (creature is null)
                {
                    return BadRequest(new ProblemDetails
                    {
                        Title = "Criatura no encontrada",
                        Status = StatusCodes.Status400BadRequest,
                        Detail = $"No se encontró la criatura con ID '{creatureId}' para agregar al Encuentro."
                    });
                }

                encounter.AddCreature(creature);
            }
        }

        await _encounterRepository.AddAsync(encounter, ct);
        await _encounterRepository.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = encounter.Id }, EncounterResponseDto.FromEntity(encounter));
    }

    /// <summary>
    /// Actualiza un Encuentro táctico existente.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(EncounterResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EncounterResponseDto>> Update(
        Guid id,
        [FromBody] CreateEncounterRequestDto request,
        CancellationToken ct)
    {
        var validationResult = await _encounterValidator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validationResult.ToDictionary()));
        }

        var userId = User.GetUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        var encounter = await _encounterRepository.GetByIdAsync(id, ct);
        if (encounter is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Encuentro no encontrado",
                Status = StatusCodes.Status404NotFound,
                Detail = $"No se encontró el Encuentro con ID '{id}'."
            });
        }

        if (encounter.OwnerUserId != userId.Value)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Title = "Acceso denegado",
                Status = StatusCodes.Status403Forbidden,
                Detail = "No tiene autorización para modificar un Encuentro que pertenece a otro usuario."
            });
        }

        encounter.SetName(request.Name);
        encounter.SetDifficultyNotes(request.DifficultyNotes);
        encounter.ClearCreatures();

        if (request.CreatureIds != null && request.CreatureIds.Count > 0)
        {
            foreach (var creatureId in request.CreatureIds)
            {
                var creature = await _creatureRepository.GetByIdAsync(creatureId, userId, ct);
                if (creature is null)
                {
                    return BadRequest(new ProblemDetails
                    {
                        Title = "Criatura no encontrada",
                        Status = StatusCodes.Status400BadRequest,
                        Detail = $"No se encontró la criatura con ID '{creatureId}' para agregar al Encuentro."
                    });
                }

                encounter.AddCreature(creature);
            }
        }

        await _encounterRepository.UpdateAsync(encounter, ct);
        await _encounterRepository.SaveChangesAsync(ct);

        return Ok(EncounterResponseDto.FromEntity(encounter));
    }

    /// <summary>
    /// Agrega un monstruo o criatura a un Encuentro táctico, verificando autorización a nivel de recurso.
    /// </summary>
    /// <param name="id">ID del Encuentro al que se desea agregar la criatura.</param>
    /// <param name="creatureId">ID de la criatura que se desea agregar.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Retorna el Encuentro actualizado.</response>
    /// <response code="403">Si el Encuentro pertenece a otro usuario.</response>
    /// <response code="404">Si el Encuentro o la criatura no existen.</response>
    [HttpPost("{id:guid}/creatures/{creatureId:guid}")]
    [ProducesResponseType(typeof(EncounterResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EncounterResponseDto>> AddCreature(
        Guid id,
        Guid creatureId,
        CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        var encounter = await _encounterRepository.GetByIdAsync(id, ct);
        if (encounter is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Encuentro no encontrado",
                Status = StatusCodes.Status404NotFound,
                Detail = $"No se encontró el Encuentro con ID '{id}'."
            });
        }

        // Verificación de autorización a nivel de recursos
        if (encounter.OwnerUserId != userId.Value)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Title = "Acceso denegado",
                Status = StatusCodes.Status403Forbidden,
                Detail = "No tiene autorización para modificar un Encuentro perteneciente a otro usuario."
            });
        }

        var creature = await _creatureRepository.GetByIdAsync(creatureId, userId, ct);
        if (creature is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Criatura no encontrada",
                Status = StatusCodes.Status404NotFound,
                Detail = $"No se encontró la criatura con ID '{creatureId}'."
            });
        }

        encounter.AddCreature(creature);
        await _encounterRepository.SaveChangesAsync(ct);

        return Ok(EncounterResponseDto.FromEntity(encounter));
    }

    /// <summary>
    /// Remueve un monstruo de un Encuentro existente, verificando autorización a nivel de recurso.
    /// </summary>
    [HttpDelete("{id:guid}/creatures/{creatureId:guid}")]
    [ProducesResponseType(typeof(EncounterResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EncounterResponseDto>> RemoveCreature(
        Guid id,
        Guid creatureId,
        CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        var encounter = await _encounterRepository.GetByIdAsync(id, ct);
        if (encounter is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Encuentro no encontrado",
                Status = StatusCodes.Status404NotFound,
                Detail = $"No se encontró el Encuentro con ID '{id}'."
            });
        }

        if (encounter.OwnerUserId != userId.Value)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Title = "Acceso denegado",
                Status = StatusCodes.Status403Forbidden,
                Detail = "No tiene autorización para modificar un Encuentro perteneciente a otro usuario."
            });
        }

        encounter.RemoveCreature(creatureId);
        await _encounterRepository.SaveChangesAsync(ct);

        return Ok(EncounterResponseDto.FromEntity(encounter));
    }

    /// <summary>
    /// Elimina un Encuentro perteneciente al usuario autenticado.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        var encounter = await _encounterRepository.GetByIdAsync(id, ct);
        if (encounter is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Encuentro no encontrado",
                Status = StatusCodes.Status404NotFound,
                Detail = $"No se encontró el Encuentro con ID '{id}'."
            });
        }

        if (encounter.OwnerUserId != userId.Value)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Title = "Acceso denegado",
                Status = StatusCodes.Status403Forbidden,
                Detail = "No tiene autorización para eliminar un Encuentro que pertenece a otro usuario."
            });
        }

        await _encounterRepository.DeleteAsync(encounter, ct);
        await _encounterRepository.SaveChangesAsync(ct);

        return NoContent();
    }
}
