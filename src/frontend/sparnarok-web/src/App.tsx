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
import type { PartyResponseDto } from './types/partyEncounter';
import { apiClient } from './services/apiClient';
import { Navbar } from './components/Navbar';
import { BestiaryView } from './components/BestiaryView';
import { CombatSimulatorView } from './components/CombatSimulatorView';
import { WeaponIngestionModal } from './components/WeaponIngestionModal';
import { LiveSessionModal } from './components/LiveSessionModal';
import { PartyOnboardingModal } from './components/PartyOnboardingModal';
import { PartyManagerModal } from './components/PartyManagerModal';
import { GoogleOAuthProvider } from '@react-oauth/google';
import { AuthProvider, useAuth } from './context/AuthContext';

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
  const { user } = useAuth();

  const [activeTab, setActiveTab] = useState<'bestiary' | 'simulation'>('bestiary');
  const [simulationCount, setSimulationCount] = useState<number>(0);

  const [creatures, setCreatures] = useState<CreatureResponseDto[]>([]);
  const [allMasteries, setAllMasteries] = useState<WeaponMasteryResponseDto[]>([]);
  const [weapons, setWeapons] = useState<WeaponDto[]>([]);
  const [parties, setParties] = useState<PartyResponseDto[]>([]);
  const [isCreateModalOpen, setIsCreateModalOpen] = useState<boolean>(false);
  const [isIngestionModalOpen, setIsIngestionModalOpen] = useState<boolean>(false);
  const [isLiveSessionOpen, setIsLiveSessionOpen] = useState<boolean>(false);
  const [isPartiesModalOpen, setIsPartiesModalOpen] = useState<boolean>(false);
  const [liveSessionPartyId, setLiveSessionPartyId] = useState<string | undefined>(undefined);
  const [inviteToken, setInviteToken] = useState<string | null>(null);
  const [isOnboardingModalOpen, setIsOnboardingModalOpen] = useState<boolean>(false);

  // Detección reactiva de invitación en la URL (?inviteToken=... o ?joinToken=...)
  useEffect(() => {
    const params = new URLSearchParams(window.location.search);
    const token = params.get('inviteToken') || params.get('joinToken');
    if (token) {
      setInviteToken(token);
      setIsOnboardingModalOpen(true);
    }
  }, []);

  // Carga inicial de criaturas, catálogo de maestrías, armas y partys
  useEffect(() => {
    const controller = new AbortController();

    const loadData = async () => {
      try {
        const [creatureList, masteryList, weaponList, partyList] = await Promise.all([
          apiClient.getCreatures(controller.signal),
          apiClient.getWeaponMasteries(controller.signal),
          apiClient.getWeapons(controller.signal),
          apiClient.getParties(),
        ]);
        setCreatures(creatureList);
        setAllMasteries(masteryList);
        setWeapons(weaponList);
        setParties(partyList);
      } catch {
        // En caso de error o aborto, el cliente ya maneja fallback local
      }
    };

    loadData();
    return () => controller.abort();
  }, []);

  const [lastQuickRoll, setLastQuickRoll] = useState<string | null>(null);

  /**
   * Incrementa el contador de simulaciones y ejecuta una tirada táctica d20 de demostración rápida.
   */
  const handleIncrementSimulation = useCallback(() => {
    setSimulationCount((prev) => prev + 1);
    const d20 = Math.floor(Math.random() * 20) + 1;
    const badge = d20 === 20 ? `🎯 ¡d20 = 20! ¡Impacto Crítico!` : d20 === 1 ? `💀 ¡d20 = 1! ¡Fallo Crítico!` : `🎲 Tirada d20: ${d20}`;
    setLastQuickRoll(badge);
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
        onOpenLiveSession={() => setIsLiveSessionOpen(true)}
        onOpenPartiesModal={() => setIsPartiesModalOpen(true)}
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
          title="Contador de tiradas rápidas de prueba (Fase 1). Para simulaciones masivas con Monte Carlo, accede a la pestaña Simulador."
        >
          🎲 {t('app.counterButton', { count: simulationCount })}
        </button>
        {lastQuickRoll && (
          <span className="quick-roll-badge fade-in" style={{ marginLeft: '12px', fontSize: '0.9rem', color: 'var(--color-gold)' }}>
            {lastQuickRoll}
          </span>
        )}
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

      {/* Modal de Modo Partida Colaborativo (Tracker de Iniciativa & Mis Notas Privadas) */}
      <LiveSessionModal
        isOpen={isLiveSessionOpen}
        parties={parties}
        activePartyId={liveSessionPartyId}
        currentUserId={user?.id}
        onClose={() => setIsLiveSessionOpen(false)}
      />

      {/* Modal de Gestión de Partys (CRUD, Enlaces de Invitación y Lanzador) */}
      <PartyManagerModal
        isOpen={isPartiesModalOpen}
        parties={parties}
        currentUserId={user?.id}
        currentUserDisplayName={user?.globalNickname || user?.displayName}
        onClose={() => setIsPartiesModalOpen(false)}
        onPartyCreated={(newParty) => {
          setParties((prev) => [newParty, ...prev.filter((p) => p.id !== newParty.id)]);
        }}
        onPartyDeleted={(deletedId) => {
          setParties((prev) => prev.filter((p) => p.id !== deletedId));
        }}
        onOpenLiveSession={(partyId) => {
          setLiveSessionPartyId(partyId);
          setIsLiveSessionOpen(true);
        }}
      />

      {/* Modal de Onboarding con Enlace de Invitación de Party */}
      <PartyOnboardingModal
        isOpen={isOnboardingModalOpen}
        inviteToken={inviteToken}
        onClose={() => {
          setIsOnboardingModalOpen(false);
          const url = new URL(window.location.href);
          url.searchParams.delete('inviteToken');
          url.searchParams.delete('joinToken');
          window.history.replaceState({}, '', url.toString());
        }}
        onJoinedSuccess={(joinedParty) => {
          setParties((prev) => [joinedParty, ...prev.filter((p) => p.id !== joinedParty.id)]);
          setIsLiveSessionOpen(true);
        }}
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

