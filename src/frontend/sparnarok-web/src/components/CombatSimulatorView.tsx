import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type {
  CreatureResponseDto,
  WeaponDto,
  MontecarloRequestDto,
  MontecarloResultDto,
  TargetingStrategy,
} from '../types/dnd';
import { CustomHeroModal } from './CustomHeroModal';
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

  const [targetingStrategy, setTargetingStrategy] = useState<TargetingStrategy>('LowestHp');
  const [iterations, setIterations] = useState<number>(10000);
  const [isSimulating, setIsSimulating] = useState<boolean>(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [result, setResult] = useState<MontecarloResultDto | null>(null);

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
      const c = creatures.find((x) => x.id === m.creatureId);
      return !c || (!c.expectedDpr && !m.weaponId);
    });
    const invalidMemberB = teamB.find((m) => {
      const c = creatures.find((x) => x.id === m.creatureId);
      return !c || (!c.expectedDpr && !m.weaponId);
    });

    if (invalidMemberA || invalidMemberB) {
      return;
    }

    setIsSimulating(true);
    setErrorMessage(null);

    const firstCreatureA = creatures.find((x) => x.id === teamA[0]?.creatureId);
    const firstCreatureB = creatures.find((x) => x.id === teamB[0]?.creatureId);

    const payload: MontecarloRequestDto = {
      teamA: teamA.map((m) => {
        const c = creatures.find((x) => x.id === m.creatureId);
        return {
          creatureId: m.creatureId,
          weaponId: c?.expectedDpr ? undefined : m.weaponId,
        };
      }),
      teamB: teamB.map((m) => {
        const c = creatures.find((x) => x.id === m.creatureId);
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

  const handleHeroSaved = (hero: CreatureResponseDto) => {
    onHeroCreated?.(hero);
    setTeamA((prev) => {
      if (prev.length === 0) {
        return [{ id: `member-a-${Date.now()}`, creatureId: hero.id, weaponId: undefined }];
      }
      const updated = [...prev];
      updated[0] = { ...updated[0], creatureId: hero.id, weaponId: undefined };
      return updated;
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

          <div className="team-members-list">
            {teamA.map((member, idx) => {
              const creature = creatures.find((c) => c.id === member.creatureId);
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
                      {creatures.map((c) => (
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
            })}
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

          <div className="team-members-list">
            {teamB.map((member, idx) => {
              const creature = creatures.find((c) => c.id === member.creatureId);
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
                      {creatures.map((c) => (
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
            })}
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
    </div>
  );
};
