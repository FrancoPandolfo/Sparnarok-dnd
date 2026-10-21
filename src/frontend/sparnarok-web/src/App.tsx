import { useState, useCallback } from 'react';
import { useTranslation } from 'react-i18next';
import './App.css';

/**
 * Componente principal de la interfaz de usuario de Sparnarok: The Architect's Codex.
 * Proporciona el punto de entrada a la simulación matemática de D&D 5e y orquestación de mundo.
 *
 * @returns {JSX.Element} Vista renderizada del Códice con soporte de internacionalización (i18n).
 */
export function App() {
  const { t, i18n } = useTranslation();
  const [simulationCount, setSimulationCount] = useState<number>(0);

  /**
   * Conmuta dinámicamente entre los idiomas soportados (Español / Inglés).
   */
  const handleToggleLanguage = useCallback(() => {
    const nextLanguage = i18n.language.startsWith('es') ? 'en' : 'es';
    i18n.changeLanguage(nextLanguage);
  }, [i18n]);

  /**
   * Incrementa el contador de simulaciones de combate ejecutadas.
   */
  const handleIncrementSimulation = useCallback(() => {
    setSimulationCount((prev) => prev + 1);
  }, []);

  return (
    <main className="app-container">
      {/* Barra superior con indicador de estado y conmutador de idioma */}
      <header className="top-bar">
        <div className="badge">
          <span>{t('app.currentLanguage')}</span>
        </div>
        <button
          type="button"
          className="lang-btn"
          id="lang-toggle-btn"
          onClick={handleToggleLanguage}
          aria-label={t('app.switchLanguage')}
        >
          {t('app.switchLanguage')}
        </button>
      </header>

      {/* Sección Hero principal */}
      <section className="hero-section">
        <h1 className="hero-title">{t('app.title')}</h1>
        <p className="hero-subtitle">{t('app.subtitle')}</p>
      </section>

      {/* Tarjeta interactiva de simulación de combate */}
      <section className="interactive-card">
        <button
          type="button"
          className="counter-button"
          id="simulation-counter-btn"
          onClick={handleIncrementSimulation}
        >
          {t('app.counterButton', { count: simulationCount })}
        </button>
      </section>

      {/* Grid de capacidades del motor */}
      <section className="features-grid">
        <article className="feature-box">
          <h2 className="feature-title">Combat Math Matrix</h2>
          <p className="feature-desc">{t('app.features.combatMath')}</p>
        </article>
        <article className="feature-box">
          <h2 className="feature-title">World Orchestration</h2>
          <p className="feature-desc">{t('app.features.worldOrchestration')}</p>
        </article>
        <article className="feature-box">
          <h2 className="feature-title">Architectural Rigor</h2>
          <p className="feature-desc">{t('app.features.architecture')}</p>
        </article>
      </section>

      {/* Enlaces de documentación técnica */}
      <footer className="doc-section">
        <span className="feature-title">{t('app.documentation.title')}</span>
        <nav className="doc-links">
          <a
            href="http://localhost:5000/swagger"
            target="_blank"
            rel="noopener noreferrer"
            className="doc-link"
          >
            {t('app.documentation.swaggerLink')} &rarr;
          </a>
          <a
            href="https://github.com/FrancoPandolfo/Sparnarok-dnd/tree/main/docs/adrs"
            target="_blank"
            rel="noopener noreferrer"
            className="doc-link"
          >
            {t('app.documentation.adrsLink')} &rarr;
          </a>
        </nav>
      </footer>
    </main>
  );
}

export default App;
