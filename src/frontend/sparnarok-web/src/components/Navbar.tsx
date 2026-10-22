import React from 'react';
import { useTranslation } from 'react-i18next';
import { GoogleLogin } from '@react-oauth/google';
import { useAuth } from '../context/AuthContext';

export interface NavbarProps {
  activeTab: 'bestiary' | 'simulation';
  onTabChange: (tab: 'bestiary' | 'simulation') => void;
  onOpenCreateModal: () => void;
  onOpenIngestionModal: () => void;
}

/**
 * Barra superior de navegación, comandos globales e identidad de usuario (Google OAuth2 + JWT).
 */
export const Navbar: React.FC<NavbarProps> = ({
  activeTab,
  onTabChange,
  onOpenCreateModal,
  onOpenIngestionModal,
}) => {
  const { t, i18n } = useTranslation();
  const { user, isAuthenticated, loginWithGoogle, logout } = useAuth();

  const handleToggleLanguage = () => {
    const nextLang = i18n.language.startsWith('es') ? 'en' : 'es';
    i18n.changeLanguage(nextLang);
  };

  return (
    <header className="navbar-container">
      <div className="navbar-brand">
        <div className="brand-icon">⚔️</div>
        <div className="brand-text">
          <span className="brand-title">SPARNAROK</span>
          <span className="brand-subtitle">THE ARCHITECT'S CODEX</span>
        </div>
      </div>

      <nav className="navbar-tabs" aria-label="Navegación principal">
        <button
          type="button"
          id="tab-bestiary-btn"
          className={`nav-tab-btn ${activeTab === 'bestiary' ? 'active' : ''}`}
          onClick={() => onTabChange('bestiary')}
        >
          📖 {t('app.nav.bestiary')}
        </button>
        <button
          type="button"
          id="tab-simulation-btn"
          className={`nav-tab-btn ${activeTab === 'simulation' ? 'active' : ''}`}
          onClick={() => onTabChange('simulation')}
        >
          🎲 {t('app.nav.simulation')}
        </button>
      </nav>

      <div className="navbar-actions">
        <button
          type="button"
          id="open-ingestion-btn"
          className="action-btn ingestion-btn"
          onClick={onOpenIngestionModal}
          title={t('app.nav.ingestCsv')}
        >
          📥 {t('app.nav.ingestCsv')}
        </button>
        {activeTab === 'bestiary' && (
          <button
            type="button"
            id="create-creature-btn"
            className="action-btn primary-gold"
            onClick={onOpenCreateModal}
          >
            {t('app.nav.newCreature')}
          </button>
        )}
        <button
          type="button"
          id="lang-toggle-btn"
          className="action-btn lang-toggle"
          onClick={handleToggleLanguage}
          aria-label={t('app.switchLanguage')}
        >
          🌐 {t('app.switchLanguage')}
        </button>

        {/* Zona de Identidad y Autenticación de Usuario */}
        {isAuthenticated && user ? (
          <div className="user-profile-badge" id="user-profile-badge">
            {user.avatarUrl ? (
              <img
                id="user-avatar"
                src={user.avatarUrl}
                alt={user.displayName}
                className="user-avatar-img"
              />
            ) : (
              <div id="user-avatar" className="user-avatar-fallback">
                {user.displayName ? user.displayName.charAt(0).toUpperCase() : 'U'}
              </div>
            )}
            <span id="user-display-name" className="user-display-name">
              {user.displayName}
            </span>
            <button
              type="button"
              id="logout-btn"
              className="action-btn logout-btn"
              onClick={logout}
              title={t('app.auth.logout')}
            >
              🚪 {t('app.auth.logout')}
            </button>
          </div>
        ) : (
          <div className="auth-actions-group">
            <div className="google-official-login-wrapper" data-testid="google-official-login">
              <GoogleLogin
                onSuccess={async (credentialResponse) => {
                  if (credentialResponse.credential) {
                    await loginWithGoogle(credentialResponse.credential);
                  }
                }}
                onError={() => {
                  console.error('Error al iniciar sesión con Google');
                }}
                shape="pill"
                size="medium"
                theme="filled_black"
              />
            </div>
            <button
              type="button"
              id="google-login-btn"
              className="action-btn google-login-btn"
              onClick={() =>
                loginWithGoogle(
                  'mock-google-token:adventurer@sparnarok.dnd:Aventurero del Códice:sub-12345:https://lh3.googleusercontent.com/a/default-user=s96-c'
                )
              }
              title={t('app.auth.loginWithGoogle')}
            >
              <svg className="google-icon" viewBox="0 0 24 24" width="16" height="16">
                <path
                  fill="#4285F4"
                  d="M22.56 12.25c0-.78-.07-1.53-.2-2.25H12v4.26h5.92c-.26 1.37-1.04 2.53-2.21 3.31v2.77h3.57c2.08-1.92 3.28-4.74 3.28-8.09z"
                />
                <path
                  fill="#34A853"
                  d="M12 23c2.97 0 5.46-.98 7.28-2.66l-3.57-2.77c-.98.66-2.23 1.06-3.71 1.06-2.86 0-5.29-1.93-6.16-4.53H2.18v2.84C3.99 20.53 7.7 23 12 23z"
                />
                <path
                  fill="#FBBC05"
                  d="M5.84 14.09c-.22-.66-.35-1.36-.35-2.09s.13-1.43.35-2.09V7.06H2.18C1.43 8.55 1 10.22 1 12s.43 3.45 1.18 4.94l2.85-2.22.81-.63z"
                />
                <path
                  fill="#EA4335"
                  d="M12 5.38c1.62 0 3.06.56 4.21 1.64l3.15-3.15C17.45 2.09 14.97 1 12 1 7.7 1 3.99 3.47 2.18 7.06l3.66 2.84c.87-2.6 3.3-4.52 6.16-4.52z"
                />
              </svg>
              <span>{t('app.auth.loginWithGoogle')}</span>
            </button>
          </div>
        )}
      </div>
    </header>
  );
};
