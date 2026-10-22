/**
 * DTOs e interfaces para la gestión del inventario compartido (Collaborative Party Stash)
 * y la integración con la API de referencia de D&D 5e (SRD).
 */

export interface ReferenceItemSummaryDto {
  index: string;
  name: string;
  url: string;
}

export interface ReferenceEquipmentDetailDto {
  index: string;
  name: string;
  weight: number;
  description: string;
  rarity: string;
  itemType: string;
  isOfficialSrd: boolean;
  cost?: string | null;
}

export type ReferenceItemDetailDto = ReferenceEquipmentDetailDto;

export interface StashItemDto {
  id: string;
  partyId: string;
  name: string;
  quantity: number;
  weight: number;
  description: string;
  rarity: string;
  itemType: string;
  isOfficialSrd: boolean;
  referenceIndex?: string | null;
  assignedCreatureId?: string | null;
  createdAt: string;
}

export interface CreateStashItemRequestDto {
  name: string;
  quantity: number;
  weight: number;
  description?: string;
  rarity?: string;
  itemType?: string;
  isOfficialSrd?: boolean;
  referenceIndex?: string | null;
  assignedCreatureId?: string | null;
}

export interface PartyStashDto {
  partyId: string;
  partyName: string;
  totalWeight: number;
  items: StashItemDto[];
}
