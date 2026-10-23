namespace Sparnarok.Api.Application.DTOs;

/// <summary>
/// Solicitud de autenticación social con credencial o token ID de Google OAuth2.
/// </summary>
/// <param name="IdToken">Token ID firmado por Google emitido tras la autorización del usuario.</param>
public record GoogleLoginRequestDto(
    string IdToken);

/// <summary>
/// Solicitud de registro convencional de cuenta nueva mediante Email y Contraseña.
/// </summary>
/// <param name="Email">Correo electrónico del nuevo usuario.</param>
/// <param name="Password">Contraseña segura (mínimo 6 caracteres).</param>
/// <param name="DisplayName">Nombre o alias para mostrar.</param>
public record RegisterRequestDto(
    string Email,
    string Password,
    string DisplayName);

/// <summary>
/// Solicitud de inicio de sesión convencional con Email y Contraseña.
/// </summary>
/// <param name="Email">Correo electrónico registrado.</param>
/// <param name="Password">Contraseña del usuario.</param>
public record LoginRequestDto(
    string Email,
    string Password);

/// <summary>
/// Solicitud para establecer o actualizar la contraseña convencional de una cuenta.
/// </summary>
/// <param name="NewPassword">Nueva contraseña a establecer.</param>
public record SetPasswordRequestDto(
    string NewPassword);

/// <summary>
/// Respuesta devuelta tras una autenticación exitosa, conteniendo el JWT propio de Sparnarok y los datos del perfil.
/// </summary>
/// <param name="Token">Token JWT firmado por la API de Sparnarok.</param>
/// <param name="UserId">Identificador único del usuario.</param>
/// <param name="Email">Correo electrónico verificado del usuario.</param>
/// <param name="DisplayName">Nombre para mostrar en la interfaz.</param>
/// <param name="AvatarUrl">URL de la foto de perfil del usuario (opcional).</param>
/// <param name="HasPassword">Indica si la cuenta cuenta con contraseña para login convencional.</param>
/// <param name="HasGoogleLinked">Indica si la cuenta está vinculada a Google OAuth2.</param>
public record AuthResponseDto(
    string Token,
    Guid UserId,
    string Email,
    string DisplayName,
    string? AvatarUrl,
    bool HasPassword = false,
    bool HasGoogleLinked = false);

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
