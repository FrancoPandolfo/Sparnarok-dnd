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
    private readonly IValidator<CreateStashItemRequest> _stashItemValidator;
    private readonly IValidator<UpdatePartyFundsRequest> _fundsValidator;
    private readonly IValidator<QueueCraftingItemRequest> _craftingValidator;
    private readonly IStashNotificationService _notificationService;

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="PartiesController"/>.
    /// </summary>
    public PartiesController(
        IPartyRepository partyRepository,
        ICreatureRepository creatureRepository,
        IValidator<CreatePartyRequestDto> partyValidator,
        IValidator<CreateStashItemRequest> stashItemValidator,
        IValidator<UpdatePartyFundsRequest> fundsValidator,
        IValidator<QueueCraftingItemRequest> craftingValidator,
        IStashNotificationService notificationService)
    {
        _partyRepository = partyRepository;
        _creatureRepository = creatureRepository;
        _partyValidator = partyValidator;
        _stashItemValidator = stashItemValidator;
        _fundsValidator = fundsValidator;
        _craftingValidator = craftingValidator;
        _notificationService = notificationService;
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

        if (party.OwnerUserId != userId.Value && !party.PartyMembers.Any(pm => pm.UserId == userId.Value))
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

        // Registrar membresía inicial del creador de la Party (Fase 3.7)
        var creatorRole = string.IsNullOrWhiteSpace(request.Role) ? "DungeonMaster" : request.Role.Trim();
        var creatorName = string.IsNullOrWhiteSpace(request.InGameName)
            ? User.GetDisplayName()
            : request.InGameName.Trim();
        party.AddPartyMember(new PartyMember(Guid.NewGuid(), party.Id, userId.Value, creatorRole, creatorName));

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
    /// Obtiene los detalles previos de invitación a una Party mediante su token para onboarding.
    /// </summary>
    /// <param name="token">Token único de invitación.</param>
    /// <param name="ct">Token de cancelación.</param>
    [HttpGet("/api/parties/join/{token}")]
    [HttpGet("join/{token}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PartyInviteDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PartyInviteDetailsDto>> GetInviteDetails(string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return NotFound(new ProblemDetails
            {
                Title = "Invitación no válida",
                Status = StatusCodes.Status404NotFound,
                Detail = "El token de invitación no puede estar vacío."
            });
        }

        var party = await _partyRepository.GetByInviteTokenAsync(token.Trim(), ct);
        if (party is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Invitación no encontrada",
                Status = StatusCodes.Status404NotFound,
                Detail = "No se encontró ninguna Party asociada al enlace o token de invitación proporcionado."
            });
        }

        return Ok(new PartyInviteDetailsDto(
            party.Id,
            party.Name,
            party.InviteToken,
            party.PartyMembers.Count
        ));
    }

    /// <summary>
    /// Une al usuario autenticado a una Party mediante un token de invitación válido,
    /// requiriendo la definición de su rol contextual y nombre en partida (Fase 3.7 Onboarding).
    /// </summary>
    /// <param name="token">Token único de invitación.</param>
    /// <param name="request">Rol y nombre dentro de la partida.</param>
    /// <param name="ct">Token de cancelación.</param>
    [HttpPost("/api/parties/join/{token}")]
    [HttpPost("join/{token}")]
    [ProducesResponseType(typeof(PartyResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PartyResponseDto>> JoinByToken(
        string token,
        [FromBody] JoinPartyRequestDto request,
        CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue) return Unauthorized();

        if (request is null)
        {
            return BadRequest(new ProblemDetails { Title = "Solicitud inválida", Status = StatusCodes.Status400BadRequest });
        }

        if (string.IsNullOrWhiteSpace(request.Role) || (request.Role != "DungeonMaster" && request.Role != "Player"))
        {
            ModelState.AddModelError(nameof(request.Role), "El rol es obligatorio y debe ser 'DungeonMaster' o 'Player'.");
        }

        if (string.IsNullOrWhiteSpace(request.InGameName))
        {
            ModelState.AddModelError(nameof(request.InGameName), "El nombre dentro de la partida (InGameName) es obligatorio.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var party = await _partyRepository.GetByInviteTokenAsync(token.Trim(), ct);
        if (party is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Invitación no encontrada o expirada",
                Status = StatusCodes.Status404NotFound,
                Detail = "No se encontró ninguna Party activa asociada a este token de invitación."
            });
        }

        var existingMember = party.PartyMembers.FirstOrDefault(m => m.UserId == userId.Value);
        if (existingMember != null)
        {
            existingMember.SetRole(request.Role.Trim());
            existingMember.SetInGameName(request.InGameName.Trim());
        }
        else
        {
            var newMember = new PartyMember(
                Guid.NewGuid(),
                party.Id,
                userId.Value,
                request.Role.Trim(),
                request.InGameName.Trim()
            );
            party.AddPartyMember(newMember);
            await _partyRepository.AddPartyMemberAsync(newMember, ct);
        }

        await _partyRepository.SaveChangesAsync(ct);
        return Ok(PartyResponseDto.FromEntity(party));
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

    /// <summary>
    /// Consulta el inventario, fondos monetarios y peso total del alijo compartido (Collaborative Party Stash) de la Party.
    /// </summary>
    /// <param name="id">Identificador único de la Party.</param>
    /// <param name="ct">Token de cancelación cooperativa.</param>
    /// <response code="200">Retorna los ítems del alijo, fondos y peso acumulado.</response>
    /// <response code="401">Si no está autenticado.</response>
    /// <response code="403">Si la Party pertenece a otro usuario.</response>
    /// <response code="404">Si la Party no existe.</response>
    [HttpGet("{id:guid}/stash")]
    [ProducesResponseType(typeof(PartyStashResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PartyStashResponseDto>> GetStash(Guid id, CancellationToken ct)
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
                Detail = "No tiene autorización para consultar el Stash de una Party que pertenece a otro usuario."
            });
        }

        var stashItemsDto = party.StashItems.Select(StashItemResponseDto.FromEntity).ToList();
        var fundsDto = PartyFundDto.FromEntity(party);
        var craftingQueue = (await _partyRepository.GetCraftingQueueAsync(id, ct))
            .Select(CraftingQueueItemDto.FromEntity)
            .ToList();

        return Ok(new PartyStashResponseDto(party.Id, party.Name, party.TotalStashWeight, stashItemsDto, fundsDto, craftingQueue));
    }

    /// <summary>
    /// Añade un nuevo objeto al alijo compartido de la Party y emite un evento en tiempo real.
    /// </summary>
    /// <param name="id">Identificador único de la Party.</param>
    /// <param name="request">Datos del objeto a agregar (oficial SRD o Homebrew).</param>
    /// <param name="ct">Token de cancelación cooperativa.</param>
    /// <response code="201">Retorna el objeto creado con su ID asignado.</response>
    /// <response code="400">Si los datos de validación fallan.</response>
    /// <response code="401">Si no está autenticado.</response>
    /// <response code="403">Si la Party pertenece a otro usuario.</response>
    /// <response code="404">Si la Party no existe.</response>
    [HttpPost("{id:guid}/stash")]
    [ProducesResponseType(typeof(StashItemResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StashItemResponseDto>> AddStashItem(
        Guid id,
        [FromBody] CreateStashItemRequest request,
        CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        var validationResult = await _stashItemValidator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validationResult.ToDictionary()));
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
                Detail = "No tiene autorización para modificar el Stash de una Party que pertenece a otro usuario."
            });
        }

        var item = new StashItem(
            Guid.NewGuid(),
            party.Id,
            request.Name,
            request.Quantity,
            request.Weight,
            request.Description ?? string.Empty,
            request.Rarity ?? "Common",
            request.ItemType ?? "Adventuring Gear",
            request.IsOfficialSrd,
            request.ReferenceIndex,
            request.AssignedCreatureId
        );

        await _partyRepository.AddStashItemAsync(item, ct);

        var log = new StashActivityLog(
            Guid.NewGuid(),
            party.Id,
            userId.Value,
            User.GetDisplayName() ?? "Usuario",
            "ItemAdded",
            $"Añadido {item.Quantity}x {item.Name} al alijo compartido."
        );
        await _partyRepository.AddActivityLogAsync(log, ct);
        await _partyRepository.SaveChangesAsync(ct);

        var dto = StashItemResponseDto.FromEntity(item);
        await _notificationService.NotifyItemAddedAsync(party.Id, dto, ct);
        await _notificationService.NotifyActivityLoggedAsync(party.Id, StashActivityLogDto.FromEntity(log), ct);

        return CreatedAtAction(nameof(GetStash), new { id = party.Id }, dto);
    }

    /// <summary>
    /// Elimina un objeto del alijo compartido de la Party y emite un evento en tiempo real.
    /// </summary>
    /// <param name="id">Identificador único de la Party.</param>
    /// <param name="itemId">Identificador del objeto a remover.</param>
    /// <param name="ct">Token de cancelación cooperativa.</param>
    /// <response code="204">Si el objeto fue eliminado exitosamente.</response>
    /// <response code="401">Si no está autenticado.</response>
    /// <response code="403">Si la Party pertenece a otro usuario.</response>
    /// <response code="404">Si la Party o el objeto no existen.</response>
    [HttpDelete("{id:guid}/stash/{itemId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteStashItem(Guid id, Guid itemId, CancellationToken ct)
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
                Detail = "No tiene autorización para modificar el Stash de una Party que pertenece a otro usuario."
            });
        }

        var item = party.StashItems.FirstOrDefault(i => i.Id == itemId);
        if (item is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Objeto no encontrado",
                Status = StatusCodes.Status404NotFound,
                Detail = $"No se encontró el objeto con ID '{itemId}' en el alijo de la Party."
            });
        }

        await _partyRepository.RemoveStashItemAsync(item, ct);

        var log = new StashActivityLog(
            Guid.NewGuid(),
            party.Id,
            userId.Value,
            User.GetDisplayName() ?? "Usuario",
            "ItemDeleted",
            $"Eliminado {item.Name} del alijo compartido."
        );
        await _partyRepository.AddActivityLogAsync(log, ct);
        await _partyRepository.SaveChangesAsync(ct);

        await _notificationService.NotifyItemDeletedAsync(party.Id, itemId, ct);
        await _notificationService.NotifyActivityLoggedAsync(party.Id, StashActivityLogDto.FromEntity(log), ct);

        return NoContent();
    }

    /// <summary>
    /// Consulta los fondos monetarios (Party Fund) de la Party.
    /// </summary>
    [HttpGet("{id:guid}/funds")]
    [ProducesResponseType(typeof(PartyFundDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PartyFundDto>> GetFunds(Guid id, CancellationToken ct)
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
                Detail = "No tiene autorización para ver los fondos de esta Party."
            });
        }

        return Ok(PartyFundDto.FromEntity(party));
    }

    /// <summary>
    /// Actualiza las monedas del tesoro compartido (Party Fund) de la Party y emite un evento en tiempo real.
    /// </summary>
    [HttpPut("{id:guid}/funds")]
    [ProducesResponseType(typeof(PartyFundDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PartyFundDto>> UpdateFunds(
        Guid id,
        [FromBody] UpdatePartyFundsRequest request,
        CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        var validationResult = await _fundsValidator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validationResult.ToDictionary()));
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
                Detail = "No tiene autorización para modificar los fondos de esta Party."
            });
        }

        party.UpdateFunds(request.CopperPieces, request.SilverPieces, request.ElectrumPieces, request.GoldPieces, request.PlatinumPieces);
        await _partyRepository.UpdateAsync(party, ct);

        var log = new StashActivityLog(
            Guid.NewGuid(),
            party.Id,
            userId.Value,
            User.GetDisplayName() ?? "Usuario",
            "FundsUpdated",
            $"Fondos actualizados: {party.GoldPieces} PO, {party.SilverPieces} PP, {party.CopperPieces} PC. Total: {party.TotalGoldEquivalent:F1} PO."
        );
        await _partyRepository.AddActivityLogAsync(log, ct);
        await _partyRepository.SaveChangesAsync(ct);

        var dto = PartyFundDto.FromEntity(party);
        await _notificationService.NotifyFundsUpdatedAsync(party.Id, dto, ct);
        await _notificationService.NotifyActivityLoggedAsync(party.Id, StashActivityLogDto.FromEntity(log), ct);

        return Ok(dto);
    }

    /// <summary>
    /// Consulta la cola de fabricación (Downtime Crafting) de la Party.
    /// </summary>
    [HttpGet("{id:guid}/crafting")]
    [ProducesResponseType(typeof(IReadOnlyList<CraftingQueueItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<CraftingQueueItemDto>>> GetCraftingQueue(Guid id, CancellationToken ct)
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
                Detail = "No tiene autorización para consultar la cola de fabricación de esta Party."
            });
        }

        var queue = await _partyRepository.GetCraftingQueueAsync(id, ct);
        return Ok(queue.Select(CraftingQueueItemDto.FromEntity).ToList());
    }

    /// <summary>
    /// Encola un nuevo objeto para su fabricación en downtime, descontando automáticamente el oro del Party Fund.
    /// </summary>
    [HttpPost("{id:guid}/crafting")]
    [ProducesResponseType(typeof(CraftingQueueItemDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CraftingQueueItemDto>> QueueCraftingItem(
        Guid id,
        [FromBody] QueueCraftingItemRequest request,
        CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        var validationResult = await _craftingValidator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validationResult.ToDictionary()));
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
                Detail = "No tiene autorización para encolar fabricación en esta Party."
            });
        }

        if (party.GoldPieces < request.GoldCost)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Fondos insuficientes",
                Status = StatusCodes.Status400BadRequest,
                Detail = $"El alijo cuenta con {party.GoldPieces:F1} PO pero la fabricación requiere {request.GoldCost:F1} PO."
            });
        }

        // Descontar oro del Party Fund
        party.DeductGold(request.GoldCost);
        await _partyRepository.UpdateAsync(party, ct);

        var item = new CraftingQueueItem(
            Guid.NewGuid(),
            party.Id,
            request.ItemName,
            request.ItemType ?? "Adventuring Gear",
            request.Rarity ?? "Common",
            request.GoldCost,
            request.DaysRequired,
            request.Weight,
            request.Description ?? string.Empty,
            request.IsOfficialSrd,
            request.ReferenceIndex,
            request.CrafterName
        );

        await _partyRepository.AddCraftingItemAsync(item, ct);

        var log = new StashActivityLog(
            Guid.NewGuid(),
            party.Id,
            userId.Value,
            User.GetDisplayName() ?? "Usuario",
            "CraftingStarted",
            $"Iniciada fabricación de '{item.ItemName}' ({item.DaysRequired} días, -{item.GoldCost:F1} PO)."
        );
        await _partyRepository.AddActivityLogAsync(log, ct);
        await _partyRepository.SaveChangesAsync(ct);

        var itemDto = CraftingQueueItemDto.FromEntity(item);
        var fundsDto = PartyFundDto.FromEntity(party);

        var updatedQueue = (await _partyRepository.GetCraftingQueueAsync(id, ct)).Select(CraftingQueueItemDto.FromEntity).ToList();
        await _notificationService.NotifyCraftingQueueUpdatedAsync(party.Id, updatedQueue, ct);
        await _notificationService.NotifyFundsUpdatedAsync(party.Id, fundsDto, ct);
        await _notificationService.NotifyActivityLoggedAsync(party.Id, StashActivityLogDto.FromEntity(log), ct);

        return CreatedAtAction(nameof(GetCraftingQueue), new { id = party.Id }, itemDto);
    }

    /// <summary>
    /// Acción exclusiva del DM: Avanza 1 día de Downtime en todos los proyectos de fabricación activos.
    /// Al alcanzar 0 días restantes, el objeto se transfiere automáticamente al alijo principal.
    /// </summary>
    [HttpPost("{id:guid}/crafting/advance-day")]
    [ProducesResponseType(typeof(DowntimeAdvanceResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DowntimeAdvanceResultDto>> AdvanceDowntimeDay(Guid id, CancellationToken ct)
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
                Detail = "No tiene autorización para avanzar el Downtime de esta Party."
            });
        }

        var queue = await _partyRepository.GetCraftingQueueAsync(id, ct);
        var activeItems = queue.Where(i => i.Status == "InProgress").ToList();

        var completedNames = new List<string>();

        foreach (var activeItem in activeItems)
        {
            var completed = activeItem.AdvanceDay();
            if (completed)
            {
                var stashItem = activeItem.ToStashItem();
                await _partyRepository.AddStashItemAsync(stashItem, ct);
                completedNames.Add(stashItem.Name);

                var compLog = new StashActivityLog(
                    Guid.NewGuid(),
                    party.Id,
                    userId.Value,
                    User.GetDisplayName() ?? "DM",
                    "CraftingCompleted",
                    $"¡Fabricación completada! '{stashItem.Name}' ha sido agregado al alijo principal."
                );
                await _partyRepository.AddActivityLogAsync(compLog, ct);
            }
        }

        var advanceLog = new StashActivityLog(
            Guid.NewGuid(),
            party.Id,
            userId.Value,
            User.GetDisplayName() ?? "DM",
            "DowntimeAdvanced",
            $"Avanzado 1 día de Downtime. {completedNames.Count} objeto(s) completado(s)."
        );
        await _partyRepository.AddActivityLogAsync(advanceLog, ct);
        await _partyRepository.SaveChangesAsync(ct);

        var updatedQueueEntities = await _partyRepository.GetCraftingQueueAsync(id, ct);
        var updatedQueueDto = updatedQueueEntities.Select(CraftingQueueItemDto.FromEntity).ToList();

        var reloadedParty = await _partyRepository.GetByIdAsync(id, ct);
        var updatedStashDto = (reloadedParty?.StashItems ?? party.StashItems).Select(StashItemResponseDto.FromEntity).ToList();

        var result = new DowntimeAdvanceResultDto(
            DaysAdvanced: 1,
            CompletedItemsCount: completedNames.Count,
            ActiveItemsCount: updatedQueueEntities.Count(i => i.Status == "InProgress"),
            CompletedItemNames: completedNames,
            UpdatedQueue: updatedQueueDto,
            UpdatedStash: updatedStashDto
        );

        await _notificationService.NotifyDowntimeAdvancedAsync(party.Id, result, ct);
        await _notificationService.NotifyCraftingQueueUpdatedAsync(party.Id, updatedQueueDto, ct);
        await _notificationService.NotifyActivityLoggedAsync(party.Id, StashActivityLogDto.FromEntity(advanceLog), ct);

        if (completedNames.Count > 0)
        {
            var fullStashResponse = new PartyStashResponseDto(
                party.Id,
                party.Name,
                reloadedParty?.TotalStashWeight ?? party.TotalStashWeight,
                updatedStashDto,
                PartyFundDto.FromEntity(reloadedParty ?? party),
                updatedQueueDto
            );
            await _notificationService.NotifyStashUpdatedAsync(party.Id, fullStashResponse, ct);
        }

        return Ok(result);
    }

    /// <summary>
    /// Consulta el historial de actividad y transacciones (Audit Log) del alijo y tesoro de la Party.
    /// </summary>
    [HttpGet("{id:guid}/stash/logs")]
    [ProducesResponseType(typeof(IReadOnlyList<StashActivityLogDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<StashActivityLogDto>>> GetActivityLogs(
        Guid id,
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
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
                Detail = "No tiene autorización para consultar los registros de auditoría de esta Party."
            });
        }

        var logs = await _partyRepository.GetActivityLogsAsync(id, Math.Clamp(limit, 1, 100), ct);
        return Ok(logs.Select(StashActivityLogDto.FromEntity).ToList());
    }
}
