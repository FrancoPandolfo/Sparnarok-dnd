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
import type { PartyResponseDto } from '../types/partyEncounter';
import type { CombatInviteNotificationDto } from '../services/liveCombatHubService';

export interface LiveCombatTrackerProps {
  partyId?: string | null;
  party?: PartyResponseDto | null;
  role: PartyRole;
  combatState: LiveCombatDmProjectionDto | LiveCombatPlayerProjectionDto | null;
  currentUserId?: string | null;
  pendingInvite?: CombatInviteNotificationDto | null;
  onClearInvite?: () => void;
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
  party,
  role,
  combatState,
  currentUserId,
  pendingInvite,
  onClearInvite,
}) => {
  const effectivePartyId = partyId || 'quick-session';
  const { t } = useTranslation();

  // Estado local para actualización optimista del ordenamiento
  const [localCombatants, setLocalCombatants] = useState<
    (LiveCombatantDmDto | LiveCombatantPlayerDto)[]
  >(combatState?.combatants ?? []);

  // Sincronizar estado local cuando SignalR actualiza el estado del servidor o se cambia de Party
  useEffect(() => {
    if (combatState?.combatants) {
      setLocalCombatants(combatState.combatants);
    } else {
      setLocalCombatants([]);
    }
  }, [combatState?.combatants, partyId]);

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

  // Estado para el panel de miembros de party e invitaciones (DM)
  const [isPartyMembersOpen, setIsPartyMembersOpen] = useState(false);
  const [invitedUserIds, setInvitedUserIds] = useState<string[]>([]);

  // Estado para invitaciones recibidas y modal de unirse al combate (Jugador)
  const [localInvite, setLocalInvite] = useState<CombatInviteNotificationDto | null>(pendingInvite || null);
  const [isJoinModalOpen, setIsJoinModalOpen] = useState(false);
  const [joinCharName, setJoinCharName] = useState('');
  const [joinCharHp, setJoinCharHp] = useState<number | string>(20);
  const [joinInitiative, setJoinInitiative] = useState<number | string>(10);
  const [joinError, setJoinError] = useState<string | null>(null);

  const isDm = role === 'DungeonMaster';

  const effectiveUserId = currentUserId || (() => {
    try {
      const authStr = typeof window !== 'undefined' ? window.localStorage.getItem('sparnarok_auth') : null;
      return authStr ? JSON.parse(authStr).id : null;
    } catch {
      return null;
    }
  })();

  useEffect(() => {
    setLocalInvite(pendingInvite || null);
  }, [pendingInvite]);

  useEffect(() => {
    const handleLocalInvite = (e: any) => {
      if (e.detail && !isDm) {
        if (!e.detail.targetUserId || !effectiveUserId || e.detail.targetUserId === effectiveUserId) {
          setLocalInvite(e.detail);
        }
      }
    };
    window.addEventListener('sparnarok_combat_invite_local', handleLocalInvite);
    return () => {
      window.removeEventListener('sparnarok_combat_invite_local', handleLocalInvite);
    };
  }, [isDm, effectiveUserId]);

  const currentMember = party?.partyMembers?.find(
    (m) => effectiveUserId && m.userId === effectiveUserId
  );

  const isCombatActive =
    Boolean(combatState) &&
    combatState!.status !== 'NotStarted' &&
    combatState!.status !== 'Ended';

  const isPlayerInCombat = localCombatants.some((c) => {
    const assignedUserId = (c as any).assignedPlayerUserId;
    if (effectiveUserId && assignedUserId && assignedUserId === effectiveUserId) return true;
    if (effectiveUserId && c.id === effectiveUserId) return true;
    if (currentMember && c.name.toLowerCase() === currentMember.inGameName?.toLowerCase()) return true;
    // Si la party no tiene registro de miembros individuales (partyMembers), cualquier PJ en combate cuenta
    if (!party?.partyMembers || party.partyMembers.length === 0) {
      if (c.isPlayerCharacter) return true;
    }
    // Fallback para tests aislados sin contexto de usuario o party
    if (!effectiveUserId && !party && c.isPlayerCharacter) return true;
    return false;
  });

  // Miembros de la party que actualmente NO están en combate (para el panel del DM)
  const unjoinedMembers: { userId: string; name: string }[] = [];
  if (party?.partyMembers && party.partyMembers.length > 0) {
    for (const m of party.partyMembers) {
      if (m.role === 'Player' || (party.ownerUserId && m.userId !== party.ownerUserId)) {
        const isJoined = localCombatants.some((c) => {
          const assignedUserId = (c as any).assignedPlayerUserId;
          return (
            (assignedUserId && assignedUserId === m.userId) ||
            c.id === m.userId ||
            c.name.toLowerCase() === m.inGameName?.toLowerCase()
          );
        });
        if (!isJoined) {
          unjoinedMembers.push({
            userId: m.userId,
            name: m.inGameName || m.userId,
          });
        }
      }
    }
  } else if (party?.members && party.members.length > 0) {
    for (const m of party.members) {
      const isJoined = localCombatants.some(
        (c) => c.id === m.id || c.name.toLowerCase() === m.name.toLowerCase()
      );
      if (!isJoined) {
        unjoinedMembers.push({
          userId: m.id,
          name: m.name,
        });
      }
    }
  }

  const handleInvitePlayer = async (targetUserId: string) => {
    try {
      await liveCombatHubService.invitePlayerToEncounter(effectivePartyId, targetUserId);
      setInvitedUserIds((prev) => [...prev, targetUserId]);
    } catch (err) {
      console.error('[LiveCombatTracker] Error inviting player:', err);
    }
  };

  const handleExecuteJoin = async (initiativeVal?: number | string) => {
    let authName = 'Héroe';
    try {
      if (typeof window !== 'undefined') {
        const authStr = window.localStorage.getItem('sparnarok_auth');
        if (authStr) {
          const parsed = JSON.parse(authStr);
          authName = parsed.displayName || parsed.globalNickname || 'Héroe';
        }
      }
    } catch {}

    const heroName = joinCharName.trim() || currentMember?.inGameName || authName;
    const finalHp = Number(joinCharHp) || 20;
    const finalInit =
      initiativeVal !== undefined && initiativeVal !== ''
        ? Number(initiativeVal)
        : joinInitiative !== ''
        ? Number(joinInitiative)
        : 10;

    const playerDto: LiveCombatantPlayerDto = {
      id: effectiveUserId || `player-${Date.now()}`,
      name: heroName,
      initiative: finalInit,
      healthStatus: 'Sano',
      isPlayerCharacter: true,
      currentHp: finalHp,
      maxHp: finalHp,
      assignedPlayerUserId: effectiveUserId,
    };

    try {
      await liveCombatHubService.joinEncounter(effectivePartyId, playerDto);
      setIsJoinModalOpen(false);
      setLocalInvite(null);
      try {
        if (typeof window !== 'undefined') {
          window.localStorage.removeItem('sparnarok_combat_invite');
        }
      } catch {}
      onClearInvite?.();
    } catch (err) {
      console.error('[LiveCombatTracker] Error joining encounter:', err);
      setJoinError('Error al unirse al combate');
    }
  };

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
        await liveCombatHubService.reorderCombatant(effectivePartyId, String(active.id), newIndex);
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
    await liveCombatHubService.submitInitiative(effectivePartyId, targetHeroId, numericInit);
  };

  const handleDmOverrideInitiative = async (combatantId: string) => {
    const numericInit = overrideValue === '' ? null : Number(overrideValue);
    await liveCombatHubService.overrideInitiative(effectivePartyId, combatantId, numericInit);
    setOverrideCombatantId(null);
  };

  const handleAutoRollMonsters = async () => {
    await liveCombatHubService.autoRollMonsters(effectivePartyId);
  };

  const handleStartCombat = async () => {
    await liveCombatHubService.startCombat(effectivePartyId);
  };

  const handleNextTurn = async () => {
    await liveCombatHubService.nextTurn(effectivePartyId);
  };

  const handlePreviousTurn = async () => {
    await liveCombatHubService.previousTurn(effectivePartyId);
  };

  const handleEndCombat = async () => {
    await liveCombatHubService.endCombat(effectivePartyId);
  };

  const handleModifyHp = async (combatantId: string, delta: number) => {
    await liveCombatHubService.modifyHp(effectivePartyId, combatantId, delta);
  };

  const handleToggleVisibility = async (combatantId: string, currentVisibility: boolean) => {
    await liveCombatHubService.toggleVisibility(effectivePartyId, combatantId, !currentVisibility);
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
      await liveCombatHubService.addQuickCombatant(effectivePartyId, {
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

  const renderInviteModal = () => {
    if (isDm || !localInvite) return null;
    if (localInvite.targetUserId && effectiveUserId && localInvite.targetUserId !== effectiveUserId) {
      return null;
    }
    return (
      <div
        className="modal-backdrop combat-invite-backdrop"
        id="combat-invite-modal"
        style={{ zIndex: 1100 }}
      >
        <div
          className="modal-content combat-invite-dialog"
          style={{
            maxWidth: '450px',
            textAlign: 'center',
            padding: '24px',
            borderRadius: '8px',
          }}
        >
          <div style={{ fontSize: '2.5rem', marginBottom: '8px' }}>⚔️</div>
          <h3 style={{ marginBottom: '8px', color: 'var(--color-gold, #c8aa6e)' }}>
            {t('liveCombat.inviteTitle', '¡Invitación a Combate!')}
          </h3>
          <p
            style={{
              marginBottom: '16px',
              fontSize: '1rem',
              lineHeight: '1.4',
              color: 'var(--color-text-primary, #f0e6d2)',
            }}
          >
            {t(
              'liveCombat.inviteMessage',
              'El Dungeon Master te invita a unirte al combate actual'
            )}
          </p>

          <div
            className="form-group"
            style={{ marginBottom: '20px', textAlign: 'left' }}
          >
            <label
              htmlFor="invite-initiative-input"
              style={{ fontSize: '0.875rem', fontWeight: 600 }}
            >
              {t('liveCombat.inviteInitiativeLabel', 'Tu Iniciativa (opcional):')}
            </label>
            <input
              type="number"
              id="invite-initiative-input"
              className="form-input"
              min={1}
              max={40}
              value={joinInitiative}
              onChange={(e) =>
                setJoinInitiative(e.target.value === '' ? '' : Number(e.target.value))
              }
              placeholder="10"
            />
          </div>

          <div
            style={{
              display: 'flex',
              justifyContent: 'center',
              gap: '12px',
            }}
          >
            <button
              type="button"
              id="reject-combat-invite-btn"
              className="btn btn-secondary"
              onClick={() => {
                setLocalInvite(null);
                try {
                  if (typeof window !== 'undefined') {
                    window.localStorage.removeItem('sparnarok_combat_invite');
                  }
                } catch {}
                onClearInvite?.();
              }}
            >
              ✕ {t('liveCombat.rejectInvite', 'Rechazar')}
            </button>
            <button
              type="button"
              id="accept-combat-invite-btn"
              className="btn btn-primary primary-gold"
              onClick={() => handleExecuteJoin(joinInitiative)}
            >
              ✓ {t('liveCombat.acceptInvite', 'Aceptar')}
            </button>
          </div>
        </div>
      </div>
    );
  };

  const renderPlayerJoinModal = () => {
    if (!isJoinModalOpen) return null;
    return (
      <div
        className="modal-backdrop"
        id="player-join-modal"
        style={{ zIndex: 1050 }}
      >
        <div
          className="modal-content"
          style={{ maxWidth: '420px', padding: '20px', borderRadius: '8px' }}
        >
          <div
            className="modal-header"
            style={{
              marginBottom: '16px',
              display: 'flex',
              justifyContent: 'space-between',
              alignItems: 'center',
            }}
          >
            <h3 style={{ margin: 0, color: 'var(--color-gold, #c8aa6e)' }}>
              ⚔️ {t('liveCombat.joinCombatTitle', 'Unirse al Combate')}
            </h3>
            <button
              type="button"
              className="modal-close-btn"
              onClick={() => setIsJoinModalOpen(false)}
            >
              ✕
            </button>
          </div>
          {joinError && (
            <div className="alert-error" style={{ marginBottom: '12px' }}>
              {joinError}
            </div>
          )}
          <form
            onSubmit={(e) => {
              e.preventDefault();
              handleExecuteJoin();
            }}
          >
            <div className="form-group" style={{ marginBottom: '12px' }}>
              <label htmlFor="join-player-name">
                {t('liveCombat.playerName', 'Nombre de tu Personaje')}
              </label>
              <input
                type="text"
                id="join-player-name"
                className="form-input"
                value={joinCharName}
                onChange={(e) => setJoinCharName(e.target.value)}
                placeholder={currentMember?.inGameName || 'Tu Personaje'}
              />
            </div>
            <div
              className="form-row-dual"
              style={{
                display: 'grid',
                gridTemplateColumns: '1fr 1fr',
                gap: '12px',
                marginBottom: '16px',
              }}
            >
              <div className="form-group">
                <label htmlFor="join-player-hp">
                  {t('liveCombat.playerHp', 'HP Máximo')}
                </label>
                <input
                  type="number"
                  id="join-player-hp"
                  className="form-input"
                  min={1}
                  value={joinCharHp}
                  onChange={(e) =>
                    setJoinCharHp(e.target.value === '' ? '' : Number(e.target.value))
                  }
                />
              </div>
              <div className="form-group">
                <label htmlFor="join-player-initiative">
                  {t('liveCombat.playerInitiative', 'Iniciativa')}
                </label>
                <input
                  type="number"
                  id="join-player-initiative"
                  className="form-input"
                  min={1}
                  max={40}
                  value={joinInitiative}
                  onChange={(e) =>
                    setJoinInitiative(e.target.value === '' ? '' : Number(e.target.value))
                  }
                  placeholder="10"
                />
              </div>
            </div>
            <div
              style={{
                display: 'flex',
                justifyContent: 'flex-end',
                gap: '8px',
              }}
            >
              <button
                type="button"
                className="btn btn-secondary btn-sm"
                onClick={() => setIsJoinModalOpen(false)}
              >
                {t('liveCombat.cancel', 'Cancelar')}
              </button>
              <button
                type="submit"
                id="confirm-join-combat-btn"
                className="btn btn-primary btn-sm primary-gold"
              >
                ⚔️ {t('liveCombat.confirmJoin', 'Entrar al Combate')}
              </button>
            </div>
          </form>
        </div>
      </div>
    );
  };

  const renderDmPartyMembersPanel = () => {
    if (!isDm || !isPartyMembersOpen) return null;
    return (
      <div
        className="party-members-invite-panel"
        id="party-members-invite-panel"
        style={{
          background: 'var(--color-surface-elevated, #2a2e35)',
          border: '1px solid var(--color-gold, #c8aa6e)',
          borderRadius: '6px',
          padding: '12px 16px',
          marginBottom: '16px',
        }}
      >
        <div
          style={{
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'center',
            marginBottom: '10px',
          }}
        >
          <h4 style={{ margin: 0, fontSize: '0.95rem', color: 'var(--color-gold, #c8aa6e)' }}>
            👥 {t('liveCombat.partyMembersTitle', 'Miembros de la Party')}
          </h4>
          <span style={{ fontSize: '0.8rem', color: 'var(--color-text-muted, #7c7c7c)' }}>
            {unjoinedMembers.length === 0
              ? t('liveCombat.allMembersJoined', 'Todos los miembros están en combate')
              : t('liveCombat.unjoinedCount', `Sin unirse: ${unjoinedMembers.length}`)}
          </span>
        </div>

        {unjoinedMembers.length === 0 ? (
          <p style={{ margin: 0, fontSize: '0.85rem', color: 'var(--color-text-secondary, #a09b8c)' }}>
            {t('liveCombat.allInInitiative', 'Todos los jugadores de la party ya están en la lista de iniciativa.')}
          </p>
        ) : (
          <ul
            className="unjoined-members-list"
            style={{
              listStyle: 'none',
              margin: 0,
              padding: 0,
              display: 'flex',
              flexWrap: 'wrap',
              gap: '8px',
            }}
          >
            {unjoinedMembers.map((member) => {
              const isInvited = invitedUserIds.includes(member.userId);
              return (
                <li
                  key={member.userId}
                  className="unjoined-member-item"
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    gap: '8px',
                    background: 'rgba(0, 0, 0, 0.25)',
                    padding: '6px 10px',
                    borderRadius: '4px',
                    border: '1px solid var(--color-border, #3c3c41)',
                  }}
                >
                  <span className="member-name" style={{ fontWeight: 500, fontSize: '0.85rem' }}>
                    🛡️ {member.name}
                  </span>
                  <button
                    type="button"
                    id={`invite-member-btn-${member.userId}`}
                    className={`btn btn-xs ${isInvited ? 'btn-secondary' : 'btn-primary primary-gold'}`}
                    disabled={isInvited}
                    onClick={() => handleInvitePlayer(member.userId)}
                    style={{ fontSize: '0.75rem', padding: '2px 8px' }}
                  >
                    {isInvited ? '✓ Invitado' : '✉️ Invitar'}
                  </button>
                </li>
              );
            })}
          </ul>
        )}
      </div>
    );
  };

  // Vista del Jugador - El Lobby cuando hay un combate activo pero el jugador NO está en iniciativa
  if (!isDm && isCombatActive && !isPlayerInCombat) {
    return (
      <div className="live-combat-tracker-panel" id="live-combat-tracker">
        <div
          className="live-combat-lobby-panel"
          id="player-combat-lobby"
          style={{
            textAlign: 'center',
            padding: '40px 20px',
            background: 'var(--color-surface, #1e2328)',
            borderRadius: '8px',
            border: '1px solid var(--color-border, #3c3c41)',
            marginTop: '20px',
          }}
        >
          <div style={{ fontSize: '3rem', marginBottom: '12px' }}>🛡️</div>
          <h3
            style={{
              fontSize: '1.5rem',
              marginBottom: '8px',
              color: 'var(--color-gold, #c8aa6e)',
            }}
          >
            {t('liveCombat.lobbyTitle', 'Un encuentro está en curso')}
          </h3>
          <p
            style={{
              color: 'var(--color-text-secondary, #a09b8c)',
              maxWidth: '480px',
              margin: '0 auto 24px auto',
              lineHeight: '1.5',
            }}
          >
            {t(
              'liveCombat.lobbyDesc',
              'El Dungeon Master ha iniciado el combate para esta party. Aún no estás en la lista de iniciativa. Puedes unirte al combate ahora o esperar una invitación del DM.'
            )}
          </p>

          <div style={{ display: 'flex', justifyContent: 'center', gap: '12px' }}>
            <button
              type="button"
              id="join-encounter-lobby-btn"
              className="btn btn-primary btn-lg primary-gold"
              onClick={() => setIsJoinModalOpen(true)}
              style={{ padding: '12px 28px', fontSize: '1.1rem', fontWeight: 600 }}
            >
              ⚔️ {t('liveCombat.joinCombatBtn', 'Unirse al Combate')}
            </button>
          </div>
        </div>
        {renderInviteModal()}
        {renderPlayerJoinModal()}
      </div>
    );
  }

  if (!combatState || localCombatants.length === 0) {
    return (
      <div className="live-combat-tracker-panel" id="live-combat-tracker">
        {isDm && (
          <>
            <div className="dm-master-controls-bar" id="dm-master-controls">
              <div className="dm-controls-title">👑 {t('liveCombat.roleDm', 'Dungeon Master')}</div>
              <div className="dm-buttons-group">
                <button
                  type="button"
                  id="toggle-party-members-btn"
                  className={`btn btn-sm ${isPartyMembersOpen ? 'btn-primary' : 'btn-secondary'}`}
                  onClick={() => setIsPartyMembersOpen((prev) => !prev)}
                >
                  👥 {t('liveCombat.partyMembersBtn', 'Miembros de la Party')} ({unjoinedMembers.length})
                </button>
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
            {renderDmPartyMembersPanel()}
          </>
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
        {renderInviteModal()}
        {renderPlayerJoinModal()}
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
        <>
          <div className="dm-master-controls-bar" id="dm-master-controls">
            <div className="dm-controls-title">👑 {t('liveCombat.roleDm', 'Dungeon Master')}</div>
            <div className="dm-buttons-group">
              <button
                type="button"
                id="toggle-party-members-btn"
                className={`btn btn-sm ${isPartyMembersOpen ? 'btn-primary' : 'btn-secondary'}`}
                onClick={() => setIsPartyMembersOpen((prev) => !prev)}
              >
                👥 {t('liveCombat.partyMembersBtn', 'Miembros de la Party')} ({unjoinedMembers.length})
              </button>
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
          {renderDmPartyMembersPanel()}
        </>
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
      {renderInviteModal()}
      {renderPlayerJoinModal()}
      {renderQuickCombatantModal()}
    </div>
  );
};
