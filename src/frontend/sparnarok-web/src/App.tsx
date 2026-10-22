import { useState, useEffect, useCallback } from 'react';
import { useTranslation } from 'react-i18next';
import './App.css';
import type {
  CreatureResponseDto,
  WeaponMasteryResponseDto,
  CreateCreatureRequestDto,
  CreateActionRequestDto,
  WeaponDto,
} from './types/dnd';
import { apiClient } from './services/apiClient';
import { Navbar } from './components/Navbar';
import { BestiaryView } from './components/BestiaryView';
import { CombatSimulatorView } from './components/CombatSimulatorView';
import { WeaponIngestionModal } from './components/WeaponIngestionModal';
import { GoogleOAuthProvider } from '@react-oauth/google';
import { AuthProvider } from './context/AuthContext';

const GOOGLE_CLIENT_ID =
  (import.meta as any).env?.VITE_GOOGLE_CLIENT_ID ||
  'mock-google-client-id.apps.googleusercontent.com';

/**
 * Componente principal de la interfaz de usuario de Sparnarok: The Architect's Codex.
 * Orquesta la navegación entre el Bestiario de D&D 5e (2024) y el Simulador Matemático de Combate.
 *
 * @returns {JSX.Element} Vista renderizada del Códice con soporte de internacionalización (i18n).
 */
export function AppContent() {
  const { t } = useTranslation();

  const [activeTab, setActiveTab] = useState<'bestiary' | 'simulation'>('bestiary');
  const [simulationCount, setSimulationCount] = useState<number>(0);

  const [creatures, setCreatures] = useState<CreatureResponseDto[]>([]);
  const [allMasteries, setAllMasteries] = useState<WeaponMasteryResponseDto[]>([]);
  const [weapons, setWeapons] = useState<WeaponDto[]>([]);
  const [isCreateModalOpen, setIsCreateModalOpen] = useState<boolean>(false);
  const [isIngestionModalOpen, setIsIngestionModalOpen] = useState<boolean>(false);

  // Carga inicial de criaturas, catálogo de maestrías y armas
  useEffect(() => {
    const controller = new AbortController();

    const loadData = async () => {
      try {
        const [creatureList, masteryList, weaponList] = await Promise.all([
          apiClient.getCreatures(controller.signal),
          apiClient.getWeaponMasteries(controller.signal),
          apiClient.getWeapons(controller.signal),
        ]);
        setCreatures(creatureList);
        setAllMasteries(masteryList);
        setWeapons(weaponList);
      } catch {
        // En caso de error o aborto, el cliente ya maneja fallback local
      }
    };

    loadData();
    return () => controller.abort();
  }, []);

  /**
   * Incrementa el contador de simulaciones de combate ejecutadas.
   */
  const handleIncrementSimulation = useCallback(() => {
    setSimulationCount((prev) => prev + 1);
  }, []);

  /**
   * Crea una nueva criatura en el backend y sincroniza el estado local.
   */
  const handleCreateCreature = useCallback(async (data: CreateCreatureRequestDto) => {
    const created = await apiClient.createCreature(data);
    setCreatures((prev) => [created, ...prev.filter((c) => c.id !== created.id)]);
  }, []);

  /**
   * Elimina una criatura del backend y actualiza el estado local.
   */
  const handleDeleteCreature = useCallback(async (id: string) => {
    await apiClient.deleteCreature(id);
    setCreatures((prev) => prev.filter((c) => c.id !== id));
  }, []);

  /**
   * Incorpora una acción táctica a una criatura existente.
   */
  const handleAddAction = useCallback(async (creatureId: string, data: CreateActionRequestDto) => {
    const newAction = await apiClient.addAction(creatureId, data);
    setCreatures((prev) =>
      prev.map((c) => {
        if (c.id === creatureId) {
          return {
            ...c,
            actions: [...c.actions, newAction],
          };
        }
        return c;
      })
    );
  }, []);

  /**
   * Asigna el aprendizaje de una maestría de armas a una criatura.
   */
  const handleLearnMastery = useCallback(async (creatureId: string, masteryId: string) => {
    const updated = await apiClient.learnMastery(creatureId, masteryId);
    setCreatures((prev) =>
      prev.map((c) => (c.id === creatureId ? updated : c))
    );
  }, []);

  return (
    <div className="app-wrapper">
      {/* Barra de Navegación y Acciones Globales */}
      <Navbar
        activeTab={activeTab}
        onTabChange={setActiveTab}
        onOpenCreateModal={() => setIsCreateModalOpen(true)}
        onOpenIngestionModal={() => setIsIngestionModalOpen(true)}
      />

      {/* Hero Section */}
      <section className="hero-section">
        <h1 className="hero-title">{t('app.title')}</h1>
        <p className="hero-subtitle">{t('app.subtitle')}</p>
      </section>

      {/* Widget Interactivo de Simulación Rápida */}
      <div className="quick-sim-bar">
        <button
          type="button"
          className="counter-button"
          id="simulation-counter-btn"
          onClick={handleIncrementSimulation}
          aria-label={t('app.counterButton', { count: simulationCount })}
        >
          🎲 {t('app.counterButton', { count: simulationCount })}
        </button>
      </div>

      {/* Vista Activa: Bestiario o Simulador */}
      <main>
        {activeTab === 'bestiary' ? (
          <BestiaryView
            creatures={creatures}
            allMasteries={allMasteries}
            isCreateModalOpen={isCreateModalOpen}
            onCloseCreateModal={() => setIsCreateModalOpen(false)}
            onCreateCreature={handleCreateCreature}
            onDeleteCreature={handleDeleteCreature}
            onAddAction={handleAddAction}
            onLearnMastery={handleLearnMastery}
          />
        ) : (
          <div className="simulation-tab-container fade-in">
            {/* Vista del Simulador Táctico de Montecarlo */}
            <CombatSimulatorView
              creatures={creatures}
              weapons={weapons}
              onRunSimulation={apiClient.runMontecarlo}
              onSimulationCompleted={handleIncrementSimulation}
              onHeroCreated={(hero) => setCreatures((prev) => [hero, ...prev.filter((c) => c.id !== hero.id)])}
            />

            {/* Grid de capacidades del motor de simulación */}
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
          </div>
        )}
      </main>

      {/* Modal de Ingesta Masiva de Armas (CSV) */}
      <WeaponIngestionModal
        isOpen={isIngestionModalOpen}
        onClose={() => setIsIngestionModalOpen(false)}
        onUpload={apiClient.uploadWeaponsCsv}
      />
    </div>
  );
}

export function App() {
  return (
    <GoogleOAuthProvider clientId={GOOGLE_CLIENT_ID}>
      <AuthProvider>
        <AppContent />
      </AuthProvider>
    </GoogleOAuthProvider>
  );
}

export default App;

