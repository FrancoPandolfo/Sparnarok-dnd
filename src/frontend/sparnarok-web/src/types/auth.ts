/**
 * Perfil y sesión del usuario autenticado en el sistema Sparnarok.
 */
export interface AuthUser {
  id: string;
  email: string;
  displayName: string;
  avatarUrl?: string | null;
  token: string;
}

/**
 * Solicitud de intercambio de credencial Google por token propio.
 */
export interface GoogleLoginRequestDto {
  idToken: string;
}

/**
 * Respuesta del backend tras autenticación social exitosa con Google OAuth2.
 */
export interface AuthResponseDto {
  token: string;
  userId: string;
  email: string;
  displayName: string;
  avatarUrl?: string | null;
}
