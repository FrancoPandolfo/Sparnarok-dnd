import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { liveCombatHubService } from '../services/liveCombatHubService';
import type {
  LiveCombatDmProjectionDto,
  LiveCombatPlayerProjectionDto,
  LiveCombatantDmDto,
  LiveCombatantPlayerDto,
  PartyRole,
} from '../types/liveCombat';

export interface LiveCombatTrackerProps {
  partyId: string;
  role: PartyRole;
  combatState: LiveCombatDmProjectionDto | LiveCombatPlayerProjectionDto | null;
  currentUserId?: string | null;
}

/**
 * Rastreador de combate e iniciativa táctica en vivo con sincronización SignalR.
 * Ofrece controles maestros para el Dungeon Master y una vista censurada anti-metagaming para Jugadores.
 */
export const LiveCombatTracker: React.FC<LiveCombatTrackerProps> = ({
  partyId,
  role,
  combatState,
}) => {
  const { t } = useTranslation();

  // Estado para la tirada de iniciativa individual del jugador
  const [playerSelectedCombatantId, setPlayerSelectedCombatantId] = useState<string>('');
  const [playerInitiativeValue, setPlayerInitiativeValue] = useState<number>(10);

  // Estado para override de iniciativa del DM (para jugadores AFK o monstruos)
  const [overrideCombatantId, setOverrideCombatantId] = useState<string | null>(null);
  const [overrideValue, setOverrideValue] = useState<number>(10);

  const isDm = role === 'DungeonMaster';

  const handlePlayerSubmitInitiative = async () => {
    const targetHeroId =
      playerSelectedCombatantId || (playerHeroes.length === 1 ? playerHeroes[0].id : '');
    if (!combatState || !targetHeroId) return;
    await liveCombatHubService.submitInitiative(partyId, targetHeroId, playerInitiativeValue);
  };

  const handleDmOverrideInitiative = async (combatantId: string) => {
    await liveCombatHubService.overrideInitiative(partyId, combatantId, overrideValue);
    setOverrideCombatantId(null);
  };

  const handleAutoRollMonsters = async () => {
    await liveCombatHubService.autoRollMonsters(partyId);
  };

  const handleStartCombat = async () => {
    await liveCombatHubService.startCombat(partyId);
  };

  const handleNextTurn = async () => {
    await liveCombatHubService.nextTurn(partyId);
  };

  const handlePreviousTurn = async () => {
    await liveCombatHubService.previousTurn(partyId);
  };

  const handleEndCombat = async () => {
    await liveCombatHubService.endCombat(partyId);
  };

  const handleModifyHp = async (combatantId: string, delta: number) => {
    await liveCombatHubService.modifyHp(partyId, combatantId, delta);
  };

  const handleToggleVisibility = async (combatantId: string, currentVisibility: boolean) => {
    await liveCombatHubService.toggleVisibility(partyId, combatantId, !currentVisibility);
  };

  if (!combatState) {
    return (
      <div className="live-combat-empty-state" id="live-combat-empty-state">
        <div className="empty-state-icon">⚔️</div>
        <h3>{t('liveCombat.waitingInitiative', 'Esperando Combate')}</h3>
        <p>{t('liveCombat.emptyCombatants', 'No hay combate activo para esta Party.')}</p>
      </div>
    );
  }

  const { status, currentRound, activeCombatantId, activeCombatantName, combatants } = combatState;

  // Encontrar héroes jugables para el selector de iniciativa del jugador
  const playerHeroes = combatants.filter((c) => c.isPlayerCharacter);

  return (
    <div className="live-combat-tracker-panel" id="live-combat-tracker">
      {/* Barra superior de Estado del Combate */}
      <div className="combat-status-header">
        <div className="status-badge-container">
          <span className={`status-pill status-${status.toLowerCase()}`} id="combat-status-badge">
            {status === 'WaitingForInitiative' && `⏳ ${t('liveCombat.waitingInitiative', 'Esperando Iniciativas')}`}
            {status === 'InProgress' && `⚔️ ${t('liveCombat.inProgress', { round: currentRound })}`}
            {status === 'Ended' && `🛑 ${t('liveCombat.combatEnded', 'Combate Finalizado')}`}
            {status === 'NotStarted' && `💤 No Iniciado`}
          </span>
          <span className="party-name-badge">🏰 {combatState.partyName}</span>
        </div>

        {/* Turno Activo */}
        {status === 'InProgress' && (
          <div className="active-turn-banner" id="active-turn-banner">
            <span className="turn-label">
              {t('liveCombat.currentTurn', {
                name: activeCombatantName || t('liveCombat.enemyTurn', 'Turno del Enemigo'),
              })}
            </span>
          </div>
        )}
      </div>

      {/* Controles Maestros del Dungeon Master */}
      {isDm && (
        <div className="dm-master-controls-bar" id="dm-master-controls">
          <div className="dm-controls-title">👑 {t('liveCombat.roleDm', 'Dungeon Master')}</div>
          <div className="dm-buttons-group">
            {status === 'WaitingForInitiative' && (
              <>
                <button
                  type="button"
                  id="auto-roll-monsters-btn"
                  className="btn btn-secondary btn-sm"
                  onClick={handleAutoRollMonsters}
                >
                  🎲 {t('liveCombat.autoRollMonstersBtn', 'Auto-Roll Monstruos')}
                </button>
                <button
                  type="button"
                  id="start-combat-btn"
                  className="btn btn-primary btn-sm primary-gold"
                  onClick={handleStartCombat}
                >
                  ⚔️ {t('liveCombat.startCombatBtn', 'Iniciar Combate')}
                </button>
              </>
            )}

            {status === 'InProgress' && (
              <>
                <button
                  type="button"
                  id="prev-turn-btn"
                  className="btn btn-secondary btn-sm"
                  onClick={handlePreviousTurn}
                >
                  {t('liveCombat.prevTurnBtn', '⬅️ Turno Anterior')}
                </button>
                <button
                  type="button"
                  id="next-turn-btn"
                  className="btn btn-primary btn-sm primary-gold"
                  onClick={handleNextTurn}
                >
                  {t('liveCombat.nextTurnBtn', '➡️ Siguiente Turno')}
                </button>
                <button
                  type="button"
                  id="end-combat-btn"
                  className="btn btn-danger btn-sm"
                  onClick={handleEndCombat}
                >
                  {t('liveCombat.endCombatBtn', '🛑 Finalizar')}
                </button>
              </>
            )}
          </div>
        </div>
      )}

      {/* Panel de Carga de Iniciativa del Jugador */}
      {!isDm && status === 'WaitingForInitiative' && (
        <div className="player-initiative-bar" id="player-initiative-bar">
          <div className="player-bar-label">
            🎲 {t('liveCombat.myInitiativeLabel', 'Tu Tirada de Iniciativa:')}
          </div>
          <div className="player-bar-inputs">
            {playerHeroes.length > 1 && (
              <select
                id="player-hero-select"
                className="form-select form-select-sm"
                value={playerSelectedCombatantId}
                onChange={(e) => setPlayerSelectedCombatantId(e.target.value)}
              >
                <option value="">-- Elige tu Personaje --</option>
                {playerHeroes.map((hero) => (
                  <option key={hero.id} value={hero.id}>
                    {hero.name}
                  </option>
                ))}
              </select>
            )}

            <input
              type="number"
              id="player-initiative-input"
              className="initiative-num-input"
              min={1}
              max={40}
              value={playerInitiativeValue}
              onChange={(e) => setPlayerInitiativeValue(Number(e.target.value))}
            />

            <button
              type="button"
              id="submit-initiative-btn"
              className="btn btn-primary btn-sm primary-gold"
              onClick={handlePlayerSubmitInitiative}
              disabled={playerHeroes.length > 1 && !playerSelectedCombatantId}
            >
              🚀 {t('liveCombat.submitInitiativeBtn', 'Enviar Iniciativa')}
            </button>
          </div>
        </div>
      )}

      {/* Tabla de Combatientes en el Orden de Iniciativa */}
      <div className="combatants-table-container">
        <table className="combatants-table" id="initiative-order-table">
          <thead>
            <tr>
              <th>#</th>
              <th>{t('liveCombat.colName', 'Combatiente')}</th>
              <th>{t('liveCombat.colInitiative', 'Iniciativa')}</th>
              <th>{t('liveCombat.colHp', 'PG / Salud')}</th>
              {isDm && <th>{t('liveCombat.colAc', 'CA')}</th>}
              {isDm && <th>{t('liveCombat.colVisibility', 'Visibilidad')}</th>}
              {isDm && <th>{t('liveCombat.colActions', 'Acciones')}</th>}
            </tr>
          </thead>
          <tbody>
            {combatants.map((c, index) => {
              const isActive = c.id === activeCombatantId;
              const dmCombatant = isDm ? (c as LiveCombatantDmDto) : null;
              const playerCombatant = !isDm ? (c as LiveCombatantPlayerDto) : null;

              return (
                <tr
                  key={c.id}
                  className={`combatant-row ${isActive ? 'active-turn-row' : ''} ${
                    c.isPlayerCharacter ? 'hero-row' : 'monster-row'
                  }`}
                  id={`combatant-row-${c.id}`}
                >
                  <td className="row-index">{index + 1}</td>
                  <td className="row-name">
                    <span className="combatant-name">{c.name}</span>
                    <span
                      className={`combatant-badge ${
                        c.isPlayerCharacter ? 'badge-hero' : 'badge-monster'
                      }`}
                    >
                      {c.isPlayerCharacter
                        ? t('liveCombat.heroBadge', 'Héroe')
                        : t('liveCombat.monsterBadge', 'Monstruo')}
                    </span>
                    {isActive && <span className="active-turn-indicator">⭐</span>}
                  </td>

                  {/* Iniciativa */}
                  <td className="row-initiative">
                    {isDm && overrideCombatantId === c.id ? (
                      <div className="initiative-override-box">
                        <input
                          type="number"
                          className="input-xs"
                          value={overrideValue}
                          onChange={(e) => setOverrideValue(Number(e.target.value))}
                        />
                        <button
                          type="button"
                          className="btn btn-primary btn-xs"
                          onClick={() => handleDmOverrideInitiative(c.id)}
                        >
                          ✓
                        </button>
                      </div>
                    ) : (
                      <span className="initiative-score">
                        {c.initiative !== null ? c.initiative : '-'}
                        {isDm && (
                          <button
                            type="button"
                            className="btn-link-icon"
                            title={t('liveCombat.overrideInitiativeTitle', 'Modificar Iniciativa')}
                            onClick={() => {
                              setOverrideCombatantId(c.id);
                              setOverrideValue(c.initiative ?? 10);
                            }}
                          >
                            ✏️
                          </button>
                        )}
                      </span>
                    )}
                  </td>

                  {/* HP / Salud */}
                  <td className="row-hp">
                    {isDm && dmCombatant ? (
                      <div className="hp-real-control">
                        <span className="hp-value">
                          {dmCombatant.currentHp} / {dmCombatant.maxHp}
                        </span>
                        <div className="hp-quick-buttons">
                          <button
                            type="button"
                            className="hp-btn minus"
                            onClick={() => handleModifyHp(c.id, -5)}
                            title="-5 HP"
                          >
                            -5
                          </button>
                          <button
                            type="button"
                            className="hp-btn minus"
                            onClick={() => handleModifyHp(c.id, -1)}
                            title="-1 HP"
                          >
                            -1
                          </button>
                          <button
                            type="button"
                            className="hp-btn plus"
                            onClick={() => handleModifyHp(c.id, 1)}
                            title="+1 HP"
                          >
                            +1
                          </button>
                          <button
                            type="button"
                            className="hp-btn plus"
                            onClick={() => handleModifyHp(c.id, 5)}
                            title="+5 HP"
                          >
                            +5
                          </button>
                        </div>
                      </div>
                    ) : (
                      <span className={`health-status-badge health-${playerCombatant?.healthStatus.toLowerCase()}`}>
                        {playerCombatant?.isPlayerCharacter && playerCombatant?.currentHp !== undefined && playerCombatant.currentHp !== null
                          ? `${playerCombatant.currentHp} / ${playerCombatant.maxHp}`
                          : playerCombatant?.healthStatus}
                      </span>
                    )}
                  </td>

                  {/* CA (DM Only) */}
                  {isDm && dmCombatant && (
                    <td className="row-ac">
                      <span className="ac-shield-pill">🛡️ {dmCombatant.armorClass}</span>
                    </td>
                  )}

                  {/* Visibilidad (DM Only) */}
                  {isDm && dmCombatant && (
                    <td className="row-visibility">
                      <button
                        type="button"
                        id={`toggle-visibility-${c.id}`}
                        className={`visibility-toggle-btn ${
                          dmCombatant.isVisibleToPlayers ? 'is-visible' : 'is-hidden'
                        }`}
                        onClick={() =>
                          handleToggleVisibility(c.id, dmCombatant.isVisibleToPlayers)
                        }
                        title={t('liveCombat.toggleVisibility', 'Alternar Visibilidad')}
                      >
                        {dmCombatant.isVisibleToPlayers ? '👁️ Visible' : '🙈 Oculto'}
                      </button>
                    </td>
                  )}

                  {/* Acciones del DM */}
                  {isDm && (
                    <td className="row-actions">
                      {/* Espacio para condiciones o acciones rápidas */}
                    </td>
                  )}
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
};
