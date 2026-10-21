using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Sparnarok.Api.Application.DTOs;
using Sparnarok.Api.Application.Interfaces;
using Sparnarok.Api.Domain.Entities;
using Sparnarok.Api.Infrastructure.Persistence;

namespace Sparnarok.Api.Controllers;

/// <summary>
/// Provee capacidades de ingesta masiva y streaming de datos de reglas D&amp;D 5e mediante archivos delimitados (CSV).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class IngestionController : ControllerBase
{
    private readonly IWeaponIngestionService _weaponIngestionService;
    private readonly IArmorIngestionService _armorIngestionService;
    private readonly SparnarokDbContext _context;
    private readonly IStringLocalizer<SharedResource> _localizer;

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="IngestionController"/>.
    /// </summary>
    public IngestionController(
        IWeaponIngestionService weaponIngestionService,
        IArmorIngestionService armorIngestionService,
        SparnarokDbContext context,
        IStringLocalizer<SharedResource> localizer)
    {
        _weaponIngestionService = weaponIngestionService;
        _armorIngestionService = armorIngestionService;
        _context = context;
        _localizer = localizer;
    }

    /// <summary>
    /// Ingiere masivamente un catálogo de armas a través de un archivo CSV con soporte de inserción y actualización idempotente (Bulk Upsert).
    /// </summary>
    /// <remarks>
    /// Columnas requeridas en la cabecera del archivo CSV:
    /// - `Name`: Nombre canónico del arma (ej. "Longsword", "Dagger").
    /// - `Damage`: Fórmula de dados de daño (ej. "1d8", "1d4").
    /// - `DamageType`: Tipo de daño válido (ej. "Slashing", "Piercing", "Bludgeoning").
    /// 
    /// Columnas opcionales:
    /// - `Properties`: Propiedades tácticas (ej. "Versatile (1d10), Martial").
    /// - `Weight`: Peso en libras (numérico).
    /// - `Cost`: Costo comercial en piezas de oro/plata (ej. "15 gp").
    /// </remarks>
    /// <param name="file">Archivo CSV subido mediante multipart/form-data.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Retorna las métricas del procesamiento (registros procesados, insertados, actualizados y errores).</response>
    /// <response code="400">Si el archivo no fue provisto, está vacío o no posee extensión .csv.</response>
    [HttpPost("weapons")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(IngestionResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IngestionResultDto>> IngestWeapons(
        IFormFile file,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new ProblemDetails
            {
                Title = _localizer["InvalidCsvFile"],
                Status = StatusCodes.Status400BadRequest,
                Detail = "No se ha recibido ningún archivo o el archivo enviado tiene tamaño 0 bytes."
            });
        }

        var extension = Path.GetExtension(file.FileName);
        if (!string.Equals(extension, ".csv", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new ProblemDetails
            {
                Title = _localizer["InvalidCsvFile"],
                Status = StatusCodes.Status400BadRequest,
                Detail = $"El archivo '{file.FileName}' no posee una extensión .csv válida."
            });
        }

        await using var stream = file.OpenReadStream();
        var result = await _weaponIngestionService.IngestWeaponsAsync(stream, ct);

        return Ok(result);
    }

    /// <summary>
    /// Obtiene el catálogo completo de armas actualmente persistidas en el Códice.
    /// </summary>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Lista completa de armas registradas.</response>
    [HttpGet("weapons")]
    [ProducesResponseType(typeof(IReadOnlyList<Weapon>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<Weapon>>> GetWeapons(CancellationToken ct)
    {
        var weapons = await _context.Weapons
            .AsNoTracking()
            .OrderBy(w => w.Name)
            .ToListAsync(ct);

        return Ok(weapons);
    }

    /// <summary>
    /// Ingiere masivamente un catálogo de armaduras y escudos a través de un archivo CSV con soporte de inserción y actualización idempotente (Bulk Upsert).
    /// </summary>
    /// <remarks>
    /// Columnas requeridas en la cabecera del archivo CSV:
    /// - `Name`: Nombre de la armadura o escudo (ej. "Chain Mail", "Half Plate Armor", "Shield").
    /// - `Category`: Categoría de armadura (Light, Medium, Heavy, Shield).
    /// - `ArmorClass`: Fórmula de CA (ej. "11 + Dex modifier", "15 + Dex modifier (max 2)", "18", "+2").
    /// 
    /// Columnas opcionales:
    /// - `Strength`: Requisito mínimo de Fuerza (ej. "Str 13", "Str 15", "-").
    /// - `Stealth`: Efecto de sigilo (ej. "Disadvantage", "-").
    /// - `Weight`: Peso en libras (ej. "8 lb.", "40 lb.").
    /// - `Cost`: Costo comercial (ej. "750 GP", "10 GP").
    /// </remarks>
    /// <param name="file">Archivo CSV subido mediante multipart/form-data.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Retorna las métricas del procesamiento (registros procesados, insertados, actualizados y errores).</response>
    /// <response code="400">Si el archivo no fue provisto, está vacío o no posee extensión .csv.</response>
    [HttpPost("armors")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(IngestionResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IngestionResultDto>> IngestArmors(
        IFormFile file,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new ProblemDetails
            {
                Title = _localizer["InvalidCsvFile"],
                Status = StatusCodes.Status400BadRequest,
                Detail = "No se ha recibido ningún archivo o el archivo enviado tiene tamaño 0 bytes."
            });
        }

        var extension = Path.GetExtension(file.FileName);
        if (!string.Equals(extension, ".csv", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new ProblemDetails
            {
                Title = _localizer["InvalidCsvFile"],
                Status = StatusCodes.Status400BadRequest,
                Detail = $"El archivo '{file.FileName}' no posee una extensión .csv válida."
            });
        }

        await using var stream = file.OpenReadStream();
        var result = await _armorIngestionService.IngestArmorsAsync(stream, ct);

        return Ok(result);
    }

    /// <summary>
    /// Obtiene el catálogo completo de armaduras y escudos actualmente persistidos en el Códice.
    /// </summary>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Lista completa de armaduras registradas.</response>
    [HttpGet("armors")]
    [ProducesResponseType(typeof(IReadOnlyList<Armor>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<Armor>>> GetArmors(CancellationToken ct)
    {
        var armors = await _context.Armors
            .AsNoTracking()
            .OrderBy(a => a.Name)
            .ToListAsync(ct);

        return Ok(armors);
    }
}
