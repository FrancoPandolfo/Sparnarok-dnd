import type { CreatureResponseDto } from './dnd';

/**
 * Miembro individual registrado en una Party con su rol y nombre contextual (Fase 3.7).
 */
export interface PartyMemberDto {
  id: string;
  partyId: string;
  userId: string;
  role: 'DungeonMaster' | 'Player';
  inGameName: string;
  joinedAt: string;
}

/**
 * Solicitud de incorporación a una Party mediante token de invitación (Fase 3.7 Onboarding).
 */
export interface JoinPartyRequestDto {
  role: 'DungeonMaster' | 'Player';
  inGameName: string;
}

/**
 * Detalles de invitación para la vista previa de onboarding (Fase 3.7).
 */
export interface PartyInviteDetailsDto {
  partyId: string;
  partyName: string;
  inviteToken: string;
  memberCount: number;
}

/**
 * Solicitud de creación de una nueva Party de héroes.
 */
export interface CreatePartyRequestDto {
  name: string;
  memberCreatureIds: string[];
  role?: 'DungeonMaster' | 'Player';
  inGameName?: string;
}

/**
 * Representación de una Party de héroes persistida.
 */
export interface PartyResponseDto {
  id: string;
  name: string;
  ownerUserId: string;
  inviteToken?: string;
  createdAt: string;
  members: CreatureResponseDto[];
  partyMembers?: PartyMemberDto[];
}

/**
/// Solicitud de creación de un nuevo Encuentro táctico.
*/
export interface CreateEncounterRequestDto {
  name: string;
  difficultyNotes?: string;
  creatureIds: string[];
}

/**
/// Representación de un Encuentro táctico persistido con sus monstruos.
*/
export interface EncounterResponseDto {
  id: string;
  name: string;
  ownerUserId: string;
  difficultyNotes?: string;
  createdAt: string;
  creatures: CreatureResponseDto[];
}
