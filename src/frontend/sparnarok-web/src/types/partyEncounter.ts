import type { CreatureResponseDto } from './dnd';

/**
/// Solicitud de creación de una nueva Party de héroes.
*/
export interface CreatePartyRequestDto {
  name: string;
  memberCreatureIds: string[];
}

/**
/// Representación de una Party de héroes persistida.
*/
export interface PartyResponseDto {
  id: string;
  name: string;
  ownerUserId: string;
  createdAt: string;
  members: CreatureResponseDto[];
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
