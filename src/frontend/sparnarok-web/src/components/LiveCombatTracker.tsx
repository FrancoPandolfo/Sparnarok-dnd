import React, { useState, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import {
  DndContext,
  closestCenter,
  KeyboardSensor,
  PointerSensor,
  useSensor,
  useSensors,
  type DragEndEvent,
} from '@dnd-kit/core';
import {
  arrayMove,
  SortableContext,
  sortableKeyboardCoordinates,
  useSortable,
  verticalListSortingStrategy,
} from '@dnd-kit/sortable';
import { CSS } from '@dnd-kit/utilities';
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

interface SortableCombatantRowProps {
  combatant: LiveCombatantDmDto | LiveCombatantPlayerDto;
  index: number;
  isActive: boolean;
  isDm: boolean;
  overrideCombatantId: string | null;
  overrideValue: number | string;
  setOverrideCombatantId: (id: string | null) => void;
  setOverrideValue: (val: number | string) => void;
  handleDmOverrideInitiative: (combatantId: string) => void;
  handleModifyHp: (combatantId: string, delta: number) => void;
  handleToggleVisibility: (combatantId: string, currentVisibility: boolean) => void;
}

/**
 * Fila individual sortable para combatiente.
 * En la vista de Dungeon Master, incluye un Drag Handle táctil y accesible (≡).
 */
const SortableCombatantRow: React.FC<SortableCombatantRowProps> = ({
  combatant: c,
  index,
  isActive,
  isDm,
  overrideCombatantId,
  overrideValue,
  setOverrideCombatantId,
  setOverrideValue,
  handleDmOverrideInitiative,
  handleModifyHp,
  handleToggleVisibility,
}) => {
  const { t } = useTranslation();
  const {
    attributes,
    listeners,
    setNodeRef,
    transform,
    transition,
    isDragging,
  } = useSortable({
    id: c.id,
    disabled: !isDm,
  });

  const style: React.CSSProperties = {
    transform: CSS.Transform.toString(transform),
    transition,
    opacity: isDragging ? 0.6 : 1,
    position: isDragging ? 'relative' : undefined,
    zIndex: isDragging ? 999 : undefined,
  };

  const dmCombatant = isDm ? (c as LiveCombatantDmDto) : null;
  const playerCombatant = !isDm ? (c as LiveCombatantPlayerDto) : null;

  return (
    <tr
      ref={setNodeRef}
      style={style}
      className={`combatant-row ${isActive ? 'active-turn-row' : ''} ${
        c.isPlayerCharacter ? 'hero-row' : 'monster-row'
      } ${isDragging ? 'is-dragging' : ''}`}
      id={`combatant-row-${c.id}`}
      data-testid={`combatant-row-${c.id}`}
    >
      <td className="row-index">
        <div className="row-index-container">
          {isDm && (
            <button
              type="button"
              id={`drag-handle-${c.id}`}
              data-testid={`drag-handle-${c.id}`}
              className="drag-handle-btn"
              aria-label={t('liveCombat.dragHandleAria', {
                defaultValue: 'Reordenar {{name}}',
                name: c.name,
              })}
              title={t('liveCombat.dragHandleTitle', 'Arrastrar para reordenar turno')}
              {...attributes}
              {...listeners}
            >
              ≡
            </button>
          )}
          <span className="index-number">{index + 1}</span>
        </div>
      </td>

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

      {/* Iniciativa: Admite números o null/vacío sin romper la UI */}
      <td className="row-initiative">
        {isDm && overrideCombatantId === c.id ? (
          <div className="initiative-override-box">
            <input
              type="number"
              className="input-xs"
              value={overrideValue}
              onChange={(e) =>
                setOverrideValue(e.target.value === '' ? '' : Number(e.target.value))
              }
              placeholder="-"
              aria-label={t('liveCombat.overrideInitiativeTitle', 'Modificar Iniciativa')}
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
            {c.initiative !== null && c.initiative !== undefined ? c.initiative : '-'}
            {isDm && (
              <button
                type="button"
                className="btn-link-icon"
                title={t('liveCombat.overrideInitiativeTitle', 'Modificar Iniciativa')}
                onClick={() => {
                  setOverrideCombatantId(c.id);
                  setOverrideValue(c.initiative !== null && c.initiative !== undefined ? c.initiative : '');
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
          <span
            className={`health-status-badge health-${playerCombatant?.healthStatus.toLowerCase()}`}
          >
            {playerCombatant?.isPlayerCharacter &&
            playerCombatant?.currentHp !== undefined &&
            playerCombatant.currentHp !== null
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
          {/* Espacio para acciones rápidas o condiciones */}
        </td>
      )}
    </tr>
  );
};

/**
 * Rastreador de combate e iniciativa táctica en vivo con sincronización SignalR.
 * Ofrece controles maestros para el Dungeon Master con Drag & Drop interactivo (@dnd-kit)
 * y una vista censurada anti-metagaming para Jugadores.
 */
export const LiveCombatTracker: React.FC<LiveCombatTrackerProps> = ({
  partyId,
  role,
  combatState,
}) => {
  const { t } = useTranslation();

  // Estado local para actualización optimista del ordenamiento
  const [localCombatants, setLocalCombatants] = useState<
    (LiveCombatantDmDto | LiveCombatantPlayerDto)[]
  >(combatState?.combatants ?? []);

  // Sincronizar estado local cuando SignalR actualiza el estado del servidor
  useEffect(() => {
    if (combatState?.combatants) {
      setLocalCombatants(combatState.combatants);
    }
  }, [combatState?.combatants]);

  // Sensores de DnD Kit
  const sensors = useSensors(
    useSensor(PointerSensor, {
      activationConstraint: {
        distance: 3,
      },
    }),
    useSensor(KeyboardSensor, {
      coordinateGetter: sortableKeyboardCoordinates,
    })
  );

  // Estado para la tirada de iniciativa individual del jugador
  const [playerSelectedCombatantId, setPlayerSelectedCombatantId] = useState<string>('');
  const [playerInitiativeValue, setPlayerInitiativeValue] = useState<number | string>(10);

  // Estado para override de iniciativa del DM (para jugadores AFK o monstruos)
  const [overrideCombatantId, setOverrideCombatantId] = useState<string | null>(null);
  const [overrideValue, setOverrideValue] = useState<number | string>(10);

  // Estado para modal ligero de Combatiente Rápido
  const [isQuickModalOpen, setIsQuickModalOpen] = useState(false);
  const [quickName, setQuickName] = useState('');
  const [quickHp, setQuickHp] = useState<number | string>(20);
  const [quickAc, setQuickAc] = useState<number | string>(12);
  const [quickIsHero, setQuickIsHero] = useState(false);
  const [quickInitiative, setQuickInitiative] = useState<number | string>('');
  const [quickError, setQuickError] = useState<string | null>(null);

  const isDm = role === 'DungeonMaster';

  const handleDragEnd = async (event: DragEndEvent) => {
    const { active, over } = event;
    if (!over || active.id === over.id || !isDm) return;

    const oldIndex = localCombatants.findIndex((c) => c.id === active.id);
    const newIndex = localCombatants.findIndex((c) => c.id === over.id);

    if (oldIndex !== -1 && newIndex !== -1) {
      // 1. Actualización optimista inmediata en la UI de React
      const updated = arrayMove(localCombatants, oldIndex, newIndex);
      setLocalCombatants(updated);

      // 2. Notificación en tiempo real vía SignalR
      try {
        await liveCombatHubService.reorderCombatant(partyId, String(active.id), newIndex);
      } catch (err) {
        console.error('[LiveCombatTracker] Error al sincronizar reordenamiento:', err);
      }
    }
  };

  const handlePlayerSubmitInitiative = async () => {
    const targetHeroId =
      playerSelectedCombatantId || (playerHeroes.length === 1 ? playerHeroes[0].id : '');
    if (!combatState || !targetHeroId) return;

    const numericInit =
      playerInitiativeValue === '' ? null : Number(playerInitiativeValue);
    await liveCombatHubService.submitInitiative(partyId, targetHeroId, numericInit);
  };

  const handleDmOverrideInitiative = async (combatantId: string) => {
    const numericInit = overrideValue === '' ? null : Number(overrideValue);
    await liveCombatHubService.overrideInitiative(partyId, combatantId, numericInit);
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

  const handleQuickCombatantSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!quickName.trim()) {
      setQuickError('El nombre es obligatorio.');
      return;
    }

    const hpNum = Math.max(1, Number(quickHp) || 1);
    const acNum = Math.max(0, Number(quickAc) || 10);
    const initNum = quickInitiative !== '' ? Number(quickInitiative) : null;
    const newId =
      typeof crypto !== 'undefined' && crypto.randomUUID
        ? crypto.randomUUID()
        : `quick-${Date.now()}`;

    // Actualización optimista inmediata
    const newDto: LiveCombatantDmDto = {
      id: newId,
      name: quickName.trim(),
      initiative: initNum,
      maxHp: hpNum,
      currentHp: hpNum,
      armorClass: acNum,
      isPlayerCharacter: quickIsHero,
      isVisibleToPlayers: true,
      assignedPlayerUserId: null,
      conditions: null,
      sortOrder: localCombatants.length,
    };

    setLocalCombatants((prev) => [...prev, newDto]);
    setIsQuickModalOpen(false);
    setQuickName('');
    setQuickHp(20);
    setQuickAc(12);
    setQuickInitiative('');
    setQuickIsHero(false);
    setQuickError(null);

    try {
      await liveCombatHubService.addQuickCombatant(partyId, {
        name: newDto.name,
        maxHp: newDto.maxHp,
        armorClass: newDto.armorClass,
        isPlayerCharacter: newDto.isPlayerCharacter,
        isVisibleToPlayers: newDto.isVisibleToPlayers,
        initiative: newDto.initiative,
      });
    } catch (err) {
      console.warn('[LiveCombatTracker] Error adding quick combatant:', err);
    }
  };

  const renderQuickCombatantModal = () => {
    if (!isQuickModalOpen) return null;

    return (
      <div className="modal-backdrop" id="quick-combatant-modal-backdrop" style={{ zIndex: 1100 }}>
        <div
          className="modal-content quick-combatant-modal-dialog"
          role="dialog"
          aria-modal="true"
          id="quick-combatant-modal"
          style={{ maxWidth: '420px', width: '90%' }}
        >
          <div className="modal-header">
            <h3 className="modal-title" style={{ fontSize: '1.15rem' }}>
              ⚡ {t('liveCombat.quickCombatantModalTitle', 'Añadir Combatiente Rápido')}
            </h3>
            <button
              type="button"
              id="quick-combatant-close-btn"
              className="modal-close-btn"
              onClick={() => setIsQuickModalOpen(false)}
            >
              ✕
            </button>
          </div>
          <form onSubmit={handleQuickCombatantSubmit} className="modal-body quick-combatant-form">
            {quickError && <div className="form-error-banner">⚠️ {quickError}</div>}
            <div className="form-group" style={{ marginBottom: '12px' }}>
              <label htmlFor="quick-combatant-name">
                {t('liveCombat.quickCombatantName', 'Nombre')} *
              </label>
              <input
                type="text"
                id="quick-combatant-name"
                className="form-input"
                value={quickName}
                onChange={(e) => setQuickName(e.target.value)}
                placeholder="ej. Goblin Arquero o Sir Tristan"
                required
                autoFocus
              />
            </div>

            <div
              className="form-row-dual"
              style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px', marginBottom: '12px' }}
            >
              <div className="form-group">
                <label htmlFor="quick-combatant-hp">
                  {t('liveCombat.quickCombatantHp', 'Puntos de Golpe (HP)')} *
                </label>
                <input
                  type="number"
                  id="quick-combatant-hp"
                  className="form-input"
                  min={1}
                  value={quickHp}
                  onChange={(e) => setQuickHp(e.target.value === '' ? '' : Number(e.target.value))}
                  required
                />
              </div>

              <div className="form-group">
                <label htmlFor="quick-combatant-ac">
                  {t('liveCombat.quickCombatantAc', 'Clase de Armadura (CA)')} *
                </label>
                <input
                  type="number"
                  id="quick-combatant-ac"
                  className="form-input"
                  min={0}
                  value={quickAc}
                  onChange={(e) => setQuickAc(e.target.value === '' ? '' : Number(e.target.value))}
                  required
                />
              </div>
            </div>

            <div
              className="form-row-dual"
              style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px', marginBottom: '16px' }}
            >
              <div className="form-group">
                <label htmlFor="quick-combatant-type">
                  {t('liveCombat.quickCombatantType', 'Tipo')}
                </label>
                <select
                  id="quick-combatant-type"
                  className="form-select"
                  value={quickIsHero ? 'hero' : 'monster'}
                  onChange={(e) => setQuickIsHero(e.target.value === 'hero')}
                >
                  <option value="monster">👾 {t('liveCombat.quickCombatantMonster', 'Monstruo / NPC')}</option>
                  <option value="hero">🛡️ {t('liveCombat.quickCombatantHero', 'Héroe / PJ')}</option>
                </select>
              </div>

              <div className="form-group">
                <label htmlFor="quick-combatant-initiative">
                  {t('liveCombat.quickCombatantInitiative', 'Iniciativa')}
                </label>
                <input
                  type="number"
                  id="quick-combatant-initiative"
                  className="form-input"
                  placeholder="-"
                  value={quickInitiative}
                  onChange={(e) => setQuickInitiative(e.target.value === '' ? '' : Number(e.target.value))}
                />
              </div>
            </div>

            <div className="form-actions-right" style={{ display: 'flex', justifyContent: 'flex-end', gap: '8px' }}>
              <button
                type="button"
                id="quick-combatant-cancel-btn"
                className="btn btn-secondary btn-sm"
                onClick={() => setIsQuickModalOpen(false)}
              >
                {t('liveCombat.quickCombatantCancel', 'Cancelar')}
              </button>
              <button
                type="submit"
                id="quick-combatant-submit-btn"
                className="btn btn-primary btn-sm primary-gold"
                disabled={!quickName.trim()}
              >
                + {t('liveCombat.quickCombatantSubmit', 'Añadir')}
              </button>
            </div>
          </form>
        </div>
      </div>
    );
  };

  if (!combatState || localCombatants.length === 0) {
    return (
      <div className="live-combat-tracker-panel" id="live-combat-tracker">
        {isDm && (
          <div className="dm-master-controls-bar" id="dm-master-controls">
            <div className="dm-controls-title">👑 {t('liveCombat.roleDm', 'Dungeon Master')}</div>
            <div className="dm-buttons-group">
              <button
                type="button"
                id="add-quick-combatant-btn"
                className="btn btn-primary btn-sm primary-gold"
                onClick={() => setIsQuickModalOpen(true)}
              >
                + {t('liveCombat.addQuickCombatantBtn', 'Añadir Combatiente Rápido')}
              </button>
            </div>
          </div>
        )}
        <div className="live-combat-empty-state" id="live-combat-empty-state">
          <div className="empty-state-icon">⚔️</div>
          <h3>{t('liveCombat.waitingInitiative', 'Esperando Combate')}</h3>
          <p>{t('liveCombat.emptyCombatants', 'No hay combatientes en este combate activo.')}</p>
          {isDm && (
            <div style={{ marginTop: '1rem' }}>
              <button
                type="button"
                id="add-quick-combatant-empty-btn"
                className="btn btn-secondary btn-sm"
                onClick={() => setIsQuickModalOpen(true)}
              >
                + {t('liveCombat.addQuickCombatantBtn', 'Añadir Combatiente Rápido')}
              </button>
            </div>
          )}
        </div>
        {renderQuickCombatantModal()}
      </div>
    );
  }

  const { status, currentRound, activeCombatantId, activeCombatantName } = combatState;

  // Encontrar héroes jugables para el selector de iniciativa del jugador
  const playerHeroes = localCombatants.filter((c) => c.isPlayerCharacter);

  return (
    <div className="live-combat-tracker-panel" id="live-combat-tracker">
      {/* Barra superior de Estado del Combate */}
      <div className="combat-status-header">
        <div className="status-badge-container">
          <span
            className={`status-pill status-${status.toLowerCase()}`}
            id="combat-status-badge"
          >
            {status === 'WaitingForInitiative' &&
              `⏳ ${t('liveCombat.waitingInitiative', 'Esperando Iniciativas')}`}
            {status === 'InProgress' &&
              `⚔️ ${t('liveCombat.inProgress', { round: currentRound })}`}
            {status === 'Ended' &&
              `🛑 ${t('liveCombat.combatEnded', 'Combate Finalizado')}`}
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
            <button
              type="button"
              id="add-quick-combatant-btn"
              className="btn btn-outline btn-sm"
              onClick={() => setIsQuickModalOpen(true)}
            >
              + {t('liveCombat.addQuickCombatantBtn', 'Añadir Combatiente Rápido')}
            </button>
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
              onChange={(e) =>
                setPlayerInitiativeValue(
                  e.target.value === '' ? '' : Number(e.target.value)
                )
              }
              placeholder="-"
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

      {/* Tabla de Combatientes en el Orden de Turnos con Drag & Drop */}
      <div className="combatants-table-container">
        <DndContext
          sensors={sensors}
          collisionDetection={closestCenter}
          onDragEnd={handleDragEnd}
        >
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
            <SortableContext
              items={localCombatants.map((c) => c.id)}
              strategy={verticalListSortingStrategy}
            >
              <tbody>
                {localCombatants.map((c, index) => (
                  <SortableCombatantRow
                    key={c.id}
                    combatant={c}
                    index={index}
                    isActive={c.id === activeCombatantId}
                    isDm={isDm}
                    overrideCombatantId={overrideCombatantId}
                    overrideValue={overrideValue}
                    setOverrideCombatantId={setOverrideCombatantId}
                    setOverrideValue={setOverrideValue}
                    handleDmOverrideInitiative={handleDmOverrideInitiative}
                    handleModifyHp={handleModifyHp}
                    handleToggleVisibility={handleToggleVisibility}
                  />
                ))}
              </tbody>
            </SortableContext>
          </table>
        </DndContext>
      </div>
      {renderQuickCombatantModal()}
    </div>
  );
};
