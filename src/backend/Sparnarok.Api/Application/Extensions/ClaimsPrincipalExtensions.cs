using System.Security.Claims;

namespace Sparnarok.Api.Application.Extensions;

/// <summary>
/// Métodos de extensión sobre <see cref="ClaimsPrincipal"/> para la resolución de identidad de usuario.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Extrae el identificador de usuario <see cref="Guid"/> de los claims del token JWT actual.
    /// </summary>
    /// <param name="principal">Identidad autenticada.</param>
    /// <returns>Guid del usuario si existe y es válido; en caso contrario, null.</returns>
    public static Guid? GetUserId(this ClaimsPrincipal? principal)
    {
        if (principal == null)
            return null;

        var claimValue = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? principal.FindFirst("sub")?.Value
            ?? principal.FindFirst("userId")?.Value;

        return Guid.TryParse(claimValue, out var userId) ? userId : null;
    }

    /// <summary>
    /// Extrae el nombre o correo visible del usuario a partir de los claims del token.
    /// </summary>
    public static string GetDisplayName(this ClaimsPrincipal? principal)
    {
        if (principal == null)
            return "Aventurero";

        return principal.FindFirst(ClaimTypes.Name)?.Value
            ?? principal.FindFirst("name")?.Value
            ?? principal.FindFirst(ClaimTypes.Email)?.Value
            ?? "Aventurero";
    }
}
