/**
 * DTOs e interfaces para la gestión del inventario compartido (Collaborative Party Stash),
 * fondos monetarios (Party Fund), fabricación en descanso (Downtime Crafting)
 * y el historial de auditoría de actividad (Stash Activity Log).
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

export interface PartyFundDto {
  partyId: string;
  copperPieces: number;
  silverPieces: number;
  electrumPieces: number;
  goldPieces: number;
  platinumPieces: number;
  totalGoldEquivalent: number;
}

export interface UpdatePartyFundsRequestDto {
  copperPieces: number;
  silverPieces: number;
  electrumPieces: number;
  goldPieces: number;
  platinumPieces: number;
}

export interface CraftingQueueItemDto {
  id: string;
  partyId: string;
  itemName: string;
  itemType: string;
  rarity: string;
  goldCost: number;
  daysRequired: number;
  daysRemaining: number;
  weight: number;
  description: string;
  isOfficialSrd: boolean;
  referenceIndex?: string | null;
  crafterName?: string | null;
  status: 'InProgress' | 'Completed' | 'Cancelled' | string;
  createdAtUtc: string;
}

export interface QueueCraftingItemRequestDto {
  itemName: string;
  itemType?: string;
  rarity?: string;
  goldCost: number;
  daysRequired: number;
  weight: number;
  description?: string;
  isOfficialSrd?: boolean;
  referenceIndex?: string | null;
  crafterName?: string | null;
}

export interface StashActivityLogDto {
  id: string;
  partyId: string;
  userId?: string | null;
  userDisplayName: string;
  actionType: string;
  description: string;
  timestampUtc: string;
}

export interface DowntimeAdvanceResultDto {
  daysAdvanced: number;
  completedItemsCount: number;
  activeItemsCount: number;
  completedItemNames: string[];
  updatedQueue: CraftingQueueItemDto[];
  updatedStash: StashItemDto[];
}

export interface PartyStashDto {
  partyId: string;
  partyName: string;
  totalWeight: number;
  items: StashItemDto[];
  funds?: PartyFundDto | null;
  craftingQueue?: CraftingQueueItemDto[] | null;
}
