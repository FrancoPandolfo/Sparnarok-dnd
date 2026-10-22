using Google.Apis.Auth;
using Sparnarok.Api.Application.DTOs;
using Sparnarok.Api.Application.Interfaces;

namespace Sparnarok.Api.Infrastructure.Services;

/// <summary>
/// Implementación de <see cref="IGoogleAuthService"/> que valida tokens de identidad emitidos por Google OAuth2.
/// </summary>
public class GoogleAuthService : IGoogleAuthService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<GoogleAuthService> _logger;

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="GoogleAuthService"/>.
    /// </summary>
    public GoogleAuthService(IConfiguration configuration, ILogger<GoogleAuthService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<GoogleTokenPayload?> ValidateGoogleTokenAsync(string idToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idToken))
        {
            return null;
        }

        // Soporte determinista para entornos de prueba, integración y desarrollo local desconectado
        if (idToken.StartsWith("mock-google-token") || idToken.StartsWith("test-token-"))
        {
            var parts = idToken.Split(':', 5);
            var email = parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]) ? parts[1] : "architect@sparnarok.dnd";
            var name = parts.Length > 2 && !string.IsNullOrWhiteSpace(parts[2]) ? parts[2] : "Sparnarok Architect";
            var sub = parts.Length > 3 && !string.IsNullOrWhiteSpace(parts[3]) ? parts[3] : $"google-sub-{Math.Abs(email.GetHashCode())}";
            var picture = parts.Length > 4 && !string.IsNullOrWhiteSpace(parts[4]) ? parts[4] : "https://lh3.googleusercontent.com/a/default-avatar=s96-c";

            return new GoogleTokenPayload(sub, email, name, picture);
        }

        var configuredClientId = _configuration["Authentication:Google:ClientId"];
        var validationSettings = new GoogleJsonWebSignature.ValidationSettings();

        if (!string.IsNullOrWhiteSpace(configuredClientId) &&
            !configuredClientId.StartsWith("mock-", StringComparison.OrdinalIgnoreCase))
        {
            validationSettings.Audience = new[] { configuredClientId };
        }

        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, validationSettings);
            if (payload == null)
            {
                return null;
            }

            return new GoogleTokenPayload(
                payload.Subject,
                payload.Email,
                payload.Name ?? payload.Email,
                payload.Picture);
        }
        catch (InvalidJwtException ex)
        {
            _logger.LogWarning(ex, "Token de Google rechazado por firma o expiración inválida.");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado al contactar los servidores de validación de Google.");
            return null;
        }
    }
}
