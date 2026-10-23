using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sparnarok.Api.Application.DTOs;
using Sparnarok.Api.Application.Extensions;
using Sparnarok.Api.Application.Interfaces;
using Sparnarok.Api.Domain.Entities;

namespace Sparnarok.Api.Controllers;

/// <summary>
/// Gestiona las notas privadas de sesión y apuntes personales de campaña para cualquier integrante autenticado de una Party,
/// con aislamiento estricto por usuario (Logical Row-Level Security: OwnerUserId == CurrentUserId).
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/parties/{partyId:guid}/notes")]
[Produces("application/json")]
public class SessionNotesController : ControllerBase
{
    private readonly IPartyRepository _partyRepository;
    private readonly ISessionNoteRepository _noteRepository;
    private readonly IValidator<CreateSessionNoteRequest> _createValidator;
    private readonly IValidator<UpdateSessionNoteRequest> _updateValidator;

    public SessionNotesController(
        IPartyRepository partyRepository,
        ISessionNoteRepository noteRepository,
        IValidator<CreateSessionNoteRequest> createValidator,
        IValidator<UpdateSessionNoteRequest> updateValidator)
    {
        _partyRepository = partyRepository;
        _noteRepository = noteRepository;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    /// <summary>
    /// Lista todas las notas de sesión privadas del usuario autenticado para la Party especificada (Fase 3.7).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<SessionNoteResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<SessionNoteResponseDto>>> GetAll(Guid partyId, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue) return Unauthorized();

        var party = await _partyRepository.GetByIdAsync(partyId, ct);
        if (party is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Party no encontrada",
                Status = StatusCodes.Status404NotFound,
                Detail = $"No se encontró la Party con ID '{partyId}'."
            });
        }

        // Aislamiento estricto (Logical Row-Level Security): sólo se retornan las notas del usuario actual
        var notes = await _noteRepository.GetAllByPartyAndOwnerAsync(partyId, userId.Value, ct);
        return Ok(notes.Select(SessionNoteResponseDto.FromEntity).ToList());
    }

    /// <summary>
    /// Obtiene una nota de sesión privada por su identificador.
    /// Exclusivo para el autor y propietario de la nota.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(SessionNoteResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SessionNoteResponseDto>> GetById(Guid partyId, Guid id, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue) return Unauthorized();

        var party = await _partyRepository.GetByIdAsync(partyId, ct);
        if (party is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Party no encontrada",
                Status = StatusCodes.Status404NotFound,
                Detail = $"No se encontró la Party con ID '{partyId}'."
            });
        }

        var note = await _noteRepository.GetByIdAsync(id, ct);
        if (note is null || note.PartyId != partyId)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Nota no encontrada",
                Status = StatusCodes.Status404NotFound,
                Detail = $"No se encontró la nota con ID '{id}' para esta Party."
            });
        }

        if (note.OwnerUserId != userId.Value)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Title = "Acceso denegado",
                Status = StatusCodes.Status403Forbidden,
                Detail = "No tiene autorización para visualizar las notas privadas de otro integrante del grupo."
            });
        }

        return Ok(SessionNoteResponseDto.FromEntity(note));
    }

    /// <summary>
    /// Crea una nueva nota de sesión privada para la Party.
    /// Accesible tanto para el Dungeon Master como para los Jugadores (Fase 3.7).
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(SessionNoteResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SessionNoteResponseDto>> Create(
        Guid partyId,
        [FromBody] CreateSessionNoteRequest request,
        CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue) return Unauthorized();

        var validationResult = await _createValidator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validationResult.ToDictionary()));
        }

        var party = await _partyRepository.GetByIdAsync(partyId, ct);
        if (party is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Party no encontrada",
                Status = StatusCodes.Status404NotFound,
                Detail = $"No se encontró la Party con ID '{partyId}'."
            });
        }

        // Cada integrante puede redactar sus propias notas privadas
        var note = new SessionNote(Guid.NewGuid(), partyId, userId.Value, request.Title, request.Content);
        await _noteRepository.AddAsync(note, ct);
        await _noteRepository.SaveChangesAsync(ct);

        var dto = SessionNoteResponseDto.FromEntity(note);
        return CreatedAtAction(nameof(GetById), new { partyId, id = note.Id }, dto);
    }

    /// <summary>
    /// Modifica una nota de sesión privada existente.
    /// Exclusivo para el usuario autor de la nota.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(SessionNoteResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SessionNoteResponseDto>> Update(
        Guid partyId,
        Guid id,
        [FromBody] UpdateSessionNoteRequest request,
        CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue) return Unauthorized();

        var validationResult = await _updateValidator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validationResult.ToDictionary()));
        }

        var party = await _partyRepository.GetByIdAsync(partyId, ct);
        if (party is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Party no encontrada",
                Status = StatusCodes.Status404NotFound,
                Detail = $"No se encontró la Party con ID '{partyId}'."
            });
        }

        var note = await _noteRepository.GetByIdAsync(id, ct);
        if (note is null || note.PartyId != partyId)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Nota no encontrada",
                Status = StatusCodes.Status404NotFound,
                Detail = $"No se encontró la nota con ID '{id}' para esta Party."
            });
        }

        if (note.OwnerUserId != userId.Value)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Title = "Acceso denegado",
                Status = StatusCodes.Status403Forbidden,
                Detail = "No tiene autorización para modificar las notas privadas de otro integrante del grupo."
            });
        }

        note.Update(request.Title, request.Content);
        await _noteRepository.UpdateAsync(note, ct);
        await _noteRepository.SaveChangesAsync(ct);

        return Ok(SessionNoteResponseDto.FromEntity(note));
    }

    /// <summary>
    /// Elimina una nota de sesión privada.
    /// Exclusivo para el usuario autor de la nota.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid partyId, Guid id, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue) return Unauthorized();

        var party = await _partyRepository.GetByIdAsync(partyId, ct);
        if (party is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Party no encontrada",
                Status = StatusCodes.Status404NotFound,
                Detail = $"No se encontró la Party con ID '{partyId}'."
            });
        }

        var note = await _noteRepository.GetByIdAsync(id, ct);
        if (note is null || note.PartyId != partyId)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Nota no encontrada",
                Status = StatusCodes.Status404NotFound,
                Detail = $"No se encontró la nota con ID '{id}' para esta Party."
            });
        }

        if (note.OwnerUserId != userId.Value)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Title = "Acceso denegado",
                Status = StatusCodes.Status403Forbidden,
                Detail = "No tiene autorización para eliminar las notas privadas de otro integrante del grupo."
            });
        }

        await _noteRepository.DeleteAsync(note, ct);
        await _noteRepository.SaveChangesAsync(ct);

        return NoContent();
    }
}
