using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sparnarok.Api.Application.DTOs;
using Sparnarok.Api.Application.Extensions;
using Sparnarok.Api.Infrastructure.Persistence;

namespace Sparnarok.Api.Controllers;

/// <summary>
/// Endpoints para la gestión del perfil global e identidad del usuario (Fase 3.7).
/// </summary>
[ApiController]
[Authorize]
[Route("api/[controller]")]
[Produces("application/json")]
public class UsersController : ControllerBase
{
    private readonly SparnarokDbContext _context;

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="UsersController"/>.
    /// </summary>
    public UsersController(SparnarokDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Obtiene el perfil global del usuario autenticado actualmente.
    /// </summary>
    [HttpGet("profile")]
    [ProducesResponseType(typeof(UserProfileResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserProfileResponseDto>> GetProfile(CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue) return Unauthorized();

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId.Value, ct);
        if (user is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Usuario no encontrado",
                Status = StatusCodes.Status404NotFound,
                Detail = "No se encontró el registro del usuario autenticado."
            });
        }

        return Ok(new UserProfileResponseDto(
            user.Id,
            user.Email,
            user.DisplayName,
            user.GlobalNickname,
            user.AvatarUrl,
            user.HasPassword,
            user.HasGoogleLinked
        ));
    }

    /// <summary>
    /// Actualiza el apodo o alias global del usuario en el ecosistema Sparnarok (Fase 3.7).
    /// </summary>
    /// <param name="request">Datos de actualización de perfil.</param>
    /// <param name="ct">Token de cancelación.</param>
    [HttpPut("profile")]
    [ProducesResponseType(typeof(UserProfileResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserProfileResponseDto>> UpdateProfile(
        [FromBody] UpdateUserProfileRequestDto request,
        CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue) return Unauthorized();

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId.Value, ct);
        if (user is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Usuario no encontrado",
                Status = StatusCodes.Status404NotFound,
                Detail = "No se encontró el registro del usuario para actualizar su perfil."
            });
        }

        user.SetGlobalNickname(request?.GlobalNickname);
        await _context.SaveChangesAsync(ct);

        return Ok(new UserProfileResponseDto(
            user.Id,
            user.Email,
            user.DisplayName,
            user.GlobalNickname,
            user.AvatarUrl,
            user.HasPassword,
            user.HasGoogleLinked
        ));
    }
}
