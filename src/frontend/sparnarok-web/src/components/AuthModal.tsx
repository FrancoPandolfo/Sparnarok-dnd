import React, { useState } from 'react';
import { GoogleLogin } from '@react-oauth/google';
import { useAuth } from '../context/AuthContext';

export interface AuthModalProps {
  isOpen: boolean;
  onClose: () => void;
  initialTab?: 'login' | 'register';
}

/**
 * Modal unificado de autenticación: Acceso convencional (Email/Contraseña)
 * y Acceso federado (Google OAuth2) con soporte de fallback para desarrollo.
 */
export const AuthModal: React.FC<AuthModalProps> = ({
  isOpen,
  onClose,
  initialTab = 'login',
}) => {
  const { loginConventional, registerConventional, loginWithGoogle } = useAuth();

  const [activeTab, setActiveTab] = useState<'login' | 'register'>(initialTab);
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [displayName, setDisplayName] = useState('');

  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [googleError, setGoogleError] = useState<string | null>(null);

  if (!isOpen) return null;

  const handleReset = () => {
    setEmail('');
    setPassword('');
    setDisplayName('');
    setErrorMessage(null);
    setSuccessMessage(null);
    setGoogleError(null);
  };

  const handleClose = () => {
    handleReset();
    onClose();
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMessage(null);
    setSuccessMessage(null);
    setIsSubmitting(true);

    try {
      if (activeTab === 'login') {
        if (!email.trim() || !password) {
          setErrorMessage('Por favor, ingresa tu correo y contraseña.');
          setIsSubmitting(false);
          return;
        }
        await loginConventional(email.trim(), password);
        handleClose();
      } else {
        if (!displayName.trim() || !email.trim() || !password) {
          setErrorMessage('Todos los campos son obligatorios.');
          setIsSubmitting(false);
          return;
        }
        if (password.length < 6) {
          setErrorMessage('La contraseña debe contener al menos 6 caracteres.');
          setIsSubmitting(false);
          return;
        }
        await registerConventional(email.trim(), password, displayName.trim());
        handleClose();
      }
    } catch (err: any) {
      setErrorMessage(err.message || 'Ocurrió un error en la autenticación.');
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleGoogleSuccess = async (credentialResponse: any) => {
    if (credentialResponse.credential) {
      try {
        setIsSubmitting(true);
        await loginWithGoogle(credentialResponse.credential);
        handleClose();
      } catch (err: any) {
        setErrorMessage(err.message || 'Error al validar credencial de Google.');
      } finally {
        setIsSubmitting(false);
      }
    }
  };

  const handleGoogleError = () => {
    setGoogleError(
      'Google bloqueó el origen local (Error 401: no registered origin). Agrega http://localhost:5173 en Orígenes de JavaScript autorizados en tu consola de Google Cloud, o usa el botón de prueba de desarrollo debajo.'
    );
  };

  const handleDevMockLogin = async () => {
    setIsSubmitting(true);
    try {
      await loginWithGoogle(
        `mock-google-token:${email || 'adventurer@sparnarok.dnd'}:${displayName || 'Aventurero del Códice'}:sub-12345:https://lh3.googleusercontent.com/a/default-user=s96-c`
      );
      handleClose();
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="modal-overlay" onClick={handleClose} data-testid="auth-modal-overlay">
      <div
        className="modal-content auth-modal-box fade-in"
        onClick={(e) => e.stopPropagation()}
        role="dialog"
        aria-modal="true"
        aria-labelledby="auth-modal-title"
      >
        <button
          type="button"
          className="modal-close-btn"
          onClick={handleClose}
          aria-label="Cerrar modal"
        >
          ✕
        </button>

        <div className="auth-modal-header">
          <div className="auth-modal-icon">⚔️</div>
          <h2 id="auth-modal-title" className="auth-modal-title">
            {activeTab === 'login' ? 'Iniciar Sesión' : 'Crear Cuenta'}
          </h2>
          <p className="auth-modal-subtitle">
            Sparnarok: The Architect's Codex (D&amp;D 5e)
          </p>
        </div>

        {/* Pestañas de Selección */}
        <div className="auth-tab-selector">
          <button
            type="button"
            className={`auth-tab-btn ${activeTab === 'login' ? 'active' : ''}`}
            onClick={() => {
              setActiveTab('login');
              setErrorMessage(null);
            }}
          >
            Ingresar
          </button>
          <button
            type="button"
            className={`auth-tab-btn ${activeTab === 'register' ? 'active' : ''}`}
            onClick={() => {
              setActiveTab('register');
              setErrorMessage(null);
            }}
          >
            Registrarse
          </button>
        </div>

        {errorMessage && (
          <div className="auth-alert error-alert" role="alert">
            ⚠️ {errorMessage}
          </div>
        )}

        {successMessage && (
          <div className="auth-alert success-alert" role="alert">
            ✓ {successMessage}
          </div>
        )}

        {/* Formulario Convencional */}
        <form onSubmit={handleSubmit} className="auth-form">
          {activeTab === 'register' && (
            <div className="form-group">
              <label htmlFor="auth-name-input">Nombre o Alias</label>
              <input
                id="auth-name-input"
                type="text"
                className="form-input"
                placeholder="Ej. Gandalf el Gris"
                value={displayName}
                onChange={(e) => setDisplayName(e.target.value)}
                required
              />
            </div>
          )}

          <div className="form-group">
            <label htmlFor="auth-email-input">Correo Electrónico</label>
            <input
              id="auth-email-input"
              type="email"
              className="form-input"
              placeholder="tu-correo@ejemplo.com"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              required
            />
          </div>

          <div className="form-group">
            <label htmlFor="auth-password-input">Contraseña</label>
            <input
              id="auth-password-input"
              type="password"
              className="form-input"
              placeholder={activeTab === 'register' ? 'Mínimo 6 caracteres' : 'Tu contraseña'}
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              required
            />
          </div>

          <button
            type="submit"
            id="auth-submit-btn"
            className="btn btn-primary btn-block auth-submit-btn primary-gold"
            disabled={isSubmitting}
          >
            {isSubmitting ? 'Procesando...' : activeTab === 'login' ? 'Iniciar Sesión' : 'Crear Cuenta'}
          </button>
        </form>

        <div className="auth-divider">
          <span>o continúa con</span>
        </div>

        {/* Botón Oficial Google OAuth */}
        <div className="google-auth-wrapper" data-testid="google-oauth-login">
          <GoogleLogin
            onSuccess={handleGoogleSuccess}
            onError={handleGoogleError}
            shape="rectangular"
            size="large"
            theme="filled_black"
            text={activeTab === 'login' ? 'signin_with' : 'signup_with'}
            width="100%"
          />
        </div>

        {googleError && (
          <div className="auth-notice-box">
            <p className="notice-text">
              ℹ️ <strong>Configuración de Google OAuth:</strong> {googleError}
            </p>
          </div>
        )}

        {/* Bypass Desarrollador / Demo Local */}
        <div className="dev-mode-auth-section">
          <button
            type="button"
            id="dev-login-btn"
            className="btn btn-outline btn-sm btn-block dev-login-bypass-btn"
            onClick={handleDevMockLogin}
            title="Iniciar sesión de desarrollo sin pasar por Google Cloud"
          >
            🧪 Acceso Rápido Desarrollador (Sin Google)
          </button>
        </div>
      </div>
    </div>
  );
};
