using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sparnarok.Api.Application.DTOs;
using Sparnarok.Api.Application.Extensions;
using Sparnarok.Api.Application.Interfaces;
using Sparnarok.Api.Domain.Entities;
using Sparnarok.Api.Infrastructure.Persistence;

namespace Sparnarok.Api.Controllers;

/// <summary>
/// Endpoints de autenticación federada (Google OAuth2), autenticación convencional (Email/Contraseña)
/// y vinculación bidireccional de identidades.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IGoogleAuthService _googleAuthService;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IPasswordHasher<ApplicationUser> _passwordHasher;
    private readonly SparnarokDbContext _context;
    private readonly ILogger<AuthController> _logger;

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="AuthController"/>.
    /// </summary>
    public AuthController(
        IGoogleAuthService googleAuthService,
        IJwtTokenService jwtTokenService,
        IPasswordHasher<ApplicationUser> passwordHasher,
        SparnarokDbContext context,
        ILogger<AuthController> logger)
    {
        _googleAuthService = googleAuthService;
        _jwtTokenService = jwtTokenService;
        _passwordHasher = passwordHasher;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Registra una cuenta nueva mediante Email y Contraseña convencional.
    /// Si la cuenta ya existía vía Google sin contraseña, vincula la contraseña a la cuenta existente.
    /// </summary>
    /// <param name="request">Datos de registro (Email, Contraseña, Nombre).</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Usuario registrado o vinculado exitosamente con sesión iniciada.</response>
    /// <response code="400">Datos inválidos o contraseña insuficiente.</response>
    /// <response code="409">El correo electrónico ya posee una contraseña convencional registrada.</response>
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthResponseDto>> Register(
        [FromBody] RegisterRequestDto request,
        CancellationToken ct)
    {
        if (request is null)
        {
            return BadRequest();
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            ModelState.AddModelError(nameof(request.Email), "El correo electrónico es obligatorio.");
        }
        if (string.IsNullOrWhiteSpace(request?.Password) || request.Password.Length < 6)
        {
            ModelState.AddModelError(nameof(request.Password), "La contraseña debe tener al menos 6 caracteres.");
        }
        if (string.IsNullOrWhiteSpace(request?.DisplayName))
        {
            ModelState.AddModelError(nameof(request.DisplayName), "El nombre de usuario es obligatorio.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct);

        ApplicationUser user;
        if (existingUser != null)
        {
            if (existingUser.HasPassword)
            {
                return Conflict(new ProblemDetails
                {
                    Title = "Correo ya registrado",
                    Detail = "Ya existe una cuenta con este correo y una contraseña configurada. Inicia sesión directamente.",
                    Status = StatusCodes.Status409Conflict
                });
            }

            // Si el usuario existía solo vía Google, vinculamos la contraseña a su perfil existente
            var hash = _passwordHasher.HashPassword(existingUser, request.Password);
            existingUser.SetPassword(hash);
            existingUser.RecordLogin();
            user = existingUser;
            _logger.LogInformation("Contraseña vinculada a usuario existente de Google: {Email}", user.Email);
        }
        else
        {
            var dummyUser = ApplicationUser.CreateConventional(normalizedEmail, "placeholder", request.DisplayName.Trim());
            var hash = _passwordHasher.HashPassword(dummyUser, request.Password);
            user = ApplicationUser.CreateConventional(normalizedEmail, hash, request.DisplayName.Trim());
            await _context.Users.AddAsync(user, ct);
            _logger.LogInformation("Nuevo usuario registrado convencionalmente: {Email}", user.Email);
        }

        await _context.SaveChangesAsync(ct);
        var token = _jwtTokenService.GenerateToken(user);

        return Ok(new AuthResponseDto(
            token,
            user.Id,
            user.Email,
            user.DisplayName,
            user.AvatarUrl,
            user.HasPassword,
            user.HasGoogleLinked));
    }

    /// <summary>
    /// Inicia sesión convencionalmente mediante Email y Contraseña.
    /// </summary>
    /// <param name="request">Credenciales del usuario.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Autenticación exitosa y sesión emitida.</response>
    /// <response code="400">Datos incompletos.</response>
    /// <response code="401">Credenciales inválidas o cuenta solo habilitada con Google.</response>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponseDto>> Login(
        [FromBody] LoginRequestDto request,
        CancellationToken ct)
    {
        if (request is null)
        {
            return BadRequest();
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            ModelState.AddModelError(nameof(request.Email), "El correo electrónico es obligatorio.");
        }
        if (string.IsNullOrWhiteSpace(request?.Password))
        {
            ModelState.AddModelError(nameof(request.Password), "La contraseña es obligatoria.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct);

        if (user == null)
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Credenciales incorrectas",
                Detail = "El correo electrónico o la contraseña ingresados no coinciden con ningún registro.",
                Status = StatusCodes.Status401Unauthorized
            });
        }

        if (!user.HasPassword)
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Cuenta registrada con Google",
                Detail = "Esta cuenta fue creada originalmente mediante Google. Por favor, pulsa 'Continuar con Google' para iniciar sesión o vincular una contraseña.",
                Status = StatusCodes.Status401Unauthorized
            });
        }

        var verificationResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash!, request.Password);
        if (verificationResult == PasswordVerificationResult.Failed)
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Credenciales incorrectas",
                Detail = "El correo electrónico o la contraseña ingresados no coinciden con ningún registro.",
                Status = StatusCodes.Status401Unauthorized
            });
        }

        if (verificationResult == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.SetPassword(_passwordHasher.HashPassword(user, request.Password));
        }

        user.RecordLogin();
        await _context.SaveChangesAsync(ct);

        var token = _jwtTokenService.GenerateToken(user);
        return Ok(new AuthResponseDto(
            token,
            user.Id,
            user.Email,
            user.DisplayName,
            user.AvatarUrl,
            user.HasPassword,
            user.HasGoogleLinked));
    }

    /// <summary>
    /// Autentica o registra un usuario mediante un token de identidad emitido por Google OAuth2.
    /// Si ya existía un usuario registrado convencionalmente con el mismo correo, vincula automáticamente su cuenta de Google.
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

        var normalizedEmail = googlePayload.Email.Trim().ToLowerInvariant();

        // Buscar usuario existente por GoogleSubjectId o Email
        var user = await _context.Users.FirstOrDefaultAsync(
            u => u.GoogleSubjectId == googlePayload.Subject || u.Email == normalizedEmail,
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
            // Vinculación automática sin conflicto si el usuario existía por email sin Google asociado
            if (string.IsNullOrWhiteSpace(user.GoogleSubjectId))
            {
                user.LinkGoogle(googlePayload.Subject, googlePayload.Picture);
                _logger.LogInformation("Cuenta de Google vinculada automáticamente a usuario existente: {Email}", user.Email);
            }
            else
            {
                user.UpdateFromGoogle(googlePayload.Name, googlePayload.Picture, googlePayload.Subject);
                _logger.LogInformation("Sesión renovada para usuario existente: {Email}", user.Email);
            }
        }

        await _context.SaveChangesAsync(ct);

        var token = _jwtTokenService.GenerateToken(user);

        return Ok(new AuthResponseDto(
            token,
            user.Id,
            user.Email,
            user.DisplayName,
            user.AvatarUrl,
            user.HasPassword,
            user.HasGoogleLinked));
    }

    /// <summary>
    /// Permite al usuario autenticado configurar o actualizar una contraseña convencional
    /// (especialmente útil si se registró originalmente mediante Google).
    /// </summary>
    /// <param name="request">Nueva contraseña.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Contraseña configurada exitosamente.</response>
    /// <response code="400">Contraseña inválida.</response>
    /// <response code="401">No autenticado.</response>
    [Authorize]
    [HttpPost("set-password")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponseDto>> SetPassword(
        [FromBody] SetPasswordRequestDto request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request?.NewPassword) || request.NewPassword.Length < 6)
        {
            ModelState.AddModelError(nameof(request.NewPassword), "La contraseña debe tener al menos 6 caracteres.");
            return ValidationProblem(ModelState);
        }

        var userId = User.GetUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId.Value, ct);
        if (user is null)
        {
            return NotFound();
        }

        var hash = _passwordHasher.HashPassword(user, request.NewPassword);
        user.SetPassword(hash);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Contraseña configurada exitosamente para el usuario: {Email}", user.Email);

        var token = _jwtTokenService.GenerateToken(user);
        return Ok(new AuthResponseDto(
            token,
            user.Id,
            user.Email,
            user.DisplayName,
            user.AvatarUrl,
            user.HasPassword,
            user.HasGoogleLinked));
    }

    /// <summary>
    /// Vincula explícitamente una cuenta de Google al usuario autenticado actualmente.
    /// </summary>
    /// <param name="request">Token ID de Google.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Cuenta de Google vinculada exitosamente.</response>
    /// <response code="400">Token faltante o inválido.</response>
    /// <response code="409">La cuenta de Google ya está vinculada a otro usuario.</response>
    [Authorize]
    [HttpPost("link-google")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthResponseDto>> LinkGoogle(
        [FromBody] GoogleLoginRequestDto request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request?.IdToken))
        {
            ModelState.AddModelError(nameof(request.IdToken), "El token de Google es obligatorio.");
            return ValidationProblem(ModelState);
        }

        var userId = User.GetUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId.Value, ct);
        if (user is null)
        {
            return NotFound();
        }

        var googlePayload = await _googleAuthService.ValidateGoogleTokenAsync(request.IdToken, ct);
        if (googlePayload is null)
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Autenticación de Google inválida",
                Detail = "El token provisto no pudo ser validado con los servidores de Google o ha expirado.",
                Status = StatusCodes.Status401Unauthorized
            });
        }

        var otherUserWithGoogle = await _context.Users.FirstOrDefaultAsync(
            u => u.GoogleSubjectId == googlePayload.Subject && u.Id != user.Id,
            ct);

        if (otherUserWithGoogle != null)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Cuenta de Google ya vinculada",
                Detail = "Esta cuenta de Google ya se encuentra asociada a otro perfil de Sparnarok.",
                Status = StatusCodes.Status409Conflict
            });
        }

        user.LinkGoogle(googlePayload.Subject, googlePayload.Picture);
        await _context.SaveChangesAsync(ct);

        var token = _jwtTokenService.GenerateToken(user);
        return Ok(new AuthResponseDto(
            token,
            user.Id,
            user.Email,
            user.DisplayName,
            user.AvatarUrl,
            user.HasPassword,
            user.HasGoogleLinked));
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

        var token = _jwtTokenService.GenerateToken(user);
        return Ok(new AuthResponseDto(
            token,
            user.Id,
            user.Email,
            user.DisplayName,
            user.AvatarUrl,
            user.HasPassword,
            user.HasGoogleLinked));
    }
}
