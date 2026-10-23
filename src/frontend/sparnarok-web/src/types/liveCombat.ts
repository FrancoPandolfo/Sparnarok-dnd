/**
 * Estados del flujo de vida de un combate activo en vivo.
 */
export type LiveCombatStatus = 'NotStarted' | 'WaitingForInitiative' | 'InProgress' | 'Ended';

/**
 * Rol del usuario en la sesión de la Party.
 */
export type PartyRole = 'DungeonMaster' | 'Player';

/**
 * Combatiente proyectado para el Dungeon Master (visión completa sin censura).
 */
export interface LiveCombatantDmDto {
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
  sortOrder?: number;
}

/**
 * Combatiente proyectado para Jugadores (visión censurada anti-metagaming).
 */
export interface LiveCombatantPlayerDto {
  id: string;
  name: string;
  initiative: number | null;
  isPlayerCharacter: boolean;
  currentHp?: number | null;
  maxHp?: number | null;
  healthStatus: string;
  conditions?: string | null;
  sortOrder?: number;
}

/**
 * Proyección completa de combate para el Dungeon Master.
 */
export interface LiveCombatDmProjectionDto {
  partyId: string;
  partyName: string;
  status: LiveCombatStatus;
  currentRound: number;
  currentTurnIndex: number;
  activeCombatantId: string | null;
  activeCombatantName: string | null;
  combatants: LiveCombatantDmDto[];
}

/**
 * Proyección censurada de combate para Jugadores.
 */
export interface LiveCombatPlayerProjectionDto {
  partyId: string;
  partyName: string;
  status: LiveCombatStatus;
  currentRound: number;
  currentTurnIndex: number;
  activeCombatantId: string | null;
  activeCombatantName: string | null;
  combatants: LiveCombatantPlayerDto[];
}

/**
 * DTO para inicializar un combatiente en vivo.
 */
export interface InitializeLiveCombatantRequest {
  id: string;
  name: string;
  maxHp: number;
  armorClass: number;
  isPlayerCharacter: boolean;
  isVisibleToPlayers?: boolean;
  assignedPlayerUserId?: string | null;
  initiative?: number | null;
}

/**
 * Solicitud para inicializar un combate en vivo.
 */
export interface InitializeLiveCombatRequest {
  partyName: string;
  combatants: InitializeLiveCombatantRequest[];
}

/**
 * DTO de respuesta para una nota de sesión.
 */
export interface SessionNoteResponseDto {
  id: string;
  partyId: string;
  ownerUserId?: string;
  title: string;
  content: string;
  createdAt: string;
  updatedAt: string;
}

/**
 * Solicitud de creación de nota de sesión.
 */
export interface CreateSessionNoteRequest {
  title: string;
  content: string;
}

/**
 * Solicitud de actualización de nota de sesión.
 */
export interface UpdateSessionNoteRequest {
  title: string;
  content: string;
}

/**
 * Solicitud para registrar un combatiente rápido en la iniciativa en caliente.
 */
export interface AddQuickCombatantRequest {
  name: string;
  maxHp: number;
  armorClass: number;
  isPlayerCharacter?: boolean;
  isVisibleToPlayers?: boolean;
  initiative?: number | null;
}

