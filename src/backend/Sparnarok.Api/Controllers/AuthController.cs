using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sparnarok.Api.Application.DTOs;
using Sparnarok.Api.Application.Extensions;
using Sparnarok.Api.Application.Interfaces;
using Sparnarok.Api.Domain.Entities;
using Sparnarok.Api.Infrastructure.Persistence;

namespace Sparnarok.Api.Controllers;

/// <summary>
/// Endpoints de autenticación federada (Google OAuth2) y gestión de sesiones de usuario.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IGoogleAuthService _googleAuthService;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly SparnarokDbContext _context;
    private readonly ILogger<AuthController> _logger;

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="AuthController"/>.
    /// </summary>
    public AuthController(
        IGoogleAuthService googleAuthService,
        IJwtTokenService jwtTokenService,
        SparnarokDbContext context,
        ILogger<AuthController> logger)
    {
        _googleAuthService = googleAuthService;
        _jwtTokenService = jwtTokenService;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Autentica o registra un usuario mediante un token de identidad emitido por Google OAuth2.
    /// </summary>
    /// <param name="request">Credencial o token ID de Google.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>JWT propio de Sparnarok y datos de perfil del usuario.</returns>
    /// <response code="200">Autenticación exitosa y sesión emitida.</response>
    /// <response code="400">Petición malformada o token faltante.</response>
    /// <response code="401">Token de Google inválido, expirado o adulterado.</response>
    [HttpPost("google")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponseDto>> LoginWithGoogle(
        [FromBody] GoogleLoginRequestDto request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request?.IdToken))
        {
            ModelState.AddModelError(nameof(request.IdToken), "El token de Google es obligatorio.");
            return ValidationProblem(ModelState);
        }

        var googlePayload = await _googleAuthService.ValidateGoogleTokenAsync(request.IdToken, ct);
        if (googlePayload is null)
        {
            _logger.LogWarning("Intento de login con token de Google inválido.");
            return Unauthorized(new ProblemDetails
            {
                Title = "Autenticación de Google inválida",
                Detail = "El token provisto no pudo ser validado con los servidores de Google o ha expirado.",
                Status = StatusCodes.Status401Unauthorized
            });
        }

        // Buscar usuario existente por GoogleSubjectId o Email
        var user = await _context.Users.FirstOrDefaultAsync(
            u => u.GoogleSubjectId == googlePayload.Subject || u.Email == googlePayload.Email.ToLowerInvariant(),
            ct);

        if (user is null)
        {
            user = ApplicationUser.CreateFromGoogle(
                googlePayload.Subject,
                googlePayload.Email,
                googlePayload.Name,
                googlePayload.Picture);

            await _context.Users.AddAsync(user, ct);
            _logger.LogInformation("Nuevo usuario registrado vía Google: {Email}", user.Email);
        }
        else
        {
            user.UpdateFromGoogle(googlePayload.Name, googlePayload.Picture);
            _logger.LogInformation("Sesión renovada para usuario existente: {Email}", user.Email);
        }

        await _context.SaveChangesAsync(ct);

        var token = _jwtTokenService.GenerateToken(user);

        return Ok(new AuthResponseDto(
            token,
            user.Id,
            user.Email,
            user.DisplayName,
            user.AvatarUrl));
    }

    /// <summary>
    /// Obtiene el perfil del usuario autenticado actualmente según su token JWT.
    /// </summary>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Datos del perfil del usuario autenticado.</response>
    /// <response code="401">No autenticado o token ausente/inválido.</response>
    /// <response code="404">Usuario no encontrado en la base de datos.</response>
    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AuthResponseDto>> GetCurrentUser(CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId.Value, ct);
        if (user is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Usuario no encontrado",
                Detail = "No se encontró el registro de usuario asociado al token actual.",
                Status = StatusCodes.Status404NotFound
            });
        }

        // Devolvemos el perfil con el token re-firmado o eco
        var token = _jwtTokenService.GenerateToken(user);
        return Ok(new AuthResponseDto(
            token,
            user.Id,
            user.Email,
            user.DisplayName,
            user.AvatarUrl));
    }
}
