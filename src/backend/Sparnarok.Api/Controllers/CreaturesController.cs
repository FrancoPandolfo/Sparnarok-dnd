using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Sparnarok.Api.Application.DTOs;
using Sparnarok.Api.Application.Interfaces;
using Sparnarok.Api.Domain.Entities;
using Sparnarok.Api.Domain.Enums;
using Sparnarok.Api.Domain.ValueObjects;
using Action = Sparnarok.Api.Domain.Entities.Action;

namespace Sparnarok.Api.Controllers;

/// <summary>
/// Provee operaciones CRUD y gestión táctica para Criaturas de D&amp;D 5e (2024).
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class CreaturesController : ControllerBase
{
    private readonly ICreatureRepository _creatureRepository;
    private readonly IWeaponMasteryRepository _masteryRepository;
    private readonly IValidator<CreateCreatureRequestDto> _createValidator;
    private readonly IValidator<UpdateCreatureRequestDto> _updateValidator;
    private readonly IValidator<CreateActionRequestDto> _actionValidator;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly Sparnarok.Api.Infrastructure.Persistence.SparnarokDbContext? _context;

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="CreaturesController"/>.
    /// </summary>
    public CreaturesController(
        ICreatureRepository creatureRepository,
        IWeaponMasteryRepository masteryRepository,
        IValidator<CreateCreatureRequestDto> createValidator,
        IValidator<UpdateCreatureRequestDto> updateValidator,
        IValidator<CreateActionRequestDto> actionValidator,
        IStringLocalizer<SharedResource> localizer,
        Sparnarok.Api.Infrastructure.Persistence.SparnarokDbContext? context = null)
    {
        _creatureRepository = creatureRepository;
        _masteryRepository = masteryRepository;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _actionValidator = actionValidator;
        _localizer = localizer;
        _context = context;
    }

    /// <summary>
    /// Lista todas las criaturas registradas en el Códice junto a sus acciones y maestrías.
    /// </summary>
    /// <response code="200">Retorna la lista completa de criaturas.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CreatureResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CreatureResponseDto>>> GetAll(CancellationToken ct)
    {
        var creatures = await _creatureRepository.GetAllAsync(ct);
        var dtos = creatures.Select(CreatureResponseDto.FromEntity).ToList();
        return Ok(dtos);
    }

    /// <summary>
    /// Obtiene una criatura por su identificador único.
    /// </summary>
    /// <param name="id">Identificador único de la criatura.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Retorna la criatura solicitada.</response>
    /// <response code="404">Si la criatura no existe.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CreatureResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CreatureResponseDto>> GetById(Guid id, CancellationToken ct)
    {
        var creature = await _creatureRepository.GetByIdAsync(id, ct);
        if (creature is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = _localizer["CreatureNotFound"],
                Status = StatusCodes.Status404NotFound,
                Detail = $"No se encontró la criatura con id '{id}'."
            });
        }

        return Ok(CreatureResponseDto.FromEntity(creature));
    }

    /// <summary>
    /// Registra una nueva criatura en el Códice de Sparnarok.
    /// </summary>
    /// <param name="request">Datos de creación de la criatura.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="201">Retorna la criatura creada exitosamente con su cabecera Location.</response>
    /// <response code="400">Si los datos provistos violan las reglas canónicas de D&amp;D 5e.</response>
    [HttpPost]
    [ProducesResponseType(typeof(CreatureResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CreatureResponseDto>> Create(
        [FromBody] CreateCreatureRequestDto request,
        CancellationToken ct)
    {
        var validationResult = await _createValidator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validationResult.ToDictionary()));
        }

        var abilityScores = new AbilityScores(
            request.Strength,
            request.Dexterity,
            request.Constitution,
            request.Intelligence,
            request.Wisdom,
            request.Charisma);

        var hitPoints = new HitPoints(request.MaxHitPoints);

        var armorClass = new ArmorClass(
            request.ArmorClassBase,
            request.ShieldBonus,
            request.AllowsDexterityModifier,
            request.MaxDexterityModifier);

        var creature = new Creature(
            request.Name,
            request.Size,
            request.Type,
            request.Alignment,
            request.ChallengeRating,
            request.SpeedFeet,
            abilityScores,
            hitPoints,
            armorClass);

        await _creatureRepository.AddAsync(creature, ct);
        await _creatureRepository.SaveChangesAsync(ct);

        var responseDto = CreatureResponseDto.FromEntity(creature);
        return CreatedAtAction(nameof(GetById), new { id = creature.Id }, responseDto);
    }

    /// <summary>
    /// Registra un nuevo perfil de combatiente customizado (Personaje de Jugador / Héroe)
    /// utilizando estadísticas finales directas sin requerir un motor completo de creación de personajes.
    /// </summary>
    /// <param name="request">Estadísticas finales del héroe (Nombre, HP, CA final y atributos).</param>
    /// <param name="customValidator">Validador de reglas para perfil de héroe custom.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="201">Retorna el héroe creado con IsPlayerCharacter = true.</response>
    /// <response code="400">Si los datos provistos violan las validaciones del perfil.</response>
    [HttpPost("custom")]
    [ProducesResponseType(typeof(CreatureResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CreatureResponseDto>> CreateCustomHero(
        [FromBody] CreateCustomHeroRequestDto request,
        [FromServices] IValidator<CreateCustomHeroRequestDto> customValidator,
        CancellationToken ct)
    {
        var validationResult = await customValidator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validationResult.ToDictionary()));
        }

        var abilityScores = new AbilityScores(
            request.Strength,
            request.Dexterity,
            request.Constitution,
            request.Intelligence,
            request.Wisdom,
            request.Charisma);

        var hitPoints = new HitPoints(request.MaxHitPoints, request.MaxHitPoints);

        // La CA provista es la CA final calculada del héroe
        var armorClass = new ArmorClass(
            request.ArmorClass,
            shieldBonus: 0,
            allowsDexterityModifier: false);

        var creature = new Creature(
            request.Name,
            CreatureSize.Medium,
            CreatureType.Humanoid,
            "Heroic",
            challengeRating: 1,
            speedFeet: request.SpeedFeet > 0 ? request.SpeedFeet : 30,
            abilityScores: abilityScores,
            hitPoints: hitPoints,
            armorClass: armorClass,
            isPlayerCharacter: true);

        // Acción de ataque principal del héroe
        var attackName = string.IsNullOrWhiteSpace(request.AttackName) ? "Ataque Principal" : request.AttackName.Trim();
        var damageFormula = string.IsNullOrWhiteSpace(request.DamageFormula) ? "1d8" : request.DamageFormula.Trim();

        var primaryAttack = new Action(
            creature.Id,
            attackName,
            ActionType.Action,
            $"Ataque estándar del héroe {creature.Name}.",
            isAttack: true,
            reachOrRangeFeet: 5,
            damageFormula: damageFormula,
            damageType: DamageType.Slashing,
            attackModifierAbility: request.AttackModifierAbility);

        creature.AddAction(primaryAttack);

        await _creatureRepository.AddAsync(creature, ct);
        await _creatureRepository.SaveChangesAsync(ct);

        var responseDto = CreatureResponseDto.FromEntity(creature);
        return CreatedAtAction(nameof(GetById), new { id = creature.Id }, responseDto);
    }

    /// <summary>
    /// Actualiza los atributos base, salud y armadura de una criatura existente.
    /// </summary>
    /// <param name="id">Identificador único de la criatura.</param>
    /// <param name="request">Datos a actualizar.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Retorna la criatura con sus datos actualizados.</response>
    /// <response code="400">Si los datos no son válidos.</response>
    /// <response code="404">Si la criatura no existe.</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(CreatureResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CreatureResponseDto>> Update(
        Guid id,
        [FromBody] UpdateCreatureRequestDto request,
        CancellationToken ct)
    {
        var validationResult = await _updateValidator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validationResult.ToDictionary()));
        }

        var creature = await _creatureRepository.GetByIdAsync(id, ct);
        if (creature is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = _localizer["CreatureNotFound"],
                Status = StatusCodes.Status404NotFound,
                Detail = $"No se encontró la criatura con id '{id}'."
            });
        }

        var newAbilities = new AbilityScores(
            request.Strength,
            request.Dexterity,
            request.Constitution,
            request.Intelligence,
            request.Wisdom,
            request.Charisma);

        var newHp = new HitPoints(request.MaxHitPoints, Math.Min(creature.HitPoints.Current, request.MaxHitPoints));

        var newAc = new ArmorClass(
            request.ArmorClassBase,
            request.ShieldBonus,
            request.AllowsDexterityModifier,
            request.MaxDexterityModifier);

        creature.UpdateStatistics(
            request.Name,
            request.Size,
            request.Type,
            request.Alignment,
            request.ChallengeRating,
            request.SpeedFeet,
            newAbilities,
            newHp,
            newAc);

        await _creatureRepository.SaveChangesAsync(ct);

        return Ok(CreatureResponseDto.FromEntity(creature));
    }

    /// <summary>
    /// Elimina una criatura y sus acciones asociadas del Códice.
    /// </summary>
    /// <param name="id">Identificador único de la criatura.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="204">Criatura eliminada exitosamente.</response>
    /// <response code="404">Si la criatura no existe.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var creature = await _creatureRepository.GetByIdAsync(id, ct);
        if (creature is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = _localizer["CreatureNotFound"],
                Status = StatusCodes.Status404NotFound,
                Detail = $"No se encontró la criatura con id '{id}'."
            });
        }

        await _creatureRepository.DeleteAsync(creature, ct);
        await _creatureRepository.SaveChangesAsync(ct);

        return NoContent();
    }

    /// <summary>
    /// Agrega una acción o ataque táctico a la criatura especificada.
    /// </summary>
    /// <param name="id">Identificador de la criatura.</param>
    /// <param name="request">Datos de la acción a incorporar.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="201">Acción creada e incorporada exitosamente.</response>
    /// <response code="400">Si los datos de la acción no son válidos.</response>
    /// <response code="404">Si la criatura o la maestría asociada no existen.</response>
    [HttpPost("{id:guid}/actions")]
    [ProducesResponseType(typeof(ActionResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ActionResponseDto>> AddAction(
        Guid id,
        [FromBody] CreateActionRequestDto request,
        CancellationToken ct)
    {
        var validationResult = await _actionValidator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validationResult.ToDictionary()));
        }

        var creature = await _creatureRepository.GetByIdAsync(id, ct);
        if (creature is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = _localizer["CreatureNotFound"],
                Status = StatusCodes.Status404NotFound,
                Detail = $"No se encontró la criatura con id '{id}'."
            });
        }

        WeaponMastery? associatedMastery = null;
        if (request.AssociatedMasteryId.HasValue)
        {
            associatedMastery = await _masteryRepository.GetByIdAsync(request.AssociatedMasteryId.Value, ct);
            if (associatedMastery is null)
            {
                return NotFound(new ProblemDetails
                {
                    Title = _localizer["MasteryNotFound"],
                    Status = StatusCodes.Status404NotFound,
                    Detail = $"No se encontró la maestría con id '{request.AssociatedMasteryId.Value}'."
                });
            }
        }

        var action = new Action(
            creature.Id,
            request.Name,
            request.Type,
            request.Description,
            request.IsAttack,
            request.ReachOrRangeFeet,
            request.DamageFormula,
            request.DamageType,
            request.AttackModifierAbility,
            associatedMastery);

        creature.AddAction(action);
        await _creatureRepository.AddActionAsync(action, ct);
        await _creatureRepository.SaveChangesAsync(ct);

        return StatusCode(StatusCodes.Status201Created, ActionResponseDto.FromEntity(action));
    }

    /// <summary>
    /// Elimina una acción específica de la criatura.
    /// </summary>
    /// <param name="id">Identificador de la criatura.</param>
    /// <param name="actionId">Identificador de la acción a remover.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="204">Acción eliminada exitosamente.</response>
    /// <response code="404">Si la criatura no existe.</response>
    [HttpDelete("{id:guid}/actions/{actionId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveAction(Guid id, Guid actionId, CancellationToken ct)
    {
        var creature = await _creatureRepository.GetByIdAsync(id, ct);
        if (creature is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = _localizer["CreatureNotFound"],
                Status = StatusCodes.Status404NotFound,
                Detail = $"No se encontró la criatura con id '{id}'."
            });
        }

        creature.RemoveAction(actionId);
        await _creatureRepository.SaveChangesAsync(ct);

        return NoContent();
    }

    /// <summary>
    /// Enseña una propiedad de maestría de armas (Weapon Mastery 2024) a la criatura.
    /// </summary>
    /// <param name="id">Identificador de la criatura.</param>
    /// <param name="masteryId">Identificador de la maestría.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Maestría aprendida exitosamente.</response>
    /// <response code="404">Si la criatura o la maestría no existen.</response>
    [HttpPost("{id:guid}/masteries/{masteryId:guid}")]
    [ProducesResponseType(typeof(CreatureResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CreatureResponseDto>> LearnMastery(Guid id, Guid masteryId, CancellationToken ct)
    {
        var creature = await _creatureRepository.GetByIdAsync(id, ct);
        if (creature is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = _localizer["CreatureNotFound"],
                Status = StatusCodes.Status404NotFound,
                Detail = $"No se encontró la criatura con id '{id}'."
            });
        }

        var mastery = await _masteryRepository.GetByIdAsync(masteryId, ct);
        if (mastery is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = _localizer["MasteryNotFound"],
                Status = StatusCodes.Status404NotFound,
                Detail = $"No se encontró la maestría con id '{masteryId}'."
            });
        }

        creature.LearnWeaponMastery(mastery);
        await _creatureRepository.SaveChangesAsync(ct);

        return Ok(CreatureResponseDto.FromEntity(creature));
    }

    /// <summary>
    /// Equipa o desequipa una armadura corporal y/o un escudo en una criatura existente, recalculando su CA dinámica.
    /// </summary>
    /// <param name="id">Identificador único de la criatura.</param>
    /// <param name="armorId">ID de la armadura a equipar (o Guid.Empty / null para desequipar o no modificar).</param>
    /// <param name="shieldId">ID del escudo a equipar (o Guid.Empty / null para desequipar o no modificar).</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Criatura con el equipamiento actualizado y su CA dinámica calculada.</response>
    /// <response code="404">Si la criatura, armadura o escudo no existen.</response>
    [HttpPost("{id:guid}/equip")]
    [ProducesResponseType(typeof(CreatureResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CreatureResponseDto>> Equip(
        Guid id,
        [FromQuery] Guid? armorId,
        [FromQuery] Guid? shieldId,
        CancellationToken ct)
    {
        if (_context == null)
            return StatusCode(StatusCodes.Status501NotImplemented, "Contexto de base de datos no configurado.");

        var creature = await _context.Creatures
            .Include(c => c.Actions).ThenInclude(a => a.AssociatedMastery)
            .Include(c => c.KnownMasteries)
            .Include(c => c.EquippedArmor)
            .Include(c => c.EquippedShield)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (creature is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = _localizer["CreatureNotFound"],
                Status = StatusCodes.Status404NotFound,
                Detail = $"No se encontró la criatura con id '{id}'."
            });
        }

        if (armorId.HasValue)
        {
            if (armorId.Value == Guid.Empty)
            {
                creature.UnequipArmor();
            }
            else
            {
                var armor = await _context.Armors.FirstOrDefaultAsync(a => a.Id == armorId.Value, ct);
                if (armor is null)
                {
                    return NotFound(new ProblemDetails
                    {
                        Title = "Armadura no encontrada",
                        Status = StatusCodes.Status404NotFound,
                        Detail = $"No se encontró la armadura con id '{armorId.Value}'."
                    });
                }
                creature.EquipArmor(armor);
            }
        }

        if (shieldId.HasValue)
        {
            if (shieldId.Value == Guid.Empty)
            {
                creature.UnequipShield();
            }
            else
            {
                var shield = await _context.Armors.FirstOrDefaultAsync(a => a.Id == shieldId.Value, ct);
                if (shield is null)
                {
                    return NotFound(new ProblemDetails
                    {
                        Title = "Escudo no encontrado",
                        Status = StatusCodes.Status404NotFound,
                        Detail = $"No se encontró el escudo con id '{shieldId.Value}'."
                    });
                }
                creature.EquipShield(shield);
            }
        }

        await _context.SaveChangesAsync(ct);
        return Ok(CreatureResponseDto.FromEntity(creature));
    }
}
