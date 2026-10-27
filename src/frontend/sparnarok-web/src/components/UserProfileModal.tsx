import React, { useState } from 'react';
import { GoogleLogin } from '@react-oauth/google';
import { useAuth } from '../context/AuthContext';

export interface UserProfileModalProps {
  isOpen: boolean;
  onClose: () => void;
}

/**
 * Modal de perfil de usuario y gestión de cuentas vinculadas
 * (Google OAuth2 + Contraseña convencional para login bidireccional).
 */
export const UserProfileModal: React.FC<UserProfileModalProps> = ({ isOpen, onClose }) => {
  const { user, setPassword, linkGoogle, updateProfile, logout } = useAuth();

  const [nicknameInput, setNicknameInput] = useState(user?.globalNickname || '');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [isEditingPassword, setIsEditingPassword] = useState(false);
  const [statusMessage, setStatusMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  React.useEffect(() => {
    if (user?.globalNickname) {
      setNicknameInput(user.globalNickname);
    }
  }, [user?.globalNickname]);

  if (!isOpen || !user) return null;

  const handleSaveNickname = async (e: React.FormEvent) => {
    e.preventDefault();
    setStatusMessage(null);
    setIsSubmitting(true);
    try {
      await updateProfile(nicknameInput.trim() || null);
      setStatusMessage({ type: 'success', text: '¡Apodo global actualizado correctamente!' });
    } catch (err: any) {
      setStatusMessage({ type: 'error', text: err.message || 'Error al actualizar el apodo.' });
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleSetPassword = async (e: React.FormEvent) => {
    e.preventDefault();
    setStatusMessage(null);

    if (newPassword.length < 6) {
      setStatusMessage({ type: 'error', text: 'La contraseña debe contener al menos 6 caracteres.' });
      return;
    }

    if (newPassword !== confirmPassword) {
      setStatusMessage({ type: 'error', text: 'Las contraseñas no coinciden.' });
      return;
    }

    setIsSubmitting(true);
    try {
      await setPassword(newPassword);
      setStatusMessage({ type: 'success', text: '¡Contraseña establecida exitosamente! Ahora puedes iniciar sesión con tu email y contraseña.' });
      setNewPassword('');
      setConfirmPassword('');
      setIsEditingPassword(false);
    } catch (err: any) {
      setStatusMessage({ type: 'error', text: err.message || 'Error al establecer la contraseña.' });
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleGoogleLinkSuccess = async (credentialResponse: any) => {
    if (credentialResponse.credential) {
      setIsSubmitting(true);
      setStatusMessage(null);
      try {
        await linkGoogle(credentialResponse.credential);
        setStatusMessage({ type: 'success', text: '¡Cuenta de Google vinculada exitosamente! Ahora puedes usar tanto Google como contraseña.' });
      } catch (err: any) {
        setStatusMessage({ type: 'error', text: err.message || 'Error al vincular con Google.' });
      } finally {
        setIsSubmitting(false);
      }
    }
  };

  return (
    <div className="modal-overlay" onClick={onClose} data-testid="user-profile-modal-overlay">
      <div
        className="modal-content profile-modal-box fade-in"
        onClick={(e) => e.stopPropagation()}
        role="dialog"
        aria-modal="true"
        aria-labelledby="profile-modal-title"
      >
        <button
          type="button"
          className="modal-close-btn"
          onClick={onClose}
          aria-label="Cerrar modal"
        >
          ✕
        </button>

        <div className="profile-header">
          {user.avatarUrl ? (
            <img src={user.avatarUrl} alt={user.displayName} className="profile-large-avatar" />
          ) : (
            <div className="profile-large-fallback">
              {user.displayName ? user.displayName.charAt(0).toUpperCase() : 'U'}
            </div>
          )}
          <div className="profile-header-info">
            <h2 id="profile-modal-title" className="profile-display-name">{user.displayName}</h2>
            <span className="profile-email">{user.email}</span>
          </div>
        </div>

        {statusMessage && (
          <div className={`auth-alert ${statusMessage.type === 'success' ? 'success-alert' : 'error-alert'}`} role="alert">
            {statusMessage.type === 'success' ? '✓ ' : '⚠️ '}
            {statusMessage.text}
          </div>
        )}

        {/* Sección de Mi Perfil (Identidad Global) */}
        <div className="profile-section" id="my-profile-section">
          <h3 className="section-title">👤 Mi Perfil (Identidad Global)</h3>
          <p className="section-description">
            Configura tu apodo global visible para las parties y compañeros de mesa.
          </p>
          <form onSubmit={handleSaveNickname} id="profile-nickname-form" className="profile-nickname-form">
            <div className="form-group">
              <label htmlFor="global-nickname-input" className="form-label">
                Apodo Global:
              </label>
              <input
                type="text"
                id="global-nickname-input"
                className="form-input"
                placeholder="Ej: GandalfElGris, Valeros..."
                value={nicknameInput}
                onChange={(e) => setNicknameInput(e.target.value)}
                maxLength={50}
              />
            </div>
            <div className="form-actions-row">
              <button
                type="submit"
                id="save-global-nickname-btn"
                className="btn btn-primary primary-gold"
                disabled={isSubmitting}
              >
                {isSubmitting ? 'Guardando...' : 'Guardar Apodo'}
              </button>
            </div>
          </form>
        </div>

        {/* Sección de Vinculación de Cuentas */}
        <div className="profile-section">
          <h3 className="section-title">🛡️ Seguridad y Vinculación de Métodos de Acceso</h3>
          <p className="section-description">
            Puedes acceder con Google, con contraseña convencional, o vincular ambos métodos al mismo tiempo sin conflicto.
          </p>

          <div className="account-link-cards">
            {/* Tarjeta de Google */}
            <div className="link-card">
              <div className="link-card-info">
                <span className="link-provider-name">Google OAuth2</span>
                {user.hasGoogleLinked ? (
                  <span className="badge-linked">✓ Vinculado</span>
                ) : (
                  <span className="badge-not-linked">No vinculado</span>
                )}
              </div>
              {!user.hasGoogleLinked && (
                <div className="link-action-wrapper" data-testid="google-link-wrapper">
                  <GoogleLogin
                    onSuccess={handleGoogleLinkSuccess}
                    onError={() => setStatusMessage({ type: 'error', text: 'No se pudo conectar con Google.' })}
                    shape="pill"
                    size="medium"
                    text="continue_with"
                  />
                </div>
              )}
            </div>

            {/* Tarjeta de Contraseña Convencional */}
            <div className="link-card">
              <div className="link-card-info">
                <span className="link-provider-name">Contraseña Convencional</span>
                {user.hasPassword ? (
                  <span className="badge-linked">✓ Configurada</span>
                ) : (
                  <span className="badge-not-linked">Sin configurar</span>
                )}
              </div>

              {!isEditingPassword ? (
                <button
                  type="button"
                  className="btn btn-secondary btn-sm"
                  onClick={() => setIsEditingPassword(true)}
                >
                  {user.hasPassword ? 'Cambiar Contraseña' : 'Crear Contraseña'}
                </button>
              ) : (
                <form onSubmit={handleSetPassword} className="set-password-form">
                  <input
                    type="password"
                    className="form-input"
                    placeholder="Nueva contraseña (min. 6 caracteres)"
                    value={newPassword}
                    onChange={(e) => setNewPassword(e.target.value)}
                    required
                  />
                  <input
                    type="password"
                    className="form-input"
                    placeholder="Confirmar nueva contraseña"
                    value={confirmPassword}
                    onChange={(e) => setConfirmPassword(e.target.value)}
                    required
                  />
                  <div className="form-actions-row">
                    <button
                      type="submit"
                      className="btn btn-primary primary-gold"
                      disabled={isSubmitting}
                    >
                      {isSubmitting ? 'Guardando...' : 'Guardar Contraseña'}
                    </button>
                    <button
                      type="button"
                      className="btn btn-secondary cancel-btn"
                      onClick={() => {
                        setIsEditingPassword(false);
                        setNewPassword('');
                        setConfirmPassword('');
                      }}
                    >
                      Cancelar
                    </button>
                  </div>
                </form>
              )}
            </div>
          </div>
        </div>

        <div className="profile-modal-footer">
          <button
            type="button"
            id="modal-logout-btn"
            className="btn btn-danger logout-btn"
            onClick={() => {
              logout();
              onClose();
            }}
          >
            🚪 Cerrar Sesión
          </button>
        </div>
      </div>
    </div>
  );
};
