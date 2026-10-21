/**
 * Categorías canónicas de tamaño para criaturas según las reglas de D&D 5e (2024).
 */
export type CreatureSize = 'Tiny' | 'Small' | 'Medium' | 'Large' | 'Huge' | 'Gargantuan';

/**
 * Tipologías taxonómicas y mágicas oficiales en D&D 5e.
 */
export type CreatureType =
  | 'Aberration'
  | 'Beast'
  | 'Celestial'
  | 'Construct'
  | 'Dragon'
  | 'Elemental'
  | 'Fey'
  | 'Fiend'
  | 'Giant'
  | 'Humanoid'
  | 'Monstrosity'
  | 'Ooze'
  | 'Plant'
  | 'Undead';

/**
 * Clasificación de la acción dentro de la economía de turnos de combate.
 */
export type ActionType =
  | 'Action'
  | 'BonusAction'
  | 'Reaction'
  | 'LegendaryAction'
  | 'LairAction'
  | 'Special';

/**
 * Tipos de daño elementales y físicos del sistema de combate.
 */
export type DamageType =
  | 'Acid'
  | 'Bludgeoning'
  | 'Cold'
  | 'Fire'
  | 'Force'
  | 'Lightning'
  | 'Necrotic'
  | 'Piercing'
  | 'Poison'
  | 'Psychic'
  | 'Radiant'
  | 'Slashing'
  | 'Thunder';

/**
 * Las seis características principales del sistema D20.
 */
export type AbilityScoreType =
  | 'Strength'
  | 'Dexterity'
  | 'Constitution'
  | 'Intelligence'
  | 'Wisdom'
  | 'Charisma';

/**
 * Las 8 propiedades canónicas de Maestría de Armas introducidas en las reglas D&D 5e (2024).
 */
export type MasteryProperty =
  | 'Cleave'
  | 'Graze'
  | 'Nick'
  | 'Push'
  | 'Sap'
  | 'Slow'
  | 'Topple'
  | 'Vex';

/**
 * DTO que representa las seis características con sus modificadores calculados.
 */
export interface AbilityScoresDto {
  strength: number;
  strengthModifier: number;
  dexterity: number;
  dexterityModifier: number;
  constitution: number;
  constitutionModifier: number;
  intelligence: number;
  intelligenceModifier: number;
  wisdom: number;
  wisdomModifier: number;
  charisma: number;
  charismaModifier: number;
}

/**
 * DTO con el estado de puntos de vida de la criatura.
 */
export interface HitPointsDto {
  max: number;
  current: number;
  temporary: number;
}

/**
 * DTO con la configuración de armadura y defensas.
 */
export interface ArmorClassDto {
  base: number;
  shieldBonus: number;
  allowsDexterityModifier: boolean;
  maxDexterityModifier: number | null;
}

/**
 * DTO de respuesta para una propiedad de Maestría de Armas.
 */
export interface WeaponMasteryResponseDto {
  id: string;
  property: MasteryProperty;
  name: string;
  prerequisiteWeapon: string;
  description: string;
}

/**
 * DTO de respuesta para una acción o ataque táctico.
 */
export interface ActionResponseDto {
  id: string;
  creatureId: string;
  name: string;
  type: ActionType;
  description: string;
  isAttack: boolean;
  attackBonus: number | null;
  reachOrRangeFeet: number;
  damageFormula: string;
  damageType: DamageType | null;
  attackModifierAbility: AbilityScoreType | null;
  associatedMasteryId: string | null;
  associatedMastery?: WeaponMasteryResponseDto | null;
}

/**
 * DTO de respuesta integral de una criatura con su grafo táctico completo.
 */
export interface CreatureResponseDto {
  id: string;
  name: string;
  size: CreatureSize;
  type: CreatureType;
  alignment: string;
  challengeRating: number;
  speedFeet: number;
  proficiencyBonus: number;
  effectiveArmorClass: number;
  abilityScores: AbilityScoresDto;
  hitPoints: HitPointsDto;
  armorClass: ArmorClassDto;
  actions: ActionResponseDto[];
  knownMasteries: WeaponMasteryResponseDto[];
}

/**
 * Payload de creación de una nueva criatura en el Códice.
 */
export interface CreateCreatureRequestDto {
  name: string;
  size: CreatureSize;
  type: CreatureType;
  alignment: string;
  challengeRating: number;
  speedFeet: number;
  strength: number;
  dexterity: number;
  constitution: number;
  intelligence: number;
  wisdom: number;
  charisma: number;
  maxHitPoints: number;
  armorClassBase: number;
  shieldBonus: number;
  allowsDexterityModifier: boolean;
  maxDexterityModifier: number | null;
}

/**
 * Payload para incorporar una nueva acción táctica a una criatura.
 */
export interface CreateActionRequestDto {
  name: string;
  type: ActionType;
  description: string;
  isAttack: boolean;
  reachOrRangeFeet: number;
  damageFormula: string;
  damageType?: DamageType | null;
  attackModifierAbility?: AbilityScoreType | null;
  associatedMasteryId?: string | null;
}

/**
 * DTO que describe un arma canónica o importada en el Códice.
 */
export interface WeaponDto {
  id: string;
  name: string;
  damage: string;
  damageType: DamageType;
  properties: string;
  weight: number;
  cost: string;
}

/**
 * DTO con el resultado del proceso de ingesta masiva CSV.
 */
export interface IngestionResultDto {
  totalProcessed: number;
  inserted: number;
  updated: number;
  errors: string[];
}
