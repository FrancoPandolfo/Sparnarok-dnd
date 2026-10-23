import React, { useState, useEffect, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { LiveCombatTracker } from './LiveCombatTracker';
import { SessionNotesEditor } from './SessionNotesEditor';
import { PartyStashModal } from './PartyStashModal';
import { liveCombatHubService } from '../services/liveCombatHubService';
import type { PartyResponseDto, EncounterResponseDto } from '../types/partyEncounter';
import type {
  PartyRole,
  LiveCombatDmProjectionDto,
  LiveCombatPlayerProjectionDto,
  InitializeLiveCombatantRequest,
} from '../types/liveCombat';

export interface LiveSessionModalProps {
  isOpen: boolean;
  onClose: () => void;
  parties: PartyResponseDto[];
  activePartyId?: string;
  activeEncounter?: EncounterResponseDto | null;
  currentUserId?: string | null;
  initialRole?: PartyRole;
  onPartyChange?: (partyId: string) => void;
}

/**
 * Modal orquestador del "Modo Partida" (Live Session).
 * Conecta al LiveCombatHub vía SignalR según el rol del usuario,
 * gestiona las pestañas de Tracker de Iniciativa y Notas de Sesión (DM-only).
 */
export const LiveSessionModal: React.FC<LiveSessionModalProps> = ({
  isOpen,
  onClose,
  parties,
  activePartyId,
  activeEncounter,
  currentUserId,
  initialRole,
  onPartyChange,
}) => {
  const { t } = useTranslation();

  const effectiveParties =
    parties.length > 0
      ? parties
      : [
          {
            id: 'party-alpha',
            name: 'La Compañía del Alba',
            ownerUserId: '00000000-0000-0000-0000-000000000001',
            createdAt: new Date().toISOString(),
            members: [],
          },
        ];

  const [selectedPartyId, setSelectedPartyId] = useState<string>(
    activePartyId || effectiveParties[0].id
  );
  const [activeTab, setActiveTab] = useState<'tracker' | 'notes' | 'stash'>('tracker');
  const [role, setRole] = useState<PartyRole>(initialRole || 'DungeonMaster');
  const [combatState, setCombatState] = useState<
    LiveCombatDmProjectionDto | LiveCombatPlayerProjectionDto | null
  >(null);
  const [copyFeedback, setCopyFeedback] = useState<boolean>(false);

  const isQuickMode = selectedPartyId === 'quick-session';
  const currentParty = isQuickMode
    ? {
        id: 'quick-session',
        name: t('liveCombat.quickSessionOption', 'Sin Party (Modo Rápido)'),
        ownerUserId: currentUserId || 'dm',
        createdAt: new Date().toISOString(),
        members: [],
      }
    : effectiveParties.find((p) => p.id === selectedPartyId);

  const prevActivePartyIdRef = useRef(activePartyId);
  const prevIsOpenRef = useRef(isOpen);

  useEffect(() => {
    if (isOpen && !prevIsOpenRef.current) {
      // Al abrir el modal por primera vez, inicializar con activePartyId o primera party disponible
      setSelectedPartyId(activePartyId || effectiveParties[0]?.id || 'quick-session');
    } else if (activePartyId && activePartyId !== prevActivePartyIdRef.current) {
      // Si el componente padre actualiza explícitamente activePartyId, sincronizarlo
      setSelectedPartyId(activePartyId);
    } else if (
      selectedPartyId !== 'quick-session' &&
      effectiveParties.length > 0 &&
      (!selectedPartyId || !effectiveParties.some((p) => p.id === selectedPartyId))
    ) {
      setSelectedPartyId(effectiveParties[0].id);
    }
    prevIsOpenRef.current = isOpen;
    prevActivePartyIdRef.current = activePartyId;
  }, [isOpen, activePartyId, effectiveParties, selectedPartyId]);

  const resolvedUserId = currentUserId || (() => {
    try {
      const authStr = typeof window !== 'undefined' ? window.localStorage.getItem('sparnarok_auth') : null;
      return authStr ? JSON.parse(authStr).id : null;
    } catch {
      return null;
    }
  })();

  // Determinar rol por defecto basado en la autoría de la Party seleccionada
  useEffect(() => {
    if (initialRole) {
      setRole(initialRole);
      return;
    }
    if (selectedPartyId === 'quick-session') {
      setRole('DungeonMaster');
      return;
    }
    const currentParty = effectiveParties.find((p) => p.id === selectedPartyId);
    if (currentParty && resolvedUserId && currentParty.ownerUserId === resolvedUserId) {
      setRole('DungeonMaster');
    } else if (currentParty && resolvedUserId && currentParty.ownerUserId !== resolvedUserId) {
      setRole('Player');
    }
  }, [selectedPartyId, effectiveParties, resolvedUserId, initialRole]);

  // Sincronización SignalR con teardown explícito antes de reconectar a nueva Party
  useEffect(() => {
    let isCancelled = false;

    // Resetear inmediatamente el estado del tracker al cambiar de Party o cerrar
    setCombatState(null);

    const initConnection = async () => {
      // Detener limpiamente conexión previa antes de conectar al nuevo PartyId
      await liveCombatHubService.stopConnection();

      if (isCancelled || !isOpen || !selectedPartyId) return;

      await liveCombatHubService.startConnection(selectedPartyId, role, {
        onCombatStateUpdated: (state) => {
          if (!isCancelled) {
            setCombatState(state);
          }
        },
      });
    };

    if (isOpen && selectedPartyId) {
      initConnection();
    } else {
      liveCombatHubService.stopConnection();
    }

    return () => {
      isCancelled = true;
      // Invocación explícita de hubConnection.stop() en la función de limpieza
      if (typeof liveCombatHubService.getHubConnection === 'function') {
        const hubConn = liveCombatHubService.getHubConnection();
        if (hubConn && typeof hubConn.stop === 'function') {
          hubConn.stop().catch(() => {});
        }
      }
      if (typeof liveCombatHubService.stopConnection === 'function') {
        liveCombatHubService.stopConnection().catch?.(() => {});
      }
    };
  }, [isOpen, selectedPartyId, role]);

  const handleInitializeCombat = async () => {
    if (selectedPartyId === 'quick-session') {
      await liveCombatHubService.initializeCombat('quick-session', {
        partyName: 'Partida Rápida',
        combatants: [],
      });
      return;
    }

    const party = effectiveParties.find((p) => p.id === selectedPartyId);
    if (!party) return;

    const combatants: InitializeLiveCombatantRequest[] = [];

    // Añadir Monstruos del Encuentro si existe (los jugadores no se agregan automáticamente para permitir el Lobby y división de party)
    if (activeEncounter && activeEncounter.creatures) {
      for (const monster of activeEncounter.creatures) {
        combatants.push({
          id: monster.id,
          name: monster.name,
          maxHp: monster.hitPoints.max,
          armorClass: monster.effectiveArmorClass || monster.armorClass.base,
          isPlayerCharacter: false,
          isVisibleToPlayers: false, // Inician ocultos por defecto para emboscada/anti-metagaming
        });
      }
    }

    await liveCombatHubService.initializeCombat(selectedPartyId, {
      partyName: party.name,
      combatants,
    });
  };

  if (!isOpen) return null;

  return (
    <div className="modal-backdrop" id="live-session-modal-backdrop">
      <div className="modal-content live-session-modal-dialog" role="dialog" aria-modal="true">
        {/* Cabecera del Modal */}
        <div className="modal-header">
          <div className="modal-header-left">
            <span className="modal-icon">🎲</span>
            <div className="modal-header-text">
              <h2 className="modal-title">{t('liveCombat.modalTitle', 'Modo Partida en Vivo')}</h2>
              <div className="role-and-sync-info">
                <span className={`role-badge role-${role.toLowerCase()}`} id="user-party-role-badge">
                  {role === 'DungeonMaster'
                    ? `👑 ${t('liveCombat.roleDm', 'Dungeon Master')}`
                    : `🛡️ ${t('liveCombat.rolePlayer', 'Jugador')}`}
                </span>
                <span className="live-indicator">⚡ SignalR Live</span>
              </div>
            </div>
          </div>

          <div className="modal-header-right">
            {/* Selector de Party con opción Modo Rápido */}
            <div className="party-select-wrapper">
              <label htmlFor="live-session-party-select">
                {t('liveCombat.selectParty', 'Party:')}
              </label>
              <select
                id="live-session-party-select"
                className="form-select form-select-sm"
                value={selectedPartyId}
                onChange={(e) => {
                  const val = e.target.value;
                  if (val !== selectedPartyId) {
                    setCombatState(null);
                    setSelectedPartyId(val);
                    onPartyChange?.(val);
                    if (val === 'quick-session') {
                      setRole('DungeonMaster');
                    }
                  }
                }}
              >
                <option value="quick-session">
                  {t('liveCombat.quickSessionOption', '⚡ Sin Party (Modo Rápido)')}
                </option>
                {effectiveParties.map((p) => (
                  <option key={p.id} value={p.id}>
                    {p.name}
                  </option>
                ))}
              </select>
            </div>

            {/* Selector de Rol manual para pruebas / asimetría */}
            <div className="role-switcher-wrapper">
              <button
                type="button"
                id="toggle-role-test-btn"
                className="btn btn-outline btn-xs"
                onClick={() => setRole((r) => (r === 'DungeonMaster' ? 'Player' : 'DungeonMaster'))}
                title="Cambiar vista para pruebas"
              >
                🔄 {role === 'DungeonMaster' ? 'Vista Jugador' : 'Vista DM'}
              </button>
            </div>

            {currentParty?.inviteToken && (
              <button
                type="button"
                id="copy-invite-link-btn"
                className="btn btn-outline btn-xs"
                onClick={() => {
                  const url = `${window.location.origin}${window.location.pathname}?inviteToken=${currentParty.inviteToken}`;
                  if (navigator.clipboard?.writeText) {
                    try {
                      const res = navigator.clipboard.writeText(url);
                      if (res && typeof res.catch === 'function') {
                        res.catch(() => {});
                      }
                    } catch {
                      // no-op
                    }
                  }
                  setCopyFeedback(true);
                  setTimeout(() => setCopyFeedback(false), 3000);
                }}
                title={`Copiar enlace con token: ${currentParty.inviteToken}`}
              >
                {copyFeedback ? '✓ Enlace Copiado' : '🔗 Invitar Jugadores'}
              </button>
            )}

            <button
              type="button"
              id="close-live-session-modal-btn"
              className="modal-close-btn"
              onClick={onClose}
              aria-label="Cerrar modal"
            >
              ✕
            </button>
          </div>
        </div>

        {/* Barra de Pestañas: Siempre visible para todos los roles (RLS a nivel de nota) */}
        <div className="modal-tabs-bar" role="tablist">
          <button
            type="button"
            id="tab-live-combat"
            role="tab"
            aria-selected={activeTab === 'tracker'}
            className={`modal-tab-btn ${activeTab === 'tracker' ? 'active' : ''}`}
            onClick={() => setActiveTab('tracker')}
          >
            {t('liveCombat.trackerTab', '⚔️ Tracker de Iniciativa')}
          </button>

          <button
            type="button"
            id="tab-session-notes"
            role="tab"
            aria-selected={activeTab === 'notes'}
            className={`modal-tab-btn ${activeTab === 'notes' ? 'active' : ''}`}
            onClick={() => setActiveTab('notes')}
          >
            {t('liveCombat.notesTab', '📜 Mis Notas Privadas')}
          </button>

          <button
            type="button"
            id="tab-session-stash"
            role="tab"
            aria-selected={activeTab === 'stash'}
            className={`modal-tab-btn ${activeTab === 'stash' ? 'active' : ''}`}
            onClick={() => setActiveTab('stash')}
          >
            {t('liveCombat.stashTab', '🪙 Botín de Grupo')}
          </button>
        </div>

        {/* Cuerpo del Modal */}
        <div className="modal-body live-session-modal-body">
          {!selectedPartyId ? (
            <div className="empty-party-alert">
              <p>{t('liveCombat.noPartySelected', 'Selecciona una Party para comenzar.')}</p>
            </div>
          ) : activeTab === 'tracker' ? (
            <div className="tracker-tab-content">
              {role === 'DungeonMaster' && (!combatState || combatState.status === 'NotStarted') && (
                <div className="init-combat-bar">
                  <button
                    type="button"
                    id="init-live-combat-btn"
                    className="btn btn-primary btn-sm primary-gold"
                    onClick={handleInitializeCombat}
                  >
                    ⚔️ {selectedPartyId === 'quick-session'
                      ? '⚡ Inicializar Combate Rápido'
                      : t('liveCombat.loadEncounterBtn', 'Inicializar Combate con la Party')}
                  </button>
                </div>
              )}
              <LiveCombatTracker
                partyId={selectedPartyId}
                party={currentParty}
                role={role}
                combatState={combatState}
                currentUserId={resolvedUserId}
              />
            </div>
          ) : activeTab === 'notes' ? (
            <div className="notes-tab-content">
              <SessionNotesEditor partyId={selectedPartyId} />
            </div>
          ) : (
            <div className="stash-tab-content" id="pane-session-stash">
              <PartyStashModal
                isOpen={true}
                isEmbedded={true}
                partyId={selectedPartyId}
                partyName={currentParty?.name || 'Party'}
                onClose={() => setActiveTab('tracker')}
              />
            </div>
          )}
        </div>
      </div>
    </div>
  );
};
