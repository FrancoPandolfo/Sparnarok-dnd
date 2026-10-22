using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sparnarok.Api.Application.DTOs;
using Sparnarok.Api.Application.Extensions;
using Sparnarok.Api.Application.Interfaces;
using Sparnarok.Api.Domain.Entities;

namespace Sparnarok.Api.Controllers;

/// <summary>
/// Gestiona la persistencia y operaciones CRUD de Partys de aventureros por usuario.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class PartiesController : ControllerBase
{
    private readonly IPartyRepository _partyRepository;
    private readonly ICreatureRepository _creatureRepository;
    private readonly IValidator<CreatePartyRequestDto> _partyValidator;

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="PartiesController"/>.
    /// </summary>
    public PartiesController(
        IPartyRepository partyRepository,
        ICreatureRepository creatureRepository,
        IValidator<CreatePartyRequestDto> partyValidator)
    {
        _partyRepository = partyRepository;
        _creatureRepository = creatureRepository;
        _partyValidator = partyValidator;
    }

    /// <summary>
    /// Lista todas las Partys guardadas pertenecientes al usuario autenticado.
    /// </summary>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Retorna la lista de partys del usuario.</response>
    /// <response code="401">Si no está autenticado.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PartyResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<PartyResponseDto>>> GetAll(CancellationToken ct)
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

        var parties = await _partyRepository.GetAllByUserIdAsync(userId.Value, ct);
        var dtos = parties.Select(PartyResponseDto.FromEntity).ToList();
        return Ok(dtos);
    }

    /// <summary>
    /// Obtiene una Party por su identificador único asegurando que pertenezca al usuario autenticado.
    /// </summary>
    /// <param name="id">Identificador único de la Party.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Retorna la Party encontrada con sus integrantes.</response>
    /// <response code="403">Si la Party pertenece a otro usuario.</response>
    /// <response code="404">Si la Party no existe.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PartyResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PartyResponseDto>> GetById(Guid id, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        var party = await _partyRepository.GetByIdAsync(id, ct);
        if (party is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Party no encontrada",
                Status = StatusCodes.Status404NotFound,
                Detail = $"No se encontró la Party con ID '{id}'."
            });
        }

        if (party.OwnerUserId != userId.Value)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Title = "Acceso denegado",
                Status = StatusCodes.Status403Forbidden,
                Detail = "No tiene autorización para visualizar una Party que pertenece a otro usuario."
            });
        }

        return Ok(PartyResponseDto.FromEntity(party));
    }

    /// <summary>
    /// Crea una nueva Party de héroes asignada al usuario autenticado.
    /// </summary>
    /// <param name="request">Datos de la Party y lista de IDs de héroes integrantes.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="201">Retorna la Party creada exitosamente.</response>
    /// <response code="400">Si los datos o integrantes no son válidos (ej. no son PCs).</response>
    [HttpPost]
    [ProducesResponseType(typeof(PartyResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PartyResponseDto>> Create(
        [FromBody] CreatePartyRequestDto request,
        CancellationToken ct)
    {
        var validationResult = await _partyValidator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validationResult.ToDictionary()));
        }

        var userId = User.GetUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        var party = new Party(Guid.NewGuid(), request.Name, userId.Value);

        if (request.MemberCreatureIds != null && request.MemberCreatureIds.Count > 0)
        {
            foreach (var creatureId in request.MemberCreatureIds)
            {
                var creature = await _creatureRepository.GetByIdAsync(creatureId, userId, ct);
                if (creature is null)
                {
                    return BadRequest(new ProblemDetails
                    {
                        Title = "Criatura no encontrada",
                        Status = StatusCodes.Status400BadRequest,
                        Detail = $"No se encontró la criatura con ID '{creatureId}' para agregar a la Party."
                    });
                }

                if (!creature.IsPlayerCharacter)
                {
                    return BadRequest(new ProblemDetails
                    {
                        Title = "Integrante no válido",
                        Status = StatusCodes.Status400BadRequest,
                        Detail = $"La criatura '{creature.Name}' no es un Personaje de Jugador (PC). Solo los héroes pueden conformar una Party."
                    });
                }

                party.AddMember(creature);
            }
        }

        await _partyRepository.AddAsync(party, ct);
        await _partyRepository.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = party.Id }, PartyResponseDto.FromEntity(party));
    }

    /// <summary>
    /// Actualiza el nombre y miembros de una Party existente.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(PartyResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PartyResponseDto>> Update(
        Guid id,
        [FromBody] CreatePartyRequestDto request,
        CancellationToken ct)
    {
        var validationResult = await _partyValidator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validationResult.ToDictionary()));
        }

        var userId = User.GetUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        var party = await _partyRepository.GetByIdAsync(id, ct);
        if (party is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Party no encontrada",
                Status = StatusCodes.Status404NotFound,
                Detail = $"No se encontró la Party con ID '{id}'."
            });
        }

        if (party.OwnerUserId != userId.Value)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Title = "Acceso denegado",
                Status = StatusCodes.Status403Forbidden,
                Detail = "No tiene autorización para modificar una Party que pertenece a otro usuario."
            });
        }

        party.SetName(request.Name);
        party.ClearMembers();

        if (request.MemberCreatureIds != null && request.MemberCreatureIds.Count > 0)
        {
            foreach (var creatureId in request.MemberCreatureIds)
            {
                var creature = await _creatureRepository.GetByIdAsync(creatureId, userId, ct);
                if (creature is null)
                {
                    return BadRequest(new ProblemDetails
                    {
                        Title = "Criatura no encontrada",
                        Status = StatusCodes.Status400BadRequest,
                        Detail = $"No se encontró la criatura con ID '{creatureId}' para agregar a la Party."
                    });
                }

                if (!creature.IsPlayerCharacter)
                {
                    return BadRequest(new ProblemDetails
                    {
                        Title = "Integrante no válido",
                        Status = StatusCodes.Status400BadRequest,
                        Detail = $"La criatura '{creature.Name}' no es un Personaje de Jugador (PC)."
                    });
                }

                party.AddMember(creature);
            }
        }

        await _partyRepository.UpdateAsync(party, ct);
        await _partyRepository.SaveChangesAsync(ct);

        return Ok(PartyResponseDto.FromEntity(party));
    }

    /// <summary>
    /// Elimina una Party perteneciente al usuario autenticado.
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

        var party = await _partyRepository.GetByIdAsync(id, ct);
        if (party is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Party no encontrada",
                Status = StatusCodes.Status404NotFound,
                Detail = $"No se encontró la Party con ID '{id}'."
            });
        }

        if (party.OwnerUserId != userId.Value)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Title = "Acceso denegado",
                Status = StatusCodes.Status403Forbidden,
                Detail = "No tiene autorización para eliminar una Party que pertenece a otro usuario."
            });
        }

        await _partyRepository.DeleteAsync(party, ct);
        await _partyRepository.SaveChangesAsync(ct);

        return NoContent();
    }
}
