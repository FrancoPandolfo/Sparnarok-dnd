import { HubConnection, HubConnectionBuilder, LogLevel, HubConnectionState } from '@microsoft/signalr';
import { apiClient } from './apiClient';
import type {
  LiveCombatDmProjectionDto,
  LiveCombatPlayerProjectionDto,
  LiveCombatantDmDto,
  LiveCombatantPlayerDto,
  InitializeLiveCombatRequest,
  PartyRole,
  LiveCombatStatus,
  AddQuickCombatantRequest,
} from '../types/liveCombat';

/**
 * Callbacks para eventos emitidos en tiempo real por LiveCombatHub.
 */
export interface LiveCombatHubCallbacks {
  onCombatStateUpdated?: (
    state: LiveCombatDmProjectionDto | LiveCombatPlayerProjectionDto
  ) => void;
  onError?: (err: unknown) => void;
}

interface RawCombatant {
  id: string;
  name: string;
  initiative: number | null;
  maxHp: number;
  currentHp: number;
  armorClass: number;
  isPlayerCharacter: boolean;
  isVisibleToPlayers: boolean;
  assignedPlayerUserId?: string | null;
  conditions?: string | null;
}

interface RawCombatState {
  partyId: string;
  partyName: string;
  status: LiveCombatStatus;
  currentRound: number;
  currentTurnIndex: number;
  combatants: RawCombatant[];
}

/**
 * Servicio cliente de SignalR para la sincronización en vivo del combate táctico y tracker de iniciativa
 * con proyecciones asimétricas diferenciadas para Dungeon Master y Jugadores.
 * Incluye fallback de sincronización local entre pestañas (BroadcastChannel) si el backend está desconectado.
 */
class LiveCombatHubService {
  private connection: HubConnection | null = null;
  private currentPartyId: string | null = null;
  private currentRole: PartyRole = 'Player';
  private callbacks: LiveCombatHubCallbacks | null = null;
  private broadcastChannel: BroadcastChannel | null = null;

  constructor() {
    if (typeof window !== 'undefined') {
      if ('BroadcastChannel' in window) {
        try {
          this.broadcastChannel = new BroadcastChannel('sparnarok_live_combat_channel');
          this.broadcastChannel.onmessage = (event) => {
            if (event.data?.partyId === this.currentPartyId) {
              this.emitFallbackProjection();
            }
          };
        } catch {
          // Fallback no disponible
        }
      }

      window.addEventListener('sparnarok_combat_external_update', (e: Event) => {
        const customEvent = e as CustomEvent;
        if (customEvent.detail?.partyId === this.currentPartyId) {
          this.emitFallbackProjection();
        }
      });

      window.addEventListener('storage', (e: StorageEvent) => {
        if (e.key?.startsWith('sparnarok_combat_')) {
          this.emitFallbackProjection();
        }
      });
    }
  }

  getCurrentRole(): PartyRole {
    return this.currentRole;
  }

  /**
   * Inicia o actualiza la conexión con el LiveCombatHub y se suscribe al grupo correspondiente.
   */
  async startConnection(
    partyId: string,
    role: PartyRole,
    callbacks: LiveCombatHubCallbacks
  ): Promise<void> {
    this.callbacks = callbacks;
    this.currentRole = role;
    this.currentPartyId = partyId;

    // Emitir inmediatamente la proyección local / caché para respuesta instantánea (cero latencia)
    this.emitFallbackProjection();

    // Si ya existe una conexión activa
    if (this.connection && this.connection.state === HubConnectionState.Connected) {
      try {
        await this.connection.invoke('JoinCombat', partyId, role);
      } catch (err) {
        console.warn('[LiveCombatHub] Error al unirse a nuevo combate:', err);
        callbacks.onError?.(err);
      }
      return;
    }

    if (this.connection) {
      try {
        await this.connection.stop();
      } catch {
        // no-op
      }
      this.connection = null;
    }

    const connection = new HubConnectionBuilder()
      .withUrl('/hubs/live-combat', {
        accessTokenFactory: () => apiClient.getAuthToken() ?? '',
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    connection.on(
      'CombatStateUpdated',
      (state: LiveCombatDmProjectionDto | LiveCombatPlayerProjectionDto) => {
        this.callbacks?.onCombatStateUpdated?.(state);
      }
    );

    try {
      await connection.start();
      await connection.invoke('JoinCombat', partyId, role);
      this.connection = connection;
    } catch {
      // Si la conexión al backend no responde, se mantiene la proyección local activa
      this.emitFallbackProjection();
    }
  }

  /**
   * Abandona el canal de combate y cierra la conexión.
   */
  async stopConnection(): Promise<void> {
    if (this.connection) {
      if (this.currentPartyId && this.connection.state === HubConnectionState.Connected) {
        try {
          await this.connection.invoke('LeaveCombat', this.currentPartyId);
        } catch {
          // no-op
        }
      }
      try {
        await this.connection.stop();
      } catch {
        // no-op
      }
      this.connection = null;
      this.currentPartyId = null;
    }
  }

  /**
   * Inicializa un combate con una lista de combatientes (DM).
   */
  async initializeCombat(
    partyId: string,
    request: InitializeLiveCombatRequest
  ): Promise<void> {
    if (this.connection && this.connection.state === HubConnectionState.Connected) {
      await this.connection.invoke('InitializeCombat', partyId, request);
      return;
    }

    // Fallback local
    const rawCombatants: RawCombatant[] = request.combatants.map((c) => ({
      id: c.id,
      name: c.name,
      initiative: c.initiative ?? null,
      maxHp: c.maxHp,
      currentHp: c.maxHp,
      armorClass: c.armorClass,
      isPlayerCharacter: c.isPlayerCharacter,
      isVisibleToPlayers: c.isVisibleToPlayers ?? true,
      assignedPlayerUserId: c.assignedPlayerUserId ?? null,
      conditions: null,
    }));

    const rawState: RawCombatState = {
      partyId,
      partyName: request.partyName,
      status: 'WaitingForInitiative',
      currentRound: 1,
      currentTurnIndex: 0,
      combatants: rawCombatants,
    };

    this.saveRawState(partyId, rawState);
    this.broadcastFallbackUpdate(partyId);
  }

  /**
   * Registra la iniciativa de un jugador (o limpia el valor si es null).
   */
  async submitInitiative(
    partyId: string,
    combatantId: string,
    initiative: number | null
  ): Promise<void> {
    if (this.connection && this.connection.state === HubConnectionState.Connected) {
      await this.connection.invoke('SubmitInitiative', partyId, combatantId, initiative);
      return;
    }

    const state = this.getRawState(partyId);
    if (!state) return;

    const combatant = state.combatants.find((c) => c.id === combatantId);
    if (combatant) {
      combatant.initiative = initiative;
      this.saveRawState(partyId, state);
      this.broadcastFallbackUpdate(partyId);
    }
  }

  /**
   * Sobreescribe la iniciativa de cualquier combatiente (DM).
   */
  async overrideInitiative(
    partyId: string,
    combatantId: string,
    initiative: number | null
  ): Promise<void> {
    if (this.connection && this.connection.state === HubConnectionState.Connected) {
      await this.connection.invoke('OverrideInitiative', partyId, combatantId, initiative);
      return;
    }

    await this.submitInitiative(partyId, combatantId, initiative);
  }

  /**
   * Tira la iniciativa automáticamente para todos los monstruos (DM).
   */
  async autoRollMonsters(partyId: string): Promise<void> {
    if (this.connection && this.connection.state === HubConnectionState.Connected) {
      await this.connection.invoke('AutoRollMonsters', partyId);
      return;
    }

    const state = this.getRawState(partyId);
    if (!state) return;

    for (const combatant of state.combatants) {
      if (!combatant.isPlayerCharacter) {
        combatant.initiative = Math.floor(Math.random() * 20) + 1;
      }
    }

    this.saveRawState(partyId, state);
    this.broadcastFallbackUpdate(partyId);
  }

  /**
   * Reordena un combatiente moviéndolo a una nueva posición en la lista (Drag & Drop en vivo).
   */
  async reorderCombatant(
    partyId: string,
    combatantId: string,
    newIndex: number
  ): Promise<void> {
    if (this.connection && this.connection.state === HubConnectionState.Connected) {
      await this.connection.invoke('ReorderCombatant', combatantId, newIndex);
      return;
    }

    const state = this.getRawState(partyId);
    if (!state || state.combatants.length === 0) return;

    const oldIndex = state.combatants.findIndex((c) => c.id === combatantId);
    if (oldIndex === -1) return;

    const clampedIndex = Math.max(0, Math.min(newIndex, state.combatants.length - 1));
    const [moved] = state.combatants.splice(oldIndex, 1);
    state.combatants.splice(clampedIndex, 0, moved);

    this.saveRawState(partyId, state);
    this.broadcastFallbackUpdate(partyId);
  }

  /**
   * Añade un combatiente rápido en caliente directamente a la iniciativa activa (DM).
   */
  async addQuickCombatant(
    partyId: string,
    request: AddQuickCombatantRequest
  ): Promise<void> {
    if (this.connection && this.connection.state === HubConnectionState.Connected) {
      try {
        await this.connection.invoke('AddQuickCombatant', partyId, request);
        return;
      } catch (err) {
        console.warn('[LiveCombatHub] Error al invocar AddQuickCombatant vía SignalR, usando fallback local:', err);
      }
    }

    let state = this.getRawState(partyId);
    if (!state) {
      state = {
        partyId,
        partyName: partyId === 'quick-session' ? 'Partida Rápida' : 'Combate en Vivo',
        status: 'WaitingForInitiative',
        currentRound: 1,
        currentTurnIndex: 0,
        combatants: [],
      };
    }

    const newCombatant: RawCombatant = {
      id: typeof crypto !== 'undefined' && crypto.randomUUID
        ? crypto.randomUUID()
        : `quick-${Date.now()}-${Math.random().toString(36).substring(2, 7)}`,
      name: request.name.trim(),
      initiative: request.initiative ?? null,
      maxHp: Math.max(1, request.maxHp),
      currentHp: Math.max(1, request.maxHp),
      armorClass: Math.max(0, request.armorClass),
      isPlayerCharacter: request.isPlayerCharacter ?? false,
      isVisibleToPlayers: request.isVisibleToPlayers ?? true,
      assignedPlayerUserId: null,
      conditions: null,
    };

    state.combatants.push(newCombatant);
    this.saveRawState(partyId, state);
    this.broadcastFallbackUpdate(partyId);
  }

  /**
   * Inicia el combate formalmente respetando el orden posicional establecido (DM).
   */
  async startCombat(partyId: string): Promise<void> {
    if (this.connection && this.connection.state === HubConnectionState.Connected) {
      await this.connection.invoke('StartCombat', partyId);
      return;
    }

    const state = this.getRawState(partyId);
    if (!state || state.combatants.length === 0) return;

    // Preservar el orden posicional establecido por el DM; la iniciativa numérica es opcional
    state.status = 'InProgress';
    state.currentRound = 1;
    state.currentTurnIndex = 0;

    this.saveRawState(partyId, state);
    this.broadcastFallbackUpdate(partyId);
  }

  /**
   * Pasa al siguiente turno (DM).
   */
  async nextTurn(partyId: string): Promise<void> {
    if (this.connection && this.connection.state === HubConnectionState.Connected) {
      await this.connection.invoke('NextTurn', partyId);
      return;
    }

    const state = this.getRawState(partyId);
    if (!state || state.status !== 'InProgress' || state.combatants.length === 0) return;

    state.currentTurnIndex++;
    if (state.currentTurnIndex >= state.combatants.length) {
      state.currentTurnIndex = 0;
      state.currentRound++;
    }

    this.saveRawState(partyId, state);
    this.broadcastFallbackUpdate(partyId);
  }

  /**
   * Regresa al turno anterior (DM).
   */
  async previousTurn(partyId: string): Promise<void> {
    if (this.connection && this.connection.state === HubConnectionState.Connected) {
      await this.connection.invoke('PreviousTurn', partyId);
      return;
    }

    const state = this.getRawState(partyId);
    if (!state || state.status !== 'InProgress' || state.combatants.length === 0) return;

    state.currentTurnIndex--;
    if (state.currentTurnIndex < 0) {
      state.currentTurnIndex = state.combatants.length - 1;
      state.currentRound = Math.max(1, state.currentRound - 1);
    }

    this.saveRawState(partyId, state);
    this.broadcastFallbackUpdate(partyId);
  }

  /**
   * Modifica los puntos de vida de un combatiente (DM).
   */
  async modifyHp(partyId: string, combatantId: string, deltaHp: number): Promise<void> {
    if (this.connection && this.connection.state === HubConnectionState.Connected) {
      await this.connection.invoke('ModifyHp', partyId, combatantId, deltaHp);
      return;
    }

    const state = this.getRawState(partyId);
    if (!state) return;

    const combatant = state.combatants.find((c) => c.id === combatantId);
    if (combatant) {
      combatant.currentHp = Math.max(0, Math.min(combatant.maxHp, combatant.currentHp + deltaHp));
      this.saveRawState(partyId, state);
      this.broadcastFallbackUpdate(partyId);
    }
  }

  /**
   * Alterna la visibilidad de un combatiente ante los jugadores (DM).
   */
  async toggleVisibility(
    partyId: string,
    combatantId: string,
    isVisible: boolean
  ): Promise<void> {
    if (this.connection && this.connection.state === HubConnectionState.Connected) {
      await this.connection.invoke('ToggleVisibility', partyId, combatantId, isVisible);
      return;
    }

    const state = this.getRawState(partyId);
    if (!state) return;

    const combatant = state.combatants.find((c) => c.id === combatantId);
    if (combatant) {
      combatant.isVisibleToPlayers = isVisible;
      this.saveRawState(partyId, state);
      this.broadcastFallbackUpdate(partyId);
    }
  }

  /**
   * Finaliza el combate activo (DM).
   */
  async endCombat(partyId: string): Promise<void> {
    if (this.connection && this.connection.state === HubConnectionState.Connected) {
      await this.connection.invoke('EndCombat', partyId);
      return;
    }

    const state = this.getRawState(partyId);
    if (!state) return;

    state.status = 'Ended';
    this.saveRawState(partyId, state);
    this.broadcastFallbackUpdate(partyId);
  }

  // --- Fallback & Proyección en Memoria / LocalStorage ---

  private getRawState(partyId: string): RawCombatState | null {
    if (typeof window === 'undefined') return null;
    try {
      const raw = window.localStorage.getItem(`sparnarok_combat_${partyId}`);
      return raw ? JSON.parse(raw) : null;
    } catch {
      return null;
    }
  }

  private saveRawState(partyId: string, state: RawCombatState): void {
    if (typeof window === 'undefined') return;
    try {
      window.localStorage.setItem(`sparnarok_combat_${partyId}`, JSON.stringify(state));
    } catch {
      // no-op
    }
  }

  private broadcastFallbackUpdate(partyId: string): void {
    this.emitFallbackProjection();
    if (this.broadcastChannel) {
      try {
        this.broadcastChannel.postMessage({ partyId });
      } catch {
        // no-op
      }
    }
    if (typeof window !== 'undefined' && typeof (window as any).__syncCombatState === 'function') {
      try {
        const raw = this.getRawState(partyId);
        (window as any).__syncCombatState(partyId, raw);
      } catch {
        // no-op
      }
    }
  }

  private emitFallbackProjection(): void {
    if (!this.currentPartyId || !this.callbacks?.onCombatStateUpdated) return;

    const raw = this.getRawState(this.currentPartyId);
    if (!raw) return;

    const activeCombatant =
      raw.combatants.length > 0 && raw.currentTurnIndex < raw.combatants.length
        ? raw.combatants[raw.currentTurnIndex]
        : null;

    if (this.currentRole === 'DungeonMaster') {
      const dmCombatants: LiveCombatantDmDto[] = raw.combatants.map((c) => ({
        id: c.id,
        name: c.name,
        initiative: c.initiative,
        maxHp: c.maxHp,
        currentHp: c.currentHp,
        armorClass: c.armorClass,
        isPlayerCharacter: c.isPlayerCharacter,
        isVisibleToPlayers: c.isVisibleToPlayers,
        assignedPlayerUserId: c.assignedPlayerUserId,
        conditions: c.conditions,
      }));

      const dmProjection: LiveCombatDmProjectionDto = {
        partyId: raw.partyId,
        partyName: raw.partyName,
        status: raw.status,
        currentRound: raw.currentRound,
        currentTurnIndex: raw.currentTurnIndex,
        activeCombatantId: activeCombatant?.id ?? null,
        activeCombatantName: activeCombatant?.name ?? null,
        combatants: dmCombatants,
      };

      this.callbacks.onCombatStateUpdated(dmProjection);
    } else {
      // Proyección censurada para Jugadores
      const isVisibleMonsterOrHero = (c: RawCombatant) => c.isPlayerCharacter || c.isVisibleToPlayers;

      const playerCombatants: LiveCombatantPlayerDto[] = raw.combatants
        .filter(isVisibleMonsterOrHero)
        .map((c) => {
          const hp = c.isPlayerCharacter ? c.currentHp : null;
          const maxHp = c.isPlayerCharacter ? c.maxHp : null;
          const healthStatus =
            c.currentHp <= 0 ? 'Down' : c.currentHp <= c.maxHp / 2 ? 'Bloodied' : 'Healthy';

          return {
            id: c.id,
            name: c.name,
            initiative: c.initiative,
            isPlayerCharacter: c.isPlayerCharacter,
            currentHp: hp,
            maxHp: maxHp,
            healthStatus,
            conditions: c.conditions,
          };
        });

      let activeName = activeCombatant?.name ?? null;
      let activeId = activeCombatant?.id ?? null;

      if (activeCombatant && !activeCombatant.isPlayerCharacter && !activeCombatant.isVisibleToPlayers) {
        activeName = 'Turno del Enemigo';
        activeId = null;
      }

      const playerProjection: LiveCombatPlayerProjectionDto = {
        partyId: raw.partyId,
        partyName: raw.partyName,
        status: raw.status,
        currentRound: raw.currentRound,
        currentTurnIndex: raw.currentTurnIndex,
        activeCombatantId: activeId,
        activeCombatantName: activeName,
        combatants: playerCombatants,
      };

      this.callbacks.onCombatStateUpdated(playerProjection);
    }
  }
}

export const liveCombatHubService = new LiveCombatHubService();

if (typeof window !== 'undefined') {
  (window as any).__liveCombatHubService = liveCombatHubService;
}
