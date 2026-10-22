using Sparnarok.Api.Application.DTOs;

namespace Sparnarok.Api.Application.Interfaces;

/// <summary>
/// Contrato para la validación y extracción de información de tokens OAuth2 de Google.
/// </summary>
public interface IGoogleAuthService
{
    /// <summary>
    /// Valida un token ID de Google mediante las claves públicas del proveedor y extrae sus datos.
    /// </summary>
    /// <param name="idToken">Token de identidad provisto por Google.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Carga útil validada o null si el token es inválido o ha expirado.</returns>
    Task<GoogleTokenPayload?> ValidateGoogleTokenAsync(string idToken, CancellationToken cancellationToken = default);
}
