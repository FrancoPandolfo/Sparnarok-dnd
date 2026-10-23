/**
 * Perfil y sesión del usuario autenticado en el sistema Sparnarok.
 */
export interface AuthUser {
  id: string;
  email: string;
  displayName: string;
  globalNickname?: string | null;
  avatarUrl?: string | null;
  token: string;
  hasPassword?: boolean;
  hasGoogleLinked?: boolean;
}

/**
 * Solicitud de intercambio de credencial Google por token propio.
 */
export interface GoogleLoginRequestDto {
  idToken: string;
}

/**
 * Solicitud de registro convencional de cuenta nueva con Email y Contraseña.
 */
export interface RegisterRequestDto {
  email: string;
  password: string;
  displayName: string;
}

/**
 * Solicitud de inicio de sesión convencional con Email y Contraseña.
 */
export interface LoginRequestDto {
  email: string;
  password: string;
}

/**
 * Solicitud para configurar o actualizar la contraseña convencional.
 */
export interface SetPasswordRequestDto {
  newPassword: string;
}

/**
 * Solicitud de actualización de perfil global (Fase 3.7).
 */
export interface UpdateUserProfileRequestDto {
  globalNickname: string | null;
}

/**
 * Información detallada del perfil de usuario (Fase 3.7).
 */
export interface UserProfileResponseDto {
  userId: string;
  email: string;
  displayName: string;
  globalNickname?: string | null;
  avatarUrl?: string | null;
  hasPassword: boolean;
  hasGoogleLinked: boolean;
}

/**
 * Respuesta del backend tras autenticación exitosa (Google o Convencional).
 */
export interface AuthResponseDto {
  token: string;
  userId: string;
  email: string;
  displayName: string;
  globalNickname?: string | null;
  avatarUrl?: string | null;
  hasPassword?: boolean;
  hasGoogleLinked?: boolean;
}
