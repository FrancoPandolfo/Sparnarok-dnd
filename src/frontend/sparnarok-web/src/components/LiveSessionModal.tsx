import React, { useState, useEffect, useCallback } from 'react';
import { useTranslation } from 'react-i18next';
import { LiveCombatTracker } from './LiveCombatTracker';
import { SessionNotesEditor } from './SessionNotesEditor';
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
  const [activeTab, setActiveTab] = useState<'tracker' | 'notes'>('tracker');
  const [role, setRole] = useState<PartyRole>(initialRole || 'DungeonMaster');
  const [combatState, setCombatState] = useState<
    LiveCombatDmProjectionDto | LiveCombatPlayerProjectionDto | null
  >(null);

  useEffect(() => {
    if (activePartyId) {
      setSelectedPartyId(activePartyId);
    } else if (effectiveParties.length > 0 && (!selectedPartyId || !effectiveParties.some(p => p.id === selectedPartyId))) {
      setSelectedPartyId(effectiveParties[0].id);
    }
  }, [activePartyId, effectiveParties, selectedPartyId]);

  // Determinar rol por defecto basado en la autoría de la Party seleccionada
  useEffect(() => {
    if (initialRole) {
      setRole(initialRole);
      return;
    }
    const currentParty = effectiveParties.find((p) => p.id === selectedPartyId);
    if (currentParty && currentUserId && currentParty.ownerUserId === currentUserId) {
      setRole('DungeonMaster');
    } else if (currentParty && currentUserId && currentParty.ownerUserId !== currentUserId) {
      setRole('Player');
    }
  }, [selectedPartyId, effectiveParties, currentUserId, initialRole]);

  // Sincronización SignalR
  const connectSignalR = useCallback(async () => {
    if (!selectedPartyId || !isOpen) return;

    await liveCombatHubService.startConnection(selectedPartyId, role, {
      onCombatStateUpdated: (state) => {
        setCombatState(state);
      },
    });
  }, [selectedPartyId, role, isOpen]);

  useEffect(() => {
    if (isOpen && selectedPartyId) {
      connectSignalR();
    } else {
      liveCombatHubService.stopConnection();
      setCombatState(null);
    }

    return () => {
      liveCombatHubService.stopConnection();
    };
  }, [isOpen, selectedPartyId, role, connectSignalR]);

  // Si cambia a Player mientras estaba en notas de sesión, volver a tracker
  useEffect(() => {
    if (role === 'Player' && activeTab === 'notes') {
      setActiveTab('tracker');
    }
  }, [role, activeTab]);

  const handleInitializeCombat = async () => {
    const party = parties.find((p) => p.id === selectedPartyId);
    if (!party) return;

    const combatants: InitializeLiveCombatantRequest[] = [];

    // Añadir Héroes de la Party
    for (const member of party.members) {
      combatants.push({
        id: member.id,
        name: member.name,
        maxHp: member.hitPoints.max,
        armorClass: member.effectiveArmorClass || member.armorClass.base,
        isPlayerCharacter: true,
        isVisibleToPlayers: true,
      });
    }

    // Añadir Monstruos del Encuentro si existe
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
            {/* Selector de Party */}
            <div className="party-select-wrapper">
              <label htmlFor="live-session-party-select">
                {t('liveCombat.selectParty', 'Party:')}
              </label>
              <select
                id="live-session-party-select"
                className="form-select form-select-sm"
                value={selectedPartyId}
                onChange={(e) => setSelectedPartyId(e.target.value)}
              >
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

        {/* Barra de Pestañas */}
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

          {role === 'DungeonMaster' ? (
            <button
              type="button"
              id="tab-session-notes"
              role="tab"
              aria-selected={activeTab === 'notes'}
              className={`modal-tab-btn ${activeTab === 'notes' ? 'active' : ''}`}
              onClick={() => setActiveTab('notes')}
            >
              {t('liveCombat.notesTab', '📜 Notas de Sesión (DM)')}
            </button>
          ) : (
            <button
              type="button"
              id="tab-session-notes-locked-hint"
              className="modal-tab-btn locked-role-tab"
              onClick={() => {
                setRole('DungeonMaster');
                setActiveTab('notes');
              }}
              title="Las Notas de Sesión son exclusivas del Dungeon Master. Haz clic aquí o en 'Vista DM' para activarlas."
            >
              🔒 {t('liveCombat.notesTab', 'Notas de Sesión')} (Exclusivo DM - Clic para ver)
            </button>
          )}
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
                    ⚔️ {t('liveCombat.loadEncounterBtn', 'Inicializar Combate con la Party')}
                  </button>
                </div>
              )}
              <LiveCombatTracker
                partyId={selectedPartyId}
                role={role}
                combatState={combatState}
                currentUserId={currentUserId}
              />
            </div>
          ) : (
            <div className="notes-tab-content">
              <SessionNotesEditor partyId={selectedPartyId} />
            </div>
          )}
        </div>
      </div>
    </div>
  );
};
