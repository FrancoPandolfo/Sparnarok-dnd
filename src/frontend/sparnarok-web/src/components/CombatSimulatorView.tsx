import React, { useState, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import type {
  CreatureResponseDto,
  WeaponDto,
  MontecarloRequestDto,
  MontecarloResultDto,
  TargetingStrategy,
} from '../types/dnd';
import type { PartyResponseDto, EncounterResponseDto } from '../types/partyEncounter';
import { CustomHeroModal } from './CustomHeroModal';
import { PartyStashModal } from './PartyStashModal';
import { LiveSessionModal } from './LiveSessionModal';
import { apiClient } from '../services/apiClient';
import { useAuth } from '../context/AuthContext';

export interface CombatSimulatorViewProps {
  creatures: CreatureResponseDto[];
  weapons: WeaponDto[];
  onRunSimulation: (request: MontecarloRequestDto) => Promise<MontecarloResultDto>;
  onSimulationCompleted?: (result: MontecarloResultDto) => void;
  onHeroCreated?: (hero: CreatureResponseDto) => void;
  isAuthenticated?: boolean;
}

export interface TeamMemberConfig {
  id: string;
  creatureId: string;
  weaponId?: string;
}

/**
 * Vista de Simulación Táctica de Montecarlo (Party vs Encounter / N vs M).
 * Permite armar listas dinámicas para la Party (Team A) y el Encuentro de Monstruos (Team B),
 * configurar la estrategia de Targeting AI (LowestHP o Random), y ejecutar simulaciones estocásticas concurrentes.
 */
export const CombatSimulatorView: React.FC<CombatSimulatorViewProps> = ({
  creatures,
  weapons,
  onRunSimulation,
  onSimulationCompleted,
  onHeroCreated,
  isAuthenticated,
}) => {
  const { t } = useTranslation();
  const auth = useAuth();
  const isAuthed = isAuthenticated !== undefined ? isAuthenticated : auth.isAuthenticated;

  const [isHeroModalOpen, setIsHeroModalOpen] = useState(false);

  // Listas dinámicas de integrantes de cada equipo
  const [teamA, setTeamA] = useState<TeamMemberConfig[]>(() => [
    {
      id: 'member-a-0',
      creatureId: creatures[0]?.id || '',
      weaponId: weapons[0]?.id || '',
    },
  ]);

  const [teamB, setTeamB] = useState<TeamMemberConfig[]>(() => [
    {
      id: 'member-b-0',
      creatureId: (creatures.length > 1 ? creatures[1].id : creatures[0]?.id) || '',
      weaponId: (weapons.length > 1 ? weapons[1].id : weapons[0]?.id) || '',
    },
  ]);

  // Estado para persistencia y carga de grupos (Partys y Encuentros)
  const [parties, setParties] = useState<PartyResponseDto[]>([]);
  const [selectedPartyId, setSelectedPartyId] = useState<string>('');
  const [encounters, setEncounters] = useState<EncounterResponseDto[]>([]);
  const [selectedEncounterId, setSelectedEncounterId] = useState<string>('');

  // Modales de guardado
  const [isSavePartyModalOpen, setIsSavePartyModalOpen] = useState(false);
  const [partyNameInput, setPartyNameInput] = useState('');
  const [isSavingParty, setIsSavingParty] = useState(false);

  const [isSaveEncounterModalOpen, setIsSaveEncounterModalOpen] = useState(false);
  const [encounterNameInput, setEncounterNameInput] = useState('');
  const [encounterNotesInput, setEncounterNotesInput] = useState('');
  const [isSavingEncounter, setIsSavingEncounter] = useState(false);

  const [statusFeedback, setStatusFeedback] = useState<string | null>(null);
  const [isPartyStashModalOpen, setIsPartyStashModalOpen] = useState<boolean>(false);
  const [isLiveSessionModalOpen, setIsLiveSessionModalOpen] = useState<boolean>(false);

  const [targetingStrategy, setTargetingStrategy] = useState<TargetingStrategy>('LowestHp');
  const [iterations, setIterations] = useState<number>(10000);
  const [isSimulating, setIsSimulating] = useState<boolean>(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [result, setResult] = useState<MontecarloResultDto | null>(null);

  // Carga inicial y reactiva de Partys y Encuentros persistidos
  useEffect(() => {
    let isMounted = true;
    const fetchGroups = async () => {
      try {
        const [loadedParties, loadedEncounters] = await Promise.all([
          apiClient.getParties(),
          apiClient.getEncounters(),
        ]);
        if (isMounted) {
          setParties(loadedParties);
          setEncounters(loadedEncounters);
        }
      } catch {
        // Fallback local silencioso
      }
    };
    fetchGroups();
    return () => {
      isMounted = false;
    };
  }, [isAuthed]);

  // Catálogo unificado que incluye criaturas del bestiario y miembros cargados de partys/encuentros
  const allCreatures = React.useMemo(() => {
    const map = new Map<string, CreatureResponseDto>();
    creatures.forEach((c) => map.set(c.id, c));
    parties.forEach((p) => {
      p.members?.forEach((m) => {
        if (!map.has(m.id)) map.set(m.id, m);
      });
    });
    encounters.forEach((e) => {
      e.creatures?.forEach((c) => {
        if (!map.has(c.id)) map.set(c.id, c);
      });
    });
    return Array.from(map.values());
  }, [creatures, parties, encounters]);

  // Sincronizar selección inicial cuando las criaturas o armas cargan asíncronamente
  React.useEffect(() => {
    if (creatures.length > 0) {
      setTeamA((prev) => {
        if (prev.length === 0 || !prev[0].creatureId) {
          const firstCreature = creatures[0];
          return [
            {
              id: 'member-a-0',
              creatureId: firstCreature.id,
              weaponId: firstCreature.expectedDpr ? undefined : weapons[0]?.id || '',
            },
          ];
        }
        return prev;
      });

      setTeamB((prev) => {
        if (prev.length === 0 || !prev[0].creatureId) {
          const secondCreature = creatures.length > 1 ? creatures[1] : creatures[0];
          return [
            {
              id: 'member-b-0',
              creatureId: secondCreature.id,
              weaponId: secondCreature.expectedDpr
                ? undefined
                : (weapons.length > 1 ? weapons[1].id : weapons[0]?.id) || '',
            },
          ];
        }
        return prev;
      });
    }
  }, [creatures, weapons]);

  // Manejo de miembros del Equipo A
  const handleAddMemberA = () => {
    const defaultCreature = creatures[0];
    if (!defaultCreature) return;
    const newMember: TeamMemberConfig = {
      id: `member-a-${Date.now()}-${teamA.length}`,
      creatureId: defaultCreature.id,
      weaponId: defaultCreature.expectedDpr ? undefined : weapons[0]?.id || '',
    };
    setTeamA((prev) => [...prev, newMember]);
  };

  const handleRemoveMemberA = (index: number) => {
    setTeamA((prev) => prev.filter((_, i) => i !== index));
  };

  const handleUpdateMemberA = (index: number, creatureId: string, weaponId?: string) => {
    setTeamA((prev) => {
      const updated = [...prev];
      const creature = creatures.find((c) => c.id === creatureId);
      updated[index] = {
        ...updated[index],
        creatureId,
        weaponId: creature?.expectedDpr ? undefined : (weaponId ?? updated[index].weaponId ?? weapons[0]?.id ?? ''),
      };
      return updated;
    });
  };

  // Manejo de miembros del Equipo B
  const handleAddMemberB = () => {
    const defaultCreature = creatures.length > 1 ? creatures[1] : creatures[0];
    if (!defaultCreature) return;
    const defaultWeapon = weapons.length > 1 ? weapons[1] : weapons[0];
    const newMember: TeamMemberConfig = {
      id: `member-b-${Date.now()}-${teamB.length}`,
      creatureId: defaultCreature.id,
      weaponId: defaultCreature.expectedDpr ? undefined : defaultWeapon?.id || '',
    };
    setTeamB((prev) => [...prev, newMember]);
  };

  const handleRemoveMemberB = (index: number) => {
    setTeamB((prev) => prev.filter((_, i) => i !== index));
  };

  const handleUpdateMemberB = (index: number, creatureId: string, weaponId?: string) => {
    setTeamB((prev) => {
      const updated = [...prev];
      const creature = creatures.find((c) => c.id === creatureId);
      updated[index] = {
        ...updated[index],
        creatureId,
        weaponId: creature?.expectedDpr ? undefined : (weaponId ?? updated[index].weaponId ?? weapons[0]?.id ?? ''),
      };
      return updated;
    });
  };

  const handleRun = async () => {
    if (!isAuthed) {
      setErrorMessage(t('simulation.authRequiredDesc'));
      return;
    }
    if (teamA.length === 0) {
      setErrorMessage(t('simulation.emptyTeamA'));
      return;
    }
    if (teamB.length === 0) {
      setErrorMessage(t('simulation.emptyTeamB'));
      return;
    }

    const invalidMemberA = teamA.find((m) => {
      const c = allCreatures.find((x) => x.id === m.creatureId);
      return !c || (!c.expectedDpr && !m.weaponId);
    });
    const invalidMemberB = teamB.find((m) => {
      const c = allCreatures.find((x) => x.id === m.creatureId);
      return !c || (!c.expectedDpr && !m.weaponId);
    });

    if (invalidMemberA || invalidMemberB) {
      return;
    }

    setIsSimulating(true);
    setErrorMessage(null);

    const firstCreatureA = allCreatures.find((x) => x.id === teamA[0]?.creatureId);
    const firstCreatureB = allCreatures.find((x) => x.id === teamB[0]?.creatureId);

    const payload: MontecarloRequestDto = {
      teamA: teamA.map((m) => {
        const c = allCreatures.find((x) => x.id === m.creatureId);
        return {
          creatureId: m.creatureId,
          weaponId: c?.expectedDpr ? undefined : m.weaponId,
        };
      }),
      teamB: teamB.map((m) => {
        const c = allCreatures.find((x) => x.id === m.creatureId);
        return {
          creatureId: m.creatureId,
          weaponId: c?.expectedDpr ? undefined : m.weaponId,
        };
      }),
      combatantAId: teamA[0]?.creatureId,
      weaponAId: firstCreatureA?.expectedDpr ? undefined : teamA[0]?.weaponId,
      combatantBId: teamB[0]?.creatureId,
      weaponBId: firstCreatureB?.expectedDpr ? undefined : teamB[0]?.weaponId,
      targetingStrategy,
      iterations,
    };

    try {
      const response = await onRunSimulation(payload);
      setResult(response);
      onSimulationCompleted?.(response);
    } catch {
      setErrorMessage(t('simulation.errorSimulating'));
    } finally {
      setIsSimulating(false);
    }
  };

  const handleSelectParty = (partyId: string) => {
    setSelectedPartyId(partyId);
    if (!partyId) return;

    const party = parties.find((p) => p.id === partyId);
    if (!party || !party.members || party.members.length === 0) return;

    party.members.forEach((m) => {
      if (!creatures.some((c) => c.id === m.id)) {
        onHeroCreated?.(m);
      }
    });

    const newTeamA: TeamMemberConfig[] = party.members.map((m, idx) => ({
      id: `member-a-${Date.now()}-${idx}`,
      creatureId: m.id,
      weaponId: m.expectedDpr ? undefined : (weapons[0]?.id || ''),
    }));

    setTeamA(newTeamA);
  };

  const handleSelectEncounter = (encounterId: string) => {
    setSelectedEncounterId(encounterId);
    if (!encounterId) return;

    const encounter = encounters.find((e) => e.id === encounterId);
    if (!encounter || !encounter.creatures || encounter.creatures.length === 0) return;

    encounter.creatures.forEach((c) => {
      if (!creatures.some((x) => x.id === c.id)) {
        onHeroCreated?.(c);
      }
    });

    const newTeamB: TeamMemberConfig[] = encounter.creatures.map((c, idx) => ({
      id: `member-b-${Date.now()}-${idx}`,
      creatureId: c.id,
      weaponId: c.expectedDpr ? undefined : (weapons.length > 1 ? weapons[1].id : weapons[0]?.id || ''),
    }));

    setTeamB(newTeamB);
  };

  const handleClearTeamA = () => {
    setTeamA([]);
    setSelectedPartyId('');
  };

  const handleClearTeamB = () => {
    setTeamB([]);
    setSelectedEncounterId('');
  };

  const handleConfirmSaveParty = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!partyNameInput.trim()) return;

    const memberCreatureIds = teamA.map((m) => m.creatureId).filter(Boolean);
    if (memberCreatureIds.length === 0) {
      setErrorMessage(t('simulation.emptyTeamA'));
      return;
    }

    setIsSavingParty(true);
    setErrorMessage(null);
    try {
      const created = await apiClient.createParty({
        name: partyNameInput.trim(),
        memberCreatureIds,
      });

      setParties((prev) => [created, ...prev.filter((p) => p.id !== created.id)]);
      setSelectedPartyId(created.id);
      setIsSavePartyModalOpen(false);
      setPartyNameInput('');
      setStatusFeedback(t('simulation.saveSuccessParty'));
      setTimeout(() => setStatusFeedback(null), 4000);
    } catch {
      setErrorMessage('Error al guardar la Party.');
    } finally {
      setIsSavingParty(false);
    }
  };

  const handleConfirmSaveEncounter = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!encounterNameInput.trim()) return;

    const creatureIds = teamB.map((m) => m.creatureId).filter(Boolean);
    if (creatureIds.length === 0) {
      setErrorMessage(t('simulation.emptyTeamB'));
      return;
    }

    setIsSavingEncounter(true);
    setErrorMessage(null);
    try {
      const created = await apiClient.createEncounter({
        name: encounterNameInput.trim(),
        difficultyNotes: encounterNotesInput.trim() || undefined,
        creatureIds,
      });

      setEncounters((prev) => [created, ...prev.filter((e) => e.id !== created.id)]);
      setSelectedEncounterId(created.id);
      setIsSaveEncounterModalOpen(false);
      setEncounterNameInput('');
      setEncounterNotesInput('');
      setStatusFeedback(t('simulation.saveSuccessEncounter'));
      setTimeout(() => setStatusFeedback(null), 4000);
    } catch {
      setErrorMessage('Error al guardar el Encuentro.');
    } finally {
      setIsSavingEncounter(false);
    }
  };

  const handleHeroSaved = (hero: CreatureResponseDto) => {
    onHeroCreated?.(hero);
    setTeamA((prev) => {
      if (prev.length === 0) {
        return [{ id: `member-a-${Date.now()}`, creatureId: hero.id, weaponId: undefined }];
      }
      const firstCreature = creatures.find((c) => c.id === prev[0].creatureId);
      if (prev.length === 1 && (!firstCreature || !firstCreature.isPlayerCharacter)) {
        return [{ id: `member-a-${Date.now()}`, creatureId: hero.id, weaponId: undefined }];
      }
      return [...prev, { id: `member-a-${Date.now()}-${prev.length}`, creatureId: hero.id, weaponId: undefined }];
    });
  };

  return (
    <div className="combat-simulator-view fade-in">
      {/* Banner de Autenticación Requerida */}
      {!isAuthed && (
        <div id="sim-auth-banner" className="sim-auth-banner" role="status">
          <div className="auth-banner-icon">🔐</div>
          <div className="auth-banner-content">
            <h3 className="auth-banner-title">{t('simulation.authRequiredTitle')}</h3>
            <p className="auth-banner-desc">{t('simulation.authRequiredDesc')}</p>
          </div>
        </div>
      )}

      <header className="simulator-header">
        <div>
          <h2 className="section-title">{t('simulation.title')}</h2>
          <p className="section-subtitle">{t('simulation.subtitle')}</p>
        </div>
        <div className="simulator-header-actions">
          <button
            type="button"
            id="open-custom-hero-btn"
            className={`btn btn-primary hero-btn ${!isAuthed ? 'btn-disabled' : ''}`}
            onClick={() => {
              if (isAuthed) {
                setIsHeroModalOpen(true);
              }
            }}
            disabled={!isAuthed}
            title={!isAuthed ? t('simulation.authRequiredTooltip') : t('simulation.addCustomHero')}
          >
            🛡️ {t('simulation.addCustomHero')}
          </button>
        </div>
      </header>

      {/* Feedback de Estado de Guardado */}
      {statusFeedback && (
        <div id="status-feedback-banner" className="status-feedback-banner fade-in" role="status">
          ✨ {statusFeedback}
        </div>
      )}

      {/* Panel de Configuración de Equipos Dinámicos */}
      <div className="sim-setup-grid">
        {/* Equipo A (La Party / Héroes) */}
        <div className="combatant-config-card initiator-card">
          <div className="team-card-header">
            <div className="team-badge-group">
              <div className="card-badge initiator-badge">{t('simulation.partyTitle')}</div>
              <span className="team-count-pill">{teamA.length} {teamA.length === 1 ? 'combatiente' : 'combatientes'}</span>
            </div>
            <button
              type="button"
              id="add-member-a-btn"
              className="btn-add-member"
              onClick={handleAddMemberA}
              disabled={isSimulating || creatures.length === 0}
            >
              {t('simulation.addMember')}
            </button>
          </div>

          {/* Selector de Party Guardada y Botones de Persistencia */}
          <div className="team-persistence-bar">
            <div className="persistence-select-wrapper">
              <label htmlFor="saved-party-select" className="sr-only">
                {t('simulation.loadSavedParty')}
              </label>
              <select
                id="saved-party-select"
                className="sim-select party-encounter-dropdown"
                value={selectedPartyId}
                onChange={(e) => handleSelectParty(e.target.value)}
                disabled={isSimulating}
              >
                <option value="">{t('simulation.loadSavedParty')}</option>
                {parties.map((p) => (
                  <option key={p.id} value={p.id}>
                    🛡️ {p.name} ({p.members.length} {t('simulation.members')})
                  </option>
                ))}
              </select>
            </div>
            <div className="team-persistence-actions">
              <button
                type="button"
                id="save-party-btn"
                className="btn btn-secondary btn-sm"
                onClick={() => setIsSavePartyModalOpen(true)}
                disabled={isSimulating || teamA.length === 0}
                title={t('simulation.saveTeamAAsParty')}
              >
                💾 {t('simulation.saveTeamAAsParty')}
              </button>
              <button
                type="button"
                id="open-party-stash-btn"
                className="btn btn-primary btn-sm"
                onClick={() => setIsPartyStashModalOpen(true)}
                disabled={isSimulating}
                title={t('stash.openStashBtn', 'Abrir Alijo de la Party (Stash)')}
              >
                🎒 {t('stash.openStashBtn', 'Party Stash')}
              </button>
              <button
                type="button"
                id="open-live-session-btn"
                className="btn btn-primary btn-sm primary-gold"
                onClick={() => setIsLiveSessionModalOpen(true)}
                disabled={isSimulating}
                title={t('liveCombat.openLiveSessionBtn', 'Modo Partida (Iniciativa & Notas)')}
              >
                ⚔️ {t('liveCombat.openLiveSessionBtn', 'Modo Partida')}
              </button>
              <button
                type="button"
                id="clear-team-a-btn"
                className="btn btn-outline btn-sm"
                onClick={handleClearTeamA}
                disabled={isSimulating || teamA.length === 0}
                title={t('simulation.clearTeam')}
              >
                🗑️ {t('simulation.clearTeam')}
              </button>
            </div>
          </div>

          <div className="team-members-list">
            {teamA.length === 0 ? (
              <div className="empty-team-placeholder" id="empty-team-a-placeholder">
                <p>{t('simulation.noMembersInTeam')}</p>
              </div>
            ) : (
              teamA.map((member, idx) => {
                const creature = allCreatures.find((c) => c.id === member.creatureId);
                return (
                  <div key={member.id} className="team-member-item">
                    <div className="member-item-header">
                      <span className="member-item-title">
                        #{idx + 1} {creature ? creature.name : t('simulation.creatureSelect')}
                      </span>
                      {teamA.length > 1 && (
                        <button
                          type="button"
                          id={`remove-member-a-btn-${idx}`}
                          className="remove-member-btn"
                          onClick={() => handleRemoveMemberA(idx)}
                          disabled={isSimulating}
                          title={t('simulation.removeMember')}
                          aria-label={`${t('simulation.removeMember')} #${idx + 1}`}
                        >
                          ✕ {t('simulation.removeMember')}
                        </button>
                      )}
                    </div>

                    <div className="field-group">
                      <label htmlFor={idx === 0 ? 'combatant-a-select' : `combatant-a-select-${idx}`} className="field-label">
                        {t('simulation.creatureSelect')}
                      </label>
                      <select
                        id={idx === 0 ? 'combatant-a-select' : `combatant-a-select-${idx}`}
                        className="sim-select"
                        value={member.creatureId}
                        onChange={(e) => handleUpdateMemberA(idx, e.target.value)}
                        disabled={isSimulating}
                      >
                        {allCreatures.map((c) => (
                          <option key={`a-${idx}-${c.id}`} value={c.id}>
                            {c.isPlayerCharacter ? '🛡️ [Héroe] ' : ''}
                            {c.name}
                            {c.expectedDpr
                              ? ` (Nv ${c.level ?? 1} ${c.archetype ?? 'PC'} | ${c.expectedDpr} DPR | CA ${c.effectiveArmorClass} | ${c.hitPoints.max} HP)`
                              : ` (CR ${c.challengeRating} | CA ${c.effectiveArmorClass} | ${c.hitPoints.max} HP)`}
                          </option>
                        ))}
                      </select>
                    </div>

                    <div className="field-group">
                      <label htmlFor={idx === 0 ? 'weapon-a-select' : `weapon-a-select-${idx}`} className="field-label">
                        {t('simulation.weaponSelect')}
                      </label>
                      <select
                        id={idx === 0 ? 'weapon-a-select' : `weapon-a-select-${idx}`}
                        className="sim-select"
                        value={creature?.expectedDpr ? '' : (member.weaponId ?? '')}
                        onChange={(e) => handleUpdateMemberA(idx, member.creatureId, e.target.value)}
                        disabled={isSimulating || Boolean(creature?.expectedDpr)}
                      >
                        {creature?.expectedDpr ? (
                          <option value="">
                            {t('simulation.noWeaponRequiredDpr', { dpr: creature.expectedDpr })}
                          </option>
                        ) : (
                          weapons.map((w) => (
                            <option key={`w-a-${idx}-${w.id}`} value={w.id}>
                              {w.name} ({w.damage} {w.damageType})
                            </option>
                          ))
                        )}
                      </select>
                    </div>

                    {creature && (
                      <div className="creature-preview-strip">
                        <span>🛡️ CA: <strong>{creature.effectiveArmorClass}</strong></span>
                        <span>❤️ HP: <strong>{creature.hitPoints.max}</strong></span>
                        {creature.expectedDpr ? (
                          <span className="dpr-preview-badge">⚡ DPR: <strong>{creature.expectedDpr}</strong></span>
                        ) : (
                          <span>⚡ PB: <strong>+{creature.proficiencyBonus}</strong></span>
                        )}
                        {creature.archetype && (
                          <span className="archetype-preview-badge">🏹 {creature.archetype} (Nv {creature.level ?? 1})</span>
                        )}
                      </div>
                    )}
                  </div>
                );
              })
            )}
          </div>
        </div>

        {/* Separador Táctico VS */}
        <div className="versus-divider">
          <span className="versus-icon">⚔️</span>
          <span className="versus-text">VS</span>
        </div>

        {/* Equipo B (El Encuentro / Monstruos) */}
        <div className="combatant-config-card defender-card">
          <div className="team-card-header">
            <div className="team-badge-group">
              <div className="card-badge defender-badge">{t('simulation.encounterTitle')}</div>
              <span className="team-count-pill">{teamB.length} {teamB.length === 1 ? 'combatiente' : 'combatientes'}</span>
            </div>
            <button
              type="button"
              id="add-member-b-btn"
              className="btn-add-member"
              onClick={handleAddMemberB}
              disabled={isSimulating || creatures.length === 0}
            >
              {t('simulation.addMember')}
            </button>
          </div>

          {/* Selector de Encuentro Guardado y Botones de Persistencia */}
          <div className="team-persistence-bar">
            <div className="persistence-select-wrapper">
              <label htmlFor="saved-encounter-select" className="sr-only">
                {t('simulation.loadSavedEncounter')}
              </label>
              <select
                id="saved-encounter-select"
                className="sim-select party-encounter-dropdown"
                value={selectedEncounterId}
                onChange={(e) => handleSelectEncounter(e.target.value)}
                disabled={isSimulating}
              >
                <option value="">{t('simulation.loadSavedEncounter')}</option>
                {encounters.map((enc) => (
                  <option key={enc.id} value={enc.id}>
                    ⚔️ {enc.name} ({enc.creatures.length} {t('simulation.monsters')})
                  </option>
                ))}
              </select>
            </div>
            <div className="team-persistence-actions">
              <button
                type="button"
                id="save-encounter-btn"
                className="btn btn-secondary btn-sm"
                onClick={() => setIsSaveEncounterModalOpen(true)}
                disabled={isSimulating || teamB.length === 0}
                title={t('simulation.saveTeamBAsEncounter')}
              >
                💾 {t('simulation.saveTeamBAsEncounter')}
              </button>
              <button
                type="button"
                id="clear-team-b-btn"
                className="btn btn-outline btn-sm"
                onClick={handleClearTeamB}
                disabled={isSimulating || teamB.length === 0}
                title={t('simulation.clearTeam')}
              >
                🗑️ {t('simulation.clearTeam')}
              </button>
            </div>
          </div>

          <div className="team-members-list">
            {teamB.length === 0 ? (
              <div className="empty-team-placeholder" id="empty-team-b-placeholder">
                <p>{t('simulation.noMembersInTeam')}</p>
              </div>
            ) : (
              teamB.map((member, idx) => {
                const creature = allCreatures.find((c) => c.id === member.creatureId);
                return (
                  <div key={member.id} className="team-member-item">
                    <div className="member-item-header">
                      <span className="member-item-title">
                        #{idx + 1} {creature ? creature.name : t('simulation.creatureSelect')}
                      </span>
                      {teamB.length > 1 && (
                        <button
                          type="button"
                          id={`remove-member-b-btn-${idx}`}
                          className="remove-member-btn"
                          onClick={() => handleRemoveMemberB(idx)}
                          disabled={isSimulating}
                          title={t('simulation.removeMember')}
                          aria-label={`${t('simulation.removeMember')} #${idx + 1}`}
                        >
                          ✕ {t('simulation.removeMember')}
                        </button>
                      )}
                    </div>

                    <div className="field-group">
                      <label htmlFor={idx === 0 ? 'combatant-b-select' : `combatant-b-select-${idx}`} className="field-label">
                        {t('simulation.creatureSelect')}
                      </label>
                      <select
                        id={idx === 0 ? 'combatant-b-select' : `combatant-b-select-${idx}`}
                        className="sim-select"
                        value={member.creatureId}
                        onChange={(e) => handleUpdateMemberB(idx, e.target.value)}
                        disabled={isSimulating}
                      >
                        {allCreatures.map((c) => (
                          <option key={`b-${idx}-${c.id}`} value={c.id}>
                            {c.isPlayerCharacter ? '🛡️ [Héroe] ' : ''}
                            {c.name}
                            {c.expectedDpr
                              ? ` (Nv ${c.level ?? 1} ${c.archetype ?? 'PC'} | ${c.expectedDpr} DPR | CA ${c.effectiveArmorClass} | ${c.hitPoints.max} HP)`
                              : ` (CR ${c.challengeRating} | CA ${c.effectiveArmorClass} | ${c.hitPoints.max} HP)`}
                          </option>
                        ))}
                      </select>
                    </div>

                    <div className="field-group">
                      <label htmlFor={idx === 0 ? 'weapon-b-select' : `weapon-b-select-${idx}`} className="field-label">
                        {t('simulation.weaponSelect')}
                      </label>
                      <select
                        id={idx === 0 ? 'weapon-b-select' : `weapon-b-select-${idx}`}
                        className="sim-select"
                        value={creature?.expectedDpr ? '' : (member.weaponId ?? '')}
                        onChange={(e) => handleUpdateMemberB(idx, member.creatureId, e.target.value)}
                        disabled={isSimulating || Boolean(creature?.expectedDpr)}
                      >
                        {creature?.expectedDpr ? (
                          <option value="">
                            {t('simulation.noWeaponRequiredDpr', { dpr: creature.expectedDpr })}
                          </option>
                        ) : (
                          weapons.map((w) => (
                            <option key={`w-b-${idx}-${w.id}`} value={w.id}>
                              {w.name} ({w.damage} {w.damageType})
                            </option>
                          ))
                        )}
                      </select>
                    </div>

                    {creature && (
                      <div className="creature-preview-strip">
                        <span>🛡️ CA: <strong>{creature.effectiveArmorClass}</strong></span>
                        <span>❤️ HP: <strong>{creature.hitPoints.max}</strong></span>
                        {creature.expectedDpr ? (
                          <span className="dpr-preview-badge">⚡ DPR: <strong>{creature.expectedDpr}</strong></span>
                        ) : (
                          <span>⚡ PB: <strong>+{creature.proficiencyBonus}</strong></span>
                        )}
                        {creature.archetype && (
                          <span className="archetype-preview-badge">🏹 {creature.archetype} (Nv {creature.level ?? 1})</span>
                        )}
                      </div>
                    )}
                  </div>
                );
              })
            )}
          </div>
        </div>
      </div>

      {/* Barra de Parámetros y Ejecución */}
      <div className="sim-controls-bar">
        {/* Selector de Targeting AI */}
        <div className="targeting-control">
          <label htmlFor="targeting-strategy-select" className="field-label">
            {t('simulation.targetingStrategyLabel')}
          </label>
          <select
            id="targeting-strategy-select"
            className="sim-select"
            value={targetingStrategy}
            onChange={(e) => setTargetingStrategy(e.target.value as TargetingStrategy)}
            disabled={isSimulating}
          >
            <option value="LowestHp">{t('simulation.targetingLowestHp')}</option>
            <option value="Random">{t('simulation.targetingRandom')}</option>
          </select>
        </div>

        {/* Selector de Iteraciones */}
        <div className="iterations-control">
          <label htmlFor="iterations-select" className="field-label">
            {t('simulation.iterationsLabel')}
          </label>
          <select
            id="iterations-select"
            className="sim-select iterations-select"
            value={iterations}
            onChange={(e) => setIterations(Number(e.target.value))}
            disabled={isSimulating}
          >
            <option value={100}>100 duelos</option>
            <option value={1000}>1,000 duelos</option>
            <option value={5000}>5,000 duelos</option>
            <option value={10000}>10,000 duelos (Recomendado)</option>
            <option value={20000}>20,000 duelos (Stress test)</option>
          </select>
        </div>

        <button
          type="button"
          id="run-montecarlo-btn"
          className="run-simulation-btn"
          onClick={handleRun}
          disabled={!isAuthed || isSimulating || teamA.length === 0 || teamB.length === 0}
          title={!isAuthed ? t('simulation.authRequiredTooltip') : undefined}
        >
          {isSimulating ? (
            <>
              <span className="spinner-inline" /> {t('simulation.simulating')}
            </>
          ) : (
            <>🎲 {t('simulation.runButton')}</>
          )}
        </button>
      </div>

      {/* Alerta de Error */}
      {errorMessage && (
        <div id="sim-error-message" className="sim-error-alert" role="alert">
          ⚠️ {errorMessage}
        </div>
      )}

      {/* Panel de Resultados de la Simulación */}
      {result && (
        <section id="montecarlo-results-panel" className="results-panel fade-in">
          <div className="results-header">
            <div>
              <h3 className="results-title">{t('simulation.resultsTitle')}</h3>
              <span className="matchup-subtitle">
                {result.combatantAName} ({result.weaponAName}) vs {result.combatantBName} ({result.weaponBName})
              </span>
            </div>
            <div id="sim-execution-time" className="exec-time-pill">
              ⚡ {t('simulation.executionTime', { ms: result.executionTimeMs })}
            </div>
          </div>

          {/* Gráfico de Barras de Win Rate */}
          <div className="winrate-section">
            <h4 className="winrate-title">{t('simulation.winRateTitle')}</h4>

            <div
              id="winrate-bar-container"
              className="winrate-bar-container"
              role="progressbar"
              aria-label="Win rate distribution"
              aria-valuenow={result.combatantAWinRate}
              aria-valuemin={0}
              aria-valuemax={100}
            >
              <div
                id="win-rate-a"
                className="winrate-segment segment-a"
                style={{ width: `${result.combatantAWinRate}%` }}
                title={`${result.combatantAName}: ${result.combatantAWinRate}%`}
              >
                {result.combatantAWinRate > 10 && (
                  <span className="segment-label">{result.combatantAWinRate}%</span>
                )}
              </div>

              {result.drawRate > 0 && (
                <div
                  id="draw-rate"
                  className="winrate-segment segment-draw"
                  style={{ width: `${result.drawRate}%` }}
                  title={`Draws: ${result.drawRate}%`}
                >
                  {result.drawRate > 10 && (
                    <span className="segment-label">{result.drawRate}%</span>
                  )}
                </div>
              )}

              <div
                id="win-rate-b"
                className="winrate-segment segment-b"
                style={{ width: `${result.combatantBWinRate}%` }}
                title={`${result.combatantBName}: ${result.combatantBWinRate}%`}
              >
                {result.combatantBWinRate > 10 && (
                  <span className="segment-label">{result.combatantBWinRate}%</span>
                )}
              </div>
            </div>

            {/* Resumen numérico debajo de la barra */}
            <div className="winrate-legend">
              <div id="win-rate-summary-a" className="legend-item legend-a">
                <span className="legend-color-dot dot-a" />
                <span className="legend-name">{result.combatantAName}:</span>
                <strong className="legend-value">
                  {result.combatantAWinRate}% ({result.combatantAWins.toLocaleString()} victorias)
                </strong>
              </div>

              {result.drawRate > 0 && (
                <div id="win-rate-summary-draws" className="legend-item legend-draw">
                  <span className="legend-color-dot dot-draw" />
                  <span className="legend-name">Empates:</span>
                  <strong className="legend-value">
                    {result.drawRate}% ({result.draws.toLocaleString()})
                  </strong>
                </div>
              )}

              <div id="win-rate-summary-b" className="legend-item legend-b">
                <span className="legend-color-dot dot-b" />
                <span className="legend-name">{result.combatantBName}:</span>
                <strong className="legend-value">
                  {result.combatantBWinRate}% ({result.combatantBWins.toLocaleString()} victorias)
                </strong>
              </div>
            </div>
          </div>

          {/* Grid de Métricas Agregadas */}
          <div id="metrics-grid-sim" className="metrics-grid-sim">
            {/* Tasa de Supervivencia de Party */}
            <article className="sim-metric-card survival-rate-card">
              <div className="metric-header">
                <span className="metric-icon">🛡️</span>
                <span className="metric-title">{t('simulation.partySurvivalRate')}</span>
              </div>
              <div id="metric-party-survival-rate" className="metric-value">
                {result.teamASurvivalRate != null ? `${result.teamASurvivalRate}%` : `${result.combatantAWinRate}%`}
              </div>
              <p className="metric-subtitle">{t('simulation.partySurvivalRateDesc')}</p>
            </article>

            {/* Promedio de Rondas */}
            <article className="sim-metric-card">
              <div className="metric-header">
                <span className="metric-icon">⏱️</span>
                <span className="metric-title">{t('simulation.avgRounds')}</span>
              </div>
              <div id="metric-avg-rounds" className="metric-value">
                {result.averageRounds}
              </div>
              <p className="metric-subtitle">{t('simulation.avgRoundsDesc')}</p>
            </article>

            {/* DPR Promedio Equipo A */}
            <article className="sim-metric-card">
              <div className="metric-header">
                <span className="metric-icon">🗡️</span>
                <span className="metric-title">{t('simulation.dprCombatantA')}</span>
              </div>
              <div id="metric-dpr-a" className="metric-value">
                {result.combatantAAverageDpr}
              </div>
              <p className="metric-subtitle">
                {result.combatantAName} &bull; {t('simulation.dprDesc')}
              </p>
            </article>

            {/* DPR Promedio Equipo B */}
            <article className="sim-metric-card">
              <div className="metric-header">
                <span className="metric-icon">🏹</span>
                <span className="metric-title">{t('simulation.dprCombatantB')}</span>
              </div>
              <div id="metric-dpr-b" className="metric-value">
                {result.combatantBAverageDpr}
              </div>
              <p className="metric-subtitle">
                {result.combatantBName} &bull; {t('simulation.dprDesc')}
              </p>
            </article>

            {/* Total Duelos */}
            <article className="sim-metric-card">
              <div className="metric-header">
                <span className="metric-icon">🔄</span>
                <span className="metric-title">{t('simulation.totalIterations')}</span>
              </div>
              <div id="metric-total-iterations" className="metric-value">
                {result.totalIterations.toLocaleString()}
              </div>
              <p className="metric-subtitle">100% duelos concurrentes procesados</p>
            </article>
          </div>
        </section>
      )}

      {/* Modal para Dar de Alta un Héroe Custom */}
      <CustomHeroModal
        isOpen={isHeroModalOpen}
        onClose={() => setIsHeroModalOpen(false)}
        onSaveHero={apiClient.createCustomHero}
        onHeroSaved={handleHeroSaved}
      />

      {/* Modal para Guardar Team A como Party */}
      {isSavePartyModalOpen && (
        <div className="modal-backdrop" role="dialog" aria-modal="true" id="save-party-modal">
          <div className="modal-card">
            <div className="modal-header">
              <h3 className="modal-title">🛡️ {t('simulation.savePartyModalTitle')}</h3>
              <button
                type="button"
                id="close-save-party-modal"
                className="modal-close-btn"
                onClick={() => setIsSavePartyModalOpen(false)}
              >
                ✕
              </button>
            </div>
            <form onSubmit={handleConfirmSaveParty} className="modal-form">
              <div className="field-group">
                <label htmlFor="party-name-input" className="field-label">
                  {t('simulation.partyNameLabel')}
                </label>
                <input
                  id="party-name-input"
                  type="text"
                  className="input-text"
                  value={partyNameInput}
                  onChange={(e) => setPartyNameInput(e.target.value)}
                  placeholder={t('simulation.partyNamePlaceholder')}
                  required
                  autoFocus
                />
              </div>
              <p className="modal-info-text">
                Se guardarán los {teamA.length} combatientes de la party.
              </p>
              <div className="modal-actions">
                <button
                  type="button"
                  id="cancel-save-party-btn"
                  className="btn btn-secondary"
                  onClick={() => setIsSavePartyModalOpen(false)}
                  disabled={isSavingParty}
                >
                  {t('common.cancel', 'Cancelar')}
                </button>
                <button
                  type="submit"
                  id="confirm-save-party-btn"
                  className="btn btn-primary"
                  disabled={isSavingParty || !partyNameInput.trim()}
                >
                  {isSavingParty ? t('common.saving', 'Guardando...') : t('common.save', 'Guardar Party')}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Modal para Guardar Team B como Encuentro */}
      {isSaveEncounterModalOpen && (
        <div className="modal-backdrop" role="dialog" aria-modal="true" id="save-encounter-modal">
          <div className="modal-card">
            <div className="modal-header">
              <h3 className="modal-title">⚔️ {t('simulation.saveEncounterModalTitle')}</h3>
              <button
                type="button"
                id="close-save-encounter-modal"
                className="modal-close-btn"
                onClick={() => setIsSaveEncounterModalOpen(false)}
              >
                ✕
              </button>
            </div>
            <form onSubmit={handleConfirmSaveEncounter} className="modal-form">
              <div className="field-group">
                <label htmlFor="encounter-name-input" className="field-label">
                  {t('simulation.encounterNameLabel')}
                </label>
                <input
                  id="encounter-name-input"
                  type="text"
                  className="input-text"
                  value={encounterNameInput}
                  onChange={(e) => setEncounterNameInput(e.target.value)}
                  placeholder={t('simulation.encounterNamePlaceholder')}
                  required
                  autoFocus
                />
              </div>
              <div className="field-group">
                <label htmlFor="encounter-notes-input" className="field-label">
                  {t('simulation.encounterNotesLabel')}
                </label>
                <textarea
                  id="encounter-notes-input"
                  className="input-text input-textarea"
                  rows={3}
                  value={encounterNotesInput}
                  onChange={(e) => setEncounterNotesInput(e.target.value)}
                  placeholder={t('simulation.encounterNotesPlaceholder')}
                />
              </div>
              <p className="modal-info-text">
                Se guardarán los {teamB.length} monstruos del encuentro.
              </p>
              <div className="modal-actions">
                <button
                  type="button"
                  id="cancel-save-encounter-btn"
                  className="btn btn-secondary"
                  onClick={() => setIsSaveEncounterModalOpen(false)}
                  disabled={isSavingEncounter}
                >
                  {t('common.cancel', 'Cancelar')}
                </button>
                <button
                  type="submit"
                  id="confirm-save-encounter-btn"
                  className="btn btn-primary"
                  disabled={isSavingEncounter || !encounterNameInput.trim()}
                >
                  {isSavingEncounter ? t('common.saving', 'Guardando...') : t('common.save', 'Guardar Encuentro')}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Modal de Inventario Compartido de la Party (Collaborative Party Stash) */}
      <PartyStashModal
        isOpen={isPartyStashModalOpen}
        partyId={selectedPartyId || (parties.length > 0 ? parties[0].id : 'default-party')}
        partyName={
          parties.find((p) => p.id === selectedPartyId)?.name ||
          (parties.length > 0 ? parties[0].name : t('simulation.partyTitle', 'La Party'))
        }
        onClose={() => setIsPartyStashModalOpen(false)}
      />

      {/* Modal de Modo Partida Colaborativo (Live Initiative & Session Notes) */}
      <LiveSessionModal
        isOpen={isLiveSessionModalOpen}
        parties={parties}
        activePartyId={selectedPartyId || (parties.length > 0 ? parties[0].id : '')}
        activeEncounter={encounters.find((e) => e.id === selectedEncounterId) || null}
        currentUserId={auth.user?.id}
        onClose={() => setIsLiveSessionModalOpen(false)}
      />
    </div>
  );
};
