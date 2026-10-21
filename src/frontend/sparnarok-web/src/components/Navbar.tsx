import React from 'react';
import { useTranslation } from 'react-i18next';

export interface NavbarProps {
  activeTab: 'bestiary' | 'simulation';
  onTabChange: (tab: 'bestiary' | 'simulation') => void;
  onOpenCreateModal: () => void;
  onOpenIngestionModal: () => void;
}

/**
 * Barra superior de navegación y comandos globales de Sparnarok.
 */
export const Navbar: React.FC<NavbarProps> = ({
  activeTab,
  onTabChange,
  onOpenCreateModal,
  onOpenIngestionModal,
}) => {
  const { t, i18n } = useTranslation();

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
      </div>
    </header>
  );
};
