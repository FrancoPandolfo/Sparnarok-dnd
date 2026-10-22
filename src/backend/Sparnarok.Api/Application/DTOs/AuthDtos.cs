namespace Sparnarok.Api.Application.DTOs;

/// <summary>
/// Solicitud de autenticación social con credencial o token ID de Google OAuth2.
/// </summary>
/// <param name="IdToken">Token ID firmado por Google emitido tras la autorización del usuario.</param>
public record GoogleLoginRequestDto(
    string IdToken);

/// <summary>
/// Respuesta devuelta tras una autenticación exitosa, conteniendo el JWT propio de Sparnarok y los datos del perfil.
/// </summary>
/// <param name="Token">Token JWT firmado por la API de Sparnarok.</param>
/// <param name="UserId">Identificador único del usuario.</param>
/// <param name="Email">Correo electrónico verificado del usuario.</param>
/// <param name="DisplayName">Nombre para mostrar en la interfaz.</param>
/// <param name="AvatarUrl">URL de la foto de perfil del usuario (opcional).</param>
public record AuthResponseDto(
    string Token,
    Guid UserId,
    string Email,
    string DisplayName,
    string? AvatarUrl);

/// <summary>
/// Carga útil (Payload) extraída y verificada criptográficamente desde un token ID de Google.
/// </summary>
/// <param name="Subject">Identificador 'sub' único del usuario en el ecosistema Google.</param>
/// <param name="Email">Correo electrónico verificado.</param>
/// <param name="Name">Nombre completo del usuario.</param>
/// <param name="Picture">URL de la imagen de perfil (opcional).</param>
public record GoogleTokenPayload(
    string Subject,
    string Email,
    string Name,
    string? Picture);
