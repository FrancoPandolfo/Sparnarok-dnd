using Sparnarok.Api.Domain.Entities;

namespace Sparnarok.Api.Application.Interfaces;

/// <summary>
/// Contrato para la generación y firma de tokens JWT propios de la API.
/// </summary>
public interface IJwtTokenService
{
    /// <summary>
    /// Genera un token JWT firmado criptográficamente para el usuario especificado.
    /// </summary>
    /// <param name="user">Entidad de usuario autenticada.</param>
    /// <returns>Cadena en formato JWT compacta (header.payload.signature).</returns>
    string GenerateToken(ApplicationUser user);
}
