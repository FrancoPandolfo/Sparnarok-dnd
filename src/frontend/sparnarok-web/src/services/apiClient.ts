import type {
  CreatureResponseDto,
  CreateCreatureRequestDto,
  WeaponMasteryResponseDto,
  CreateActionRequestDto,
  ActionResponseDto,
  IngestionResultDto,
  WeaponDto,
  ArmorDto,
  EquipItemsRequestDto,
  CreateCustomHeroRequestDto,
  MontecarloRequestDto,
  MontecarloResultDto,
  DprBaselineDto,
} from '../types/dnd';
import type { AuthResponseDto } from '../types/auth';
import type {
  CreatePartyRequestDto,
  PartyResponseDto,
  CreateEncounterRequestDto,
  EncounterResponseDto,
} from '../types/partyEncounter';
import type {
  ReferenceItemSummaryDto,
  ReferenceEquipmentDetailDto,
  StashItemDto,
  CreateStashItemRequestDto,
  PartyFundDto,
  UpdatePartyFundsRequestDto,
  CraftingQueueItemDto,
  QueueCraftingItemRequestDto,
  StashActivityLogDto,
  DowntimeAdvanceResultDto,
  PartyStashDto,
} from '../types/stash';
import type {
  SessionNoteResponseDto,
  CreateSessionNoteRequest,
  UpdateSessionNoteRequest,
} from '../types/liveCombat';

const API_BASE_URL = '/api/v1';

let currentAuthToken: string | null = null;
let inMemorySessionNotes: SessionNoteResponseDto[] = [];

function getAuthHeaders(additionalHeaders: Record<string, string> = {}): Record<string, string> {
  const headers: Record<string, string> = { ...additionalHeaders };
  if (currentAuthToken) {
    headers['Authorization'] = `Bearer ${currentAuthToken}`;
  }
  return headers;
}

/**
 * Catálogo canónico de las 8 Maestrías de Armas (D&D 5e - 2024).
 */
export const CANONICAL_MASTERIES: WeaponMasteryResponseDto[] = [
  {
    id: 'a0000001-0000-0000-0000-000000000001',
    property: 'Cleave',
    name: 'Cleave',
    prerequisiteWeapon: 'Arma pesada o versátil cuerpo a cuerpo',
    description: 'Si impactas a una criatura, puedes realizar un ataque adicional contra otra criatura a 5 pies de la primera.',
  },
  {
    id: 'a0000001-0000-0000-0000-000000000002',
    property: 'Graze',
    name: 'Graze',
    prerequisiteWeapon: 'Arma pesada cuerpo a cuerpo',
    description: 'Si fallas la tirada de ataque, infliges daño igual al modificador de la característica utilizada.',
  },
  {
    id: 'a0000001-0000-0000-0000-000000000003',
    property: 'Nick',
    name: 'Nick',
    prerequisiteWeapon: 'Arma sutil cuerpo a cuerpo',
    description: 'Permite realizar el ataque adicional con tu arma secundaria como parte de la acción de Ataque.',
  },
  {
    id: 'a0000001-0000-0000-0000-000000000004',
    property: 'Push',
    name: 'Push',
    prerequisiteWeapon: 'Arma pesada o a dos manos',
    description: 'Si impactas, puedes empujar a la criatura hasta 10 pies en línea recta si es de tamaño Grande o menor.',
  },
  {
    id: 'a0000001-0000-0000-0000-000000000005',
    property: 'Sap',
    name: 'Sap',
    prerequisiteWeapon: 'Arma versátil o marcial',
    description: 'Si impactas, el objetivo sufre desventaja en su próxima tirada de ataque antes del inicio de tu próximo turno.',
  },
  {
    id: 'a0000001-0000-0000-0000-000000000006',
    property: 'Slow',
    name: 'Slow',
    prerequisiteWeapon: 'Cualquier arma con daño contundente o cortante',
    description: 'Si impactas e infliges daño, reduces la velocidad del objetivo en 10 pies hasta el inicio de tu próximo turno.',
  },
  {
    id: 'a0000001-0000-0000-0000-000000000007',
    property: 'Topple',
    name: 'Topple',
    prerequisiteWeapon: 'Arma pesada o de asta',
    description: 'Si impactas, obligas al objetivo a superar una tirada de salvación de Constitución (CD 8 + PB + Mod) o quedar derribado.',
  },
  {
    id: 'a0000001-0000-0000-0000-000000000008',
    property: 'Vex',
    name: 'Vex',
    prerequisiteWeapon: 'Arma sutil o marcial',
    description: 'Si impactas e infliges daño, obtienes ventaja en tu próxima tirada de ataque contra ese objetivo antes del fin de tu siguiente turno.',
  },
];

/**
 * Criaturas canónicas para inicializar el almacén local y pruebas tácticas.
 */
export const INITIAL_CREATURES: CreatureResponseDto[] = [
  {
    id: 'b0000001-0000-0000-0000-000000000001',
    name: 'Bugbear Stalker',
    size: 'Medium',
    type: 'Humanoid',
    alignment: 'Chaotic Evil',
    challengeRating: 1,
    speedFeet: 30,
    proficiencyBonus: 2,
    effectiveArmorClass: 16,
    abilityScores: {
      strength: 15,
      strengthModifier: 2,
      dexterity: 14,
      dexterityModifier: 2,
      constitution: 13,
      constitutionModifier: 1,
      intelligence: 8,
      intelligenceModifier: -1,
      wisdom: 11,
      wisdomModifier: 0,
      charisma: 9,
      charismaModifier: -1,
    },
    hitPoints: { max: 27, current: 27, temporary: 0 },
    armorClass: { base: 14, shieldBonus: 2, allowsDexterityModifier: false, maxDexterityModifier: null },
    actions: [
      {
        id: 'c0000001-0000-0000-0000-000000000001',
        creatureId: 'b0000001-0000-0000-0000-000000000001',
        name: 'Morningstar Strike',
        type: 'Action',
        description: 'Ataque cuerpo a cuerpo contundente y punzante con largo alcance.',
        isAttack: true,
        attackBonus: 4,
        reachOrRangeFeet: 5,
        damageFormula: '2d8+2',
        damageType: 'Piercing',
        attackModifierAbility: 'Strength',
        associatedMasteryId: 'a0000001-0000-0000-0000-000000000005',
        associatedMastery: CANONICAL_MASTERIES.find((m) => m.property === 'Sap') || null,
      },
    ],
    knownMasteries: [CANONICAL_MASTERIES.find((m) => m.property === 'Sap')!],
  },
  {
    id: 'b0000001-0000-0000-0000-000000000002',
    name: 'Gorgon Vanguard',
    size: 'Large',
    type: 'Monstrosity',
    alignment: 'Neutral Evil',
    challengeRating: 5,
    speedFeet: 30,
    proficiencyBonus: 3,
    effectiveArmorClass: 18,
    abilityScores: {
      strength: 18,
      strengthModifier: 4,
      dexterity: 14,
      dexterityModifier: 2,
      constitution: 16,
      constitutionModifier: 3,
      intelligence: 10,
      intelligenceModifier: 0,
      wisdom: 12,
      wisdomModifier: 1,
      charisma: 8,
      charismaModifier: -1,
    },
    hitPoints: { max: 45, current: 45, temporary: 5 },
    armorClass: { base: 16, shieldBonus: 2, allowsDexterityModifier: false, maxDexterityModifier: null },
    actions: [
      {
        id: 'c0000001-0000-0000-0000-000000000002',
        creatureId: 'b0000001-0000-0000-0000-000000000002',
        name: 'Iron Gore Strike',
        type: 'Action',
        description: 'Embiste con cuernos de hierro capaces de derribar objetivos grandes.',
        isAttack: true,
        attackBonus: 7,
        reachOrRangeFeet: 5,
        damageFormula: '2d8+4',
        damageType: 'Piercing',
        attackModifierAbility: 'Strength',
        associatedMasteryId: 'a0000001-0000-0000-0000-000000000007',
        associatedMastery: CANONICAL_MASTERIES.find((m) => m.property === 'Topple') || null,
      },
    ],
    knownMasteries: [CANONICAL_MASTERIES.find((m) => m.property === 'Topple')!],
  },
  {
    id: 'b0000001-0000-0000-0000-000000000003',
    name: 'Drow Duelist',
    size: 'Medium',
    type: 'Humanoid',
    alignment: 'Neutral Evil',
    challengeRating: 2,
    speedFeet: 30,
    proficiencyBonus: 2,
    effectiveArmorClass: 15,
    abilityScores: {
      strength: 10,
      strengthModifier: 0,
      dexterity: 16,
      dexterityModifier: 3,
      constitution: 12,
      constitutionModifier: 1,
      intelligence: 11,
      intelligenceModifier: 0,
      wisdom: 13,
      wisdomModifier: 1,
      charisma: 12,
      charismaModifier: 1,
    },
    hitPoints: { max: 33, current: 33, temporary: 0 },
    armorClass: { base: 12, shieldBonus: 0, allowsDexterityModifier: true, maxDexterityModifier: null },
    actions: [
      {
        id: 'c0000001-0000-0000-0000-000000000003',
        creatureId: 'b0000001-0000-0000-0000-000000000003',
        name: 'Rapier Thrust',
        type: 'Action',
        description: 'Estocada rápida de precisión quirúrgica.',
        isAttack: true,
        attackBonus: 5,
        reachOrRangeFeet: 5,
        damageFormula: '1d8+3',
        damageType: 'Piercing',
        attackModifierAbility: 'Dexterity',
        associatedMasteryId: 'a0000001-0000-0000-0000-000000000008',
        associatedMastery: CANONICAL_MASTERIES.find((m) => m.property === 'Vex') || null,
      },
    ],
    knownMasteries: [CANONICAL_MASTERIES.find((m) => m.property === 'Vex')!],
  },
];

export const CANONICAL_WEAPONS: WeaponDto[] = [
  {
    id: 'w0000001-0000-0000-0000-000000000001',
    name: 'Longsword',
    damage: '1d8',
    damageType: 'Slashing',
    properties: 'Versatile (1d10)',
    weight: 3,
    cost: '15 GP',
  },
  {
    id: 'w0000001-0000-0000-0000-000000000002',
    name: 'Halberd',
    damage: '1d10',
    damageType: 'Slashing',
    properties: 'Heavy, Reach, Two-Handed',
    weight: 6,
    cost: '20 GP',
  },
  {
    id: 'w0000001-0000-0000-0000-000000000003',
    name: 'Rapier',
    damage: '1d8',
    damageType: 'Piercing',
    properties: 'Finesse',
    weight: 2,
    cost: '25 GP',
  },
  {
    id: 'w0000001-0000-0000-0000-000000000004',
    name: 'Greatsword',
    damage: '2d6',
    damageType: 'Slashing',
    properties: 'Heavy, Two-Handed',
    weight: 6,
    cost: '50 GP',
  },
  {
    id: 'w0000001-0000-0000-0000-000000000005',
    name: 'Dagger',
    damage: '1d4',
    damageType: 'Piercing',
    properties: 'Finesse, Light, Thrown (Range 20/60)',
    weight: 1,
    cost: '2 GP',
  },
];

export const CANONICAL_ARMORS: ArmorDto[] = [
  {
    id: 'c0000001-0000-0000-0000-000000000001',
    name: 'Leather Armor',
    category: 'Light',
    rawArmorClass: '11 + Dex modifier',
    baseArmorClass: 11,
    shieldBonus: 0,
    allowsDexterityModifier: true,
    maxDexterityModifier: null,
    strengthRequirement: null,
    stealthDisadvantage: false,
    weight: 10,
    cost: '10 GP',
  },
  {
    id: 'c0000001-0000-0000-0000-000000000002',
    name: 'Half Plate Armor',
    category: 'Medium',
    rawArmorClass: '15 + Dex modifier (max 2)',
    baseArmorClass: 15,
    shieldBonus: 0,
    allowsDexterityModifier: true,
    maxDexterityModifier: 2,
    strengthRequirement: null,
    stealthDisadvantage: true,
    weight: 40,
    cost: '750 GP',
  },
  {
    id: 'c0000001-0000-0000-0000-000000000003',
    name: 'Plate Armor',
    category: 'Heavy',
    rawArmorClass: '18',
    baseArmorClass: 18,
    shieldBonus: 0,
    allowsDexterityModifier: false,
    maxDexterityModifier: null,
    strengthRequirement: 15,
    stealthDisadvantage: true,
    weight: 65,
    cost: '1,500 GP',
  },
  {
    id: 'c0000001-0000-0000-0000-000000000004',
    name: 'Shield',
    category: 'Shield',
    rawArmorClass: '+2',
    baseArmorClass: 0,
    shieldBonus: 2,
    allowsDexterityModifier: false,
    maxDexterityModifier: null,
    strengthRequirement: null,
    stealthDisadvantage: false,
    weight: 6,
    cost: '10 GP',
  },
];

export const CANONICAL_DPR_BASELINES: DprBaselineDto[] = [
  // Fighter 2H
  { archetype: 'Fighter 2H', level: 1, expectedDpr: 9.2, description: 'Greatsword con Great Weapon Fighting' },
  { archetype: 'Fighter 2H', level: 2, expectedDpr: 10.8, description: 'Action Surge prorrateado' },
  { archetype: 'Fighter 2H', level: 3, expectedDpr: 12.5, description: 'Subclase Champion/Battle Master' },
  { archetype: 'Fighter 2H', level: 4, expectedDpr: 14.1, description: 'ASI Fuerza 18 o Dote' },
  { archetype: 'Fighter 2H', level: 5, expectedDpr: 19.4, description: 'Ataque Extra (2 ataques por turno)' },
  { archetype: 'Fighter 2H', level: 6, expectedDpr: 21.0, description: 'ASI Fuerza 20' },
  { archetype: 'Fighter 2H', level: 7, expectedDpr: 22.8, description: 'Habilidad de subclase nivel 7' },
  { archetype: 'Fighter 2H', level: 8, expectedDpr: 24.5, description: 'Dote de maestría avanzada' },
  { archetype: 'Fighter 2H', level: 9, expectedDpr: 25.6, description: 'Indomable y bono de competencia +4' },
  { archetype: 'Fighter 2H', level: 10, expectedDpr: 27.2, description: 'Mejora táctica de combate' },
  { archetype: 'Fighter 2H', level: 11, expectedDpr: 34.8, description: 'Ataque Extra (3 ataques por turno)' },
  { archetype: 'Fighter 2H', level: 12, expectedDpr: 36.5, description: 'Dote épica adicional' },
  { archetype: 'Fighter 2H', level: 13, expectedDpr: 38.2, description: 'Bono de competencia +5' },
  { archetype: 'Fighter 2H', level: 14, expectedDpr: 40.0, description: 'ASI / Dote' },
  { archetype: 'Fighter 2H', level: 15, expectedDpr: 42.1, description: 'Subclase avanzada' },
  { archetype: 'Fighter 2H', level: 16, expectedDpr: 44.2, description: 'ASI / Dote' },
  { archetype: 'Fighter 2H', level: 17, expectedDpr: 48.0, description: 'Segundo Action Surge' },
  { archetype: 'Fighter 2H', level: 18, expectedDpr: 50.5, description: 'Subclase capstone' },
  { archetype: 'Fighter 2H', level: 19, expectedDpr: 53.2, description: 'Dote Épica nivel 19' },
  { archetype: 'Fighter 2H', level: 20, expectedDpr: 62.0, description: 'Ataque Extra (4 ataques por turno)' },
  // Ranger Ranged
  { archetype: 'Ranger Ranged', level: 1, expectedDpr: 7.5, description: 'Arco largo 1d8+3' },
  { archetype: 'Ranger Ranged', level: 2, expectedDpr: 9.8, description: "Estilo Archery + Hunter's Mark" },
  { archetype: 'Ranger Ranged', level: 3, expectedDpr: 11.5, description: 'Subclase Gloom Stalker' },
  { archetype: 'Ranger Ranged', level: 4, expectedDpr: 13.0, description: 'ASI Destreza 18' },
  { archetype: 'Ranger Ranged', level: 5, expectedDpr: 18.2, description: "Ataque Extra + Hunter's Mark" },
  { archetype: 'Ranger Ranged', level: 6, expectedDpr: 19.5, description: 'Mejoras tácticas' },
  { archetype: 'Ranger Ranged', level: 7, expectedDpr: 21.0, description: 'Subclase nivel 7' },
  { archetype: 'Ranger Ranged', level: 8, expectedDpr: 22.5, description: 'ASI Destreza 20' },
  { archetype: 'Ranger Ranged', level: 9, expectedDpr: 24.0, description: 'Conjuros de nivel 3' },
  { archetype: 'Ranger Ranged', level: 10, expectedDpr: 25.5, description: 'Naturaleza infatigable' },
  { archetype: 'Ranger Ranged', level: 11, expectedDpr: 29.0, description: 'Mejora de daño por subclase' },
  { archetype: 'Ranger Ranged', level: 12, expectedDpr: 30.5, description: 'Dote' },
  { archetype: 'Ranger Ranged', level: 13, expectedDpr: 32.0, description: 'Conjuros de nivel 4' },
  { archetype: 'Ranger Ranged', level: 14, expectedDpr: 33.5, description: 'Habilidad táctica' },
  { archetype: 'Ranger Ranged', level: 15, expectedDpr: 35.0, description: 'Subclase nivel 15' },
  { archetype: 'Ranger Ranged', level: 16, expectedDpr: 37.0, description: 'ASI / Dote' },
  { archetype: 'Ranger Ranged', level: 17, expectedDpr: 40.0, description: 'Conjuros de nivel 5' },
  { archetype: 'Ranger Ranged', level: 18, expectedDpr: 42.0, description: 'Sentidos ferales' },
  { archetype: 'Ranger Ranged', level: 19, expectedDpr: 44.5, description: 'Dote épica' },
  { archetype: 'Ranger Ranged', level: 20, expectedDpr: 48.0, description: 'Foe Slayer 2024' },
  // Paladin 2H
  { archetype: 'Paladin 2H', level: 1, expectedDpr: 8.5, description: 'Espadón 2d6+3' },
  { archetype: 'Paladin 2H', level: 2, expectedDpr: 11.0, description: 'Divine Smite' },
  { archetype: 'Paladin 2H', level: 3, expectedDpr: 13.0, description: 'Canalizar divinidad' },
  { archetype: 'Paladin 2H', level: 4, expectedDpr: 14.5, description: 'ASI Fuerza 18' },
  { archetype: 'Paladin 2H', level: 5, expectedDpr: 20.5, description: 'Ataque Extra + Smite nivel 2' },
  { archetype: 'Paladin 2H', level: 6, expectedDpr: 21.8, description: 'Aura de protección' },
  { archetype: 'Paladin 2H', level: 7, expectedDpr: 23.2, description: 'Aura de subclase' },
  { archetype: 'Paladin 2H', level: 8, expectedDpr: 24.8, description: 'ASI Fuerza 20' },
  { archetype: 'Paladin 2H', level: 9, expectedDpr: 27.0, description: 'Smite nivel 3' },
  { archetype: 'Paladin 2H', level: 10, expectedDpr: 28.5, description: 'Aura de coraje' },
  { archetype: 'Paladin 2H', level: 11, expectedDpr: 36.0, description: 'Radiant Strikes' },
  { archetype: 'Paladin 2H', level: 12, expectedDpr: 37.8, description: 'Dote' },
  { archetype: 'Paladin 2H', level: 13, expectedDpr: 40.2, description: 'Smite nivel 4' },
  { archetype: 'Paladin 2H', level: 14, expectedDpr: 42.0, description: 'Toque purificador' },
  { archetype: 'Paladin 2H', level: 15, expectedDpr: 44.5, description: 'Subclase nivel 15' },
  { archetype: 'Paladin 2H', level: 16, expectedDpr: 46.8, description: 'ASI / Dote' },
  { archetype: 'Paladin 2H', level: 17, expectedDpr: 50.5, description: 'Smite nivel 5' },
  { archetype: 'Paladin 2H', level: 18, expectedDpr: 53.0, description: 'Rango de auras ampliado' },
  { archetype: 'Paladin 2H', level: 19, expectedDpr: 55.8, description: 'Dote épica' },
  { archetype: 'Paladin 2H', level: 20, expectedDpr: 60.0, description: 'Avatar sagrado' },
  // Barbarian 2H
  { archetype: 'Barbarian 2H', level: 1, expectedDpr: 9.8, description: 'Greataxe 1d12+3 + Furia' },
  { archetype: 'Barbarian 2H', level: 2, expectedDpr: 12.0, description: 'Reckless Attack' },
  { archetype: 'Barbarian 2H', level: 3, expectedDpr: 14.2, description: 'Subclase Berserker/Zealot' },
  { archetype: 'Barbarian 2H', level: 4, expectedDpr: 16.0, description: 'ASI Fuerza 18' },
  { archetype: 'Barbarian 2H', level: 5, expectedDpr: 22.0, description: 'Ataque Extra + Furia + Reckless' },
  { archetype: 'Barbarian 2H', level: 6, expectedDpr: 23.5, description: 'Subclase nivel 6' },
  { archetype: 'Barbarian 2H', level: 7, expectedDpr: 25.0, description: 'Instinto salvaje' },
  { archetype: 'Barbarian 2H', level: 8, expectedDpr: 26.8, description: 'ASI Fuerza 20' },
  { archetype: 'Barbarian 2H', level: 9, expectedDpr: 29.0, description: 'Furia (+3 daño) + Brutal Critical' },
  { archetype: 'Barbarian 2H', level: 10, expectedDpr: 30.8, description: 'Subclase nivel 10' },
  { archetype: 'Barbarian 2H', level: 11, expectedDpr: 34.5, description: 'Furia implacable' },
  { archetype: 'Barbarian 2H', level: 12, expectedDpr: 36.2, description: 'Dote' },
  { archetype: 'Barbarian 2H', level: 13, expectedDpr: 38.5, description: 'Brutal Strike 2024' },
  { archetype: 'Barbarian 2H', level: 14, expectedDpr: 40.8, description: 'Subclase nivel 14' },
  { archetype: 'Barbarian 2H', level: 15, expectedDpr: 43.0, description: 'Furia persistente' },
  { archetype: 'Barbarian 2H', level: 16, expectedDpr: 45.5, description: 'Furia (+4 daño)' },
  { archetype: 'Barbarian 2H', level: 17, expectedDpr: 49.0, description: 'Brutal Critical x3' },
  { archetype: 'Barbarian 2H', level: 18, expectedDpr: 51.5, description: 'Poderío indómito' },
  { archetype: 'Barbarian 2H', level: 19, expectedDpr: 54.2, description: 'Dote épica' },
  { archetype: 'Barbarian 2H', level: 20, expectedDpr: 64.0, description: 'Primal Champion (+4 STR/CON)' },
  // Rogue Sneak Attack
  { archetype: 'Rogue Sneak Attack', level: 1, expectedDpr: 7.2, description: 'Estoque 1d8+3 + 1d6 Sneak Attack' },
  { archetype: 'Rogue Sneak Attack', level: 2, expectedDpr: 8.5, description: 'Acción astuta' },
  { archetype: 'Rogue Sneak Attack', level: 3, expectedDpr: 11.2, description: 'Subclase Assassin + 2d6 Sneak' },
  { archetype: 'Rogue Sneak Attack', level: 4, expectedDpr: 12.5, description: 'ASI Destreza 18' },
  { archetype: 'Rogue Sneak Attack', level: 5, expectedDpr: 16.8, description: '3d6 Sneak Attack + Uncanny Dodge' },
  { archetype: 'Rogue Sneak Attack', level: 6, expectedDpr: 18.0, description: 'Pericia adicional' },
  { archetype: 'Rogue Sneak Attack', level: 7, expectedDpr: 21.0, description: '4d6 Sneak Attack + Evasión' },
  { archetype: 'Rogue Sneak Attack', level: 8, expectedDpr: 22.5, description: 'ASI Destreza 20' },
  { archetype: 'Rogue Sneak Attack', level: 9, expectedDpr: 25.5, description: '5d6 Sneak Attack' },
  { archetype: 'Rogue Sneak Attack', level: 10, expectedDpr: 27.0, description: 'Dote adicional' },
  { archetype: 'Rogue Sneak Attack', level: 11, expectedDpr: 30.5, description: '6d6 Sneak Attack + Talento' },
  { archetype: 'Rogue Sneak Attack', level: 12, expectedDpr: 32.0, description: 'Dote' },
  { archetype: 'Rogue Sneak Attack', level: 13, expectedDpr: 35.5, description: '7d6 Sneak Attack' },
  { archetype: 'Rogue Sneak Attack', level: 14, expectedDpr: 37.0, description: 'Sentido ciego' },
  { archetype: 'Rogue Sneak Attack', level: 15, expectedDpr: 40.5, description: '8d6 Sneak Attack' },
  { archetype: 'Rogue Sneak Attack', level: 16, expectedDpr: 42.0, description: 'ASI / Dote' },
  { archetype: 'Rogue Sneak Attack', level: 17, expectedDpr: 46.0, description: '9d6 Sneak Attack + Golpe mortal' },
  { archetype: 'Rogue Sneak Attack', level: 18, expectedDpr: 48.0, description: 'Elusivo' },
  { archetype: 'Rogue Sneak Attack', level: 19, expectedDpr: 51.5, description: '10d6 Sneak Attack + Dote épica' },
  { archetype: 'Rogue Sneak Attack', level: 20, expectedDpr: 55.0, description: 'Golpe de suerte' },
  // Warlock Eldritch Blast
  { archetype: 'Warlock Eldritch Blast', level: 1, expectedDpr: 5.5, description: 'Eldritch Blast 1d10' },
  { archetype: 'Warlock Eldritch Blast', level: 2, expectedDpr: 8.5, description: 'Agonizing Blast (+Carisma)' },
  { archetype: 'Warlock Eldritch Blast', level: 3, expectedDpr: 9.2, description: 'Pacto y conjuros nivel 2' },
  { archetype: 'Warlock Eldritch Blast', level: 4, expectedDpr: 10.5, description: 'ASI Carisma 18' },
  { archetype: 'Warlock Eldritch Blast', level: 5, expectedDpr: 16.5, description: '2 rayos Eldritch Blast (Treantmonk)' },
  { archetype: 'Warlock Eldritch Blast', level: 6, expectedDpr: 17.5, description: 'Subclase patrón' },
  { archetype: 'Warlock Eldritch Blast', level: 7, expectedDpr: 18.5, description: 'Conjuros nivel 4' },
  { archetype: 'Warlock Eldritch Blast', level: 8, expectedDpr: 20.0, description: 'ASI Carisma 20' },
  { archetype: 'Warlock Eldritch Blast', level: 9, expectedDpr: 21.0, description: 'Invocación mística' },
  { archetype: 'Warlock Eldritch Blast', level: 10, expectedDpr: 22.0, description: 'Protección de patrón' },
  { archetype: 'Warlock Eldritch Blast', level: 11, expectedDpr: 28.5, description: '3 rayos Eldritch Blast' },
  { archetype: 'Warlock Eldritch Blast', level: 12, expectedDpr: 29.8, description: 'Dote' },
  { archetype: 'Warlock Eldritch Blast', level: 13, expectedDpr: 31.0, description: 'Arcanum nivel 7' },
  { archetype: 'Warlock Eldritch Blast', level: 14, expectedDpr: 32.5, description: 'Subclase capstone' },
  { archetype: 'Warlock Eldritch Blast', level: 15, expectedDpr: 33.8, description: 'Arcanum nivel 8' },
  { archetype: 'Warlock Eldritch Blast', level: 16, expectedDpr: 35.0, description: 'ASI / Dote' },
  { archetype: 'Warlock Eldritch Blast', level: 17, expectedDpr: 42.0, description: '4 rayos Eldritch Blast' },
  { archetype: 'Warlock Eldritch Blast', level: 18, expectedDpr: 43.5, description: 'Invocación nivel 18' },
  { archetype: 'Warlock Eldritch Blast', level: 19, expectedDpr: 45.0, description: 'Dote épica' },
  { archetype: 'Warlock Eldritch Blast', level: 20, expectedDpr: 47.0, description: 'Master Arcanist' },
];

export function calculateExpectedDpr(archetype: string, level: number): number {
  const match = CANONICAL_DPR_BASELINES.find(
    (b) => b.archetype.toLowerCase() === archetype.toLowerCase() && b.level === level
  );
  return match ? match.expectedDpr : 10.0;
}

// Almacén en memoria para cuando el backend no está disponible
let inMemoryCreatures: CreatureResponseDto[] = [...INITIAL_CREATURES];
let inMemoryParties: PartyResponseDto[] = [];
let inMemoryEncounters: EncounterResponseDto[] = [];
let inMemoryStashItems: StashItemDto[] = [];
let inMemoryPartyFunds: Record<string, PartyFundDto> = {};
let inMemoryCraftingQueue: CraftingQueueItemDto[] = [];
let inMemoryActivityLogs: StashActivityLogDto[] = [];


/**
 * Cliente de consumo HTTP para la Web API de Sparnarok.
 */
export const apiClient = {
  /**
   * Obtiene el token de autenticación JWT actual.
   */
  getAuthToken(): string | null {
    return currentAuthToken;
  },

  /**
   * Establece el token de autenticación JWT para peticiones protegidas.
   */
  setAuthToken(token: string | null): void {
    currentAuthToken = token;
  },

  /**
   * Inicia sesión intercambiando un ID Token de Google OAuth2 por un JWT propio del backend.
   */
  async loginWithGoogle(idToken: string): Promise<AuthResponseDto> {
    try {
      const response = await fetch('/api/auth/google', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ idToken }),
      });

      if (response.ok) {
        const data: AuthResponseDto = await response.json();
        this.setAuthToken(data.token);
        return data;
      } else {
        const errorText = await response.text();
        throw new Error(errorText || 'Error autenticando con Google');
      }
    } catch (err) {
      if (err instanceof Error && !err.message.includes('fetch') && !err.message.includes('Failed to fetch') && !err.message.includes('NetworkError')) {
        throw err;
      }
      // Fallback para entornos offline o de prueba sin backend activo
      const syntheticUser: AuthResponseDto = {
        token: `mock-jwt-${Date.now()}`,
        userId: '00000000-0000-0000-0000-000000000001',
        email: 'offline-adventurer@sparnarok.dnd',
        displayName: 'Aventurero del Códice',
        avatarUrl: null,
      };
      this.setAuthToken(syntheticUser.token);
      return syntheticUser;
    }
  },

  /**
   * Obtiene el perfil del usuario autenticado actual.
   */
  async getCurrentUser(signal?: AbortSignal): Promise<AuthResponseDto | null> {
    if (!currentAuthToken) return null;
    try {
      const response = await fetch('/api/auth/me', {
        headers: getAuthHeaders(),
        signal,
      });
      if (response.ok) {
        return await response.json();
      }
    } catch {
      // Fallback
    }
    return null;
  },

  /**
   * Obtiene la lista completa de criaturas desde el backend o almacén local.
   */
  async getCreatures(signal?: AbortSignal): Promise<CreatureResponseDto[]> {
    try {
      const response = await fetch(`${API_BASE_URL}/creatures`, {
        headers: getAuthHeaders(),
        signal,
      });
      if (response.ok) {
        const data = await response.json();
        if (Array.isArray(data) && data.length > 0) {
          inMemoryCreatures = data;
          return data;
        }
      }
    } catch {
      // Si la API falla o está offline, servimos la memoria local
    }
    return [...inMemoryCreatures];
  },

  /**
   * Obtiene una criatura por su identificador.
   */
  async getCreatureById(id: string, signal?: AbortSignal): Promise<CreatureResponseDto> {
    try {
      const response = await fetch(`${API_BASE_URL}/creatures/${id}`, {
        headers: getAuthHeaders(),
        signal,
      });
      if (response.ok) {
        return await response.json();
      }
    } catch {
      // Fallback local
    }
    const found = inMemoryCreatures.find((c) => c.id === id);
    if (!found) {
      throw new Error(`Criatura con ID ${id} no encontrada.`);
    }
    return found;
  },

  /**
   * Crea una nueva criatura en el Códice.
   */
  async createCreature(payload: CreateCreatureRequestDto): Promise<CreatureResponseDto> {
    try {
      const response = await fetch(`${API_BASE_URL}/creatures`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload),
      });
      if (response.ok) {
        const created: CreatureResponseDto = await response.json();
        inMemoryCreatures.unshift(created);
        return created;
      }
    } catch {
      // Fallback local
    }

    // Creación sintética local
    const strMod = Math.floor((payload.strength - 10) / 2);
    const dexMod = Math.floor((payload.dexterity - 10) / 2);
    const conMod = Math.floor((payload.constitution - 10) / 2);
    const intMod = Math.floor((payload.intelligence - 10) / 2);
    const wisMod = Math.floor((payload.wisdom - 10) / 2);
    const chaMod = Math.floor((payload.charisma - 10) / 2);

    const pb =
      payload.challengeRating < 5
        ? 2
        : payload.challengeRating < 9
          ? 3
          : payload.challengeRating < 13
            ? 4
            : 5;

    const dexContribution = payload.allowsDexterityModifier
      ? payload.maxDexterityModifier !== null
        ? Math.min(dexMod, payload.maxDexterityModifier)
        : dexMod
      : 0;

    const newCreature: CreatureResponseDto = {
      id: crypto.randomUUID(),
      name: payload.name,
      size: payload.size,
      type: payload.type,
      alignment: payload.alignment,
      challengeRating: payload.challengeRating,
      speedFeet: payload.speedFeet,
      proficiencyBonus: pb,
      effectiveArmorClass: payload.armorClassBase + payload.shieldBonus + dexContribution,
      abilityScores: {
        strength: payload.strength,
        strengthModifier: strMod,
        dexterity: payload.dexterity,
        dexterityModifier: dexMod,
        constitution: payload.constitution,
        constitutionModifier: conMod,
        intelligence: payload.intelligence,
        intelligenceModifier: intMod,
        wisdom: payload.wisdom,
        wisdomModifier: wisMod,
        charisma: payload.charisma,
        charismaModifier: chaMod,
      },
      hitPoints: {
        max: payload.maxHitPoints,
        current: payload.maxHitPoints,
        temporary: 0,
      },
      armorClass: {
        base: payload.armorClassBase,
        shieldBonus: payload.shieldBonus,
        allowsDexterityModifier: payload.allowsDexterityModifier,
        maxDexterityModifier: payload.maxDexterityModifier,
      },
      actions: [],
      knownMasteries: [],
    };

    inMemoryCreatures.unshift(newCreature);
    return newCreature;
  },

  /**
   * Elimina una criatura por ID.
   */
  async deleteCreature(id: string): Promise<void> {
    try {
      await fetch(`${API_BASE_URL}/creatures/${id}`, { method: 'DELETE' });
    } catch {
      // Ignorar error si offline
    }
    inMemoryCreatures = inMemoryCreatures.filter((c) => c.id !== id);
  },

  /**
   * Obtiene el catálogo de Maestrías de Armas.
   */
  async getWeaponMasteries(signal?: AbortSignal): Promise<WeaponMasteryResponseDto[]> {
    try {
      const response = await fetch(`${API_BASE_URL}/weaponmasteries`, { signal });
      if (response.ok) {
        return await response.json();
      }
    } catch {
      // Fallback
    }
    return CANONICAL_MASTERIES;
  },

  /**
   * Añade una acción táctica a una criatura.
   */
  async addAction(
    creatureId: string,
    payload: CreateActionRequestDto
  ): Promise<ActionResponseDto> {
    try {
      const response = await fetch(`${API_BASE_URL}/creatures/${creatureId}/actions`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload),
      });
      if (response.ok) {
        return await response.json();
      }
    } catch {
      // Fallback
    }

    const creature = inMemoryCreatures.find((c) => c.id === creatureId);
    const associatedMastery = payload.associatedMasteryId
      ? CANONICAL_MASTERIES.find((m) => m.id === payload.associatedMasteryId) || null
      : null;

    const action: ActionResponseDto = {
      id: crypto.randomUUID(),
      creatureId,
      name: payload.name,
      type: payload.type,
      description: payload.description,
      isAttack: payload.isAttack,
      attackBonus: payload.isAttack && creature ? creature.proficiencyBonus + 2 : null,
      reachOrRangeFeet: payload.reachOrRangeFeet,
      damageFormula: payload.damageFormula,
      damageType: payload.damageType ?? null,
      attackModifierAbility: payload.attackModifierAbility ?? null,
      associatedMasteryId: payload.associatedMasteryId ?? null,
      associatedMastery,
    };

    if (creature) {
      creature.actions.push(action);
    }
    return action;
  },

  /**
   * Enseña una maestría a una criatura.
   */
  async learnMastery(creatureId: string, masteryId: string): Promise<CreatureResponseDto> {
    try {
      const response = await fetch(`${API_BASE_URL}/creatures/${creatureId}/masteries/${masteryId}`, {
        method: 'POST',
      });
      if (response.ok) {
        return await response.json();
      }
    } catch {
      // Fallback
    }

    const creature = inMemoryCreatures.find((c) => c.id === creatureId);
    if (!creature) throw new Error('Criatura no encontrada');
    const mastery = CANONICAL_MASTERIES.find((m) => m.id === masteryId);
    if (mastery && !creature.knownMasteries.some((m) => m.id === masteryId)) {
      creature.knownMasteries.push(mastery);
    }
    return creature;
  },

  /**
   * Sube un archivo CSV de armas a la API para ingesta y reconciliación masiva (Bulk Upsert).
   */
  async uploadWeaponsCsv(file: File): Promise<IngestionResultDto> {
    const formData = new FormData();
    formData.append('file', file);

    try {
      const response = await fetch('/api/ingestion/weapons', {
        method: 'POST',
        body: formData,
      });

      if (response.ok) {
        return await response.json();
      }
    } catch {
      // Fallback local para pruebas y entornos offline
    }

    // Parseo sintético local del texto CSV
    const text = await file.text();
    const lines = text.split(/\r?\n/).filter((l) => l.trim().length > 0);
    const dataLines = lines.length > 1 ? lines.slice(1) : [];

    return {
      totalProcessed: dataLines.length,
      inserted: dataLines.length,
      updated: 0,
      errors: [],
    };
  },

  async getWeapons(signal?: AbortSignal): Promise<WeaponDto[]> {
    try {
      const response = await fetch('/api/ingestion/weapons', { signal });
      if (response.ok) {
        const data = await response.json();
        if (Array.isArray(data) && data.length > 0) {
          return data;
        }
      }
    } catch {
      // Fallback
    }
    return [...CANONICAL_WEAPONS];
  },

  /**
   * Sube masivamente un catálogo CSV de armaduras y escudos a través de multipart/form-data.
   */
  async uploadArmorsCsv(file: File): Promise<IngestionResultDto> {
    const formData = new FormData();
    formData.append('file', file);

    try {
      const response = await fetch('/api/ingestion/armors', {
        method: 'POST',
        body: formData,
      });

      if (response.ok) {
        return await response.json();
      }
    } catch {
      // Fallback local
    }

    const text = await file.text();
    const lines = text.split(/\r?\n/).filter((l) => l.trim().length > 0);
    const dataLines = lines.length > 1 ? lines.slice(1) : [];

    return {
      totalProcessed: dataLines.length,
      inserted: dataLines.length,
      updated: 0,
      errors: [],
    };
  },

  /**
   * Obtiene la lista completa de armaduras y escudos registrados.
   */
  async getArmors(signal?: AbortSignal): Promise<ArmorDto[]> {
    try {
      const response = await fetch('/api/ingestion/armors', { signal });
      if (response.ok) {
        const data = await response.json();
        if (Array.isArray(data) && data.length > 0) {
          return data;
        }
      }
    } catch {
      // Fallback
    }
    return [...CANONICAL_ARMORS];
  },

  /**
   * Equipa o desequipa armadura y/o escudo en una criatura existente.
   */
  async equipCreature(creatureId: string, request: EquipItemsRequestDto): Promise<CreatureResponseDto | null> {
    try {
      const response = await fetch(`/api/v1/creatures/${creatureId}/equip`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(request),
      });

      if (response.ok) {
        return await response.json();
      }
    } catch {
      // Fallback
    }

    const creature = inMemoryCreatures.find((c) => c.id === creatureId);
    if (!creature) return null;

    if (request.armorId !== undefined) {
      const armor = CANONICAL_ARMORS.find((a) => a.id === request.armorId);
      creature.equippedArmorId = armor ? armor.id : null;
      creature.equippedArmorName = armor ? armor.name : null;
    }
    if (request.shieldId !== undefined) {
      const shield = CANONICAL_ARMORS.find((s) => s.id === request.shieldId);
      creature.equippedShieldId = shield ? shield.id : null;
      creature.equippedShieldName = shield ? shield.name : null;
    }
    return { ...creature };
  },

  /**
   * Obtiene la matriz completa de líneas base de DPR por arquetipo y nivel.
   */
  async getDprBaselines(signal?: AbortSignal): Promise<DprBaselineDto[]> {
    try {
      const response = await fetch('/api/v1/dpr-baselines', { signal });
      if (response.ok) {
        const data = await response.json();
        if (Array.isArray(data) && data.length > 0) {
          return data;
        }
      }
    } catch {
      // Fallback
    }
    return [...CANONICAL_DPR_BASELINES];
  },

  /**
   * Obtiene la lista de arquetipos de daño soportados para PCs.
   */
  async getDprArchetypes(signal?: AbortSignal): Promise<string[]> {
    try {
      const response = await fetch('/api/v1/dpr-baselines/archetypes', { signal });
      if (response.ok) {
        const data = await response.json();
        if (Array.isArray(data) && data.length > 0) {
          return data;
        }
      }
    } catch {
      // Fallback
    }
    return Array.from(new Set(CANONICAL_DPR_BASELINES.map((b) => b.archetype)));
  },

  /**
   * Calcula el DPR esperado para un arquetipo y nivel dados.
   */
  async calculateDpr(archetype: string, level: number): Promise<number> {
    try {
      const response = await fetch(
        `/api/v1/dpr-baselines/calculate?archetype=${encodeURIComponent(archetype)}&level=${level}`
      );
      if (response.ok) {
        const data = await response.json();
        if (typeof data.expectedDpr === 'number') {
          return data.expectedDpr;
        }
      }
    } catch {
      // Fallback
    }
    return calculateExpectedDpr(archetype, level);
  },

  /**
   * Crea un combatiente customizado (Héroe / Personaje de Jugador) con estadísticas finales.
   */
  async createCustomHero(request: CreateCustomHeroRequestDto): Promise<CreatureResponseDto> {
    try {
      const response = await fetch('/api/v1/creatures/custom', {
        method: 'POST',
        headers: getAuthHeaders({ 'Content-Type': 'application/json' }),
        body: JSON.stringify(request),
      });

      if (response.ok) {
        const created: CreatureResponseDto = await response.json();
        inMemoryCreatures.unshift(created);
        return created;
      }
    } catch {
      // Fallback local
    }

    const str = request.strength ?? 10;
    const dex = request.dexterity ?? 10;
    const con = request.constitution ?? 10;
    const int = request.intelligence ?? 10;
    const wis = request.wisdom ?? 10;
    const cha = request.charisma ?? 10;
    const calcMod = (val: number) => Math.floor((val - 10) / 2);

    const calculatedDpr =
      request.expectedDpr ??
      (request.archetype && request.level ? calculateExpectedDpr(request.archetype, request.level) : null);

    const newHero: CreatureResponseDto = {
      id: `hero-${Date.now()}`,
      name: request.name,
      size: 'Medium',
      type: 'Humanoid',
      alignment: 'Heroic',
      challengeRating: 1,
      speedFeet: request.speedFeet || 30,
      proficiencyBonus: 2,
      effectiveArmorClass: request.armorClass,
      isPlayerCharacter: true,
      level: request.level ?? 1,
      archetype: request.archetype ?? null,
      expectedDpr: calculatedDpr,
      abilityScores: {
        strength: str,
        strengthModifier: calcMod(str),
        dexterity: dex,
        dexterityModifier: calcMod(dex),
        constitution: con,
        constitutionModifier: calcMod(con),
        intelligence: int,
        intelligenceModifier: calcMod(int),
        wisdom: wis,
        wisdomModifier: calcMod(wis),
        charisma: cha,
        charismaModifier: calcMod(cha),
      },
      hitPoints: {
        max: request.maxHitPoints,
        current: request.maxHitPoints,
        temporary: 0,
      },
      armorClass: {
        base: request.armorClass,
        shieldBonus: 0,
        allowsDexterityModifier: false,
        maxDexterityModifier: null,
      },
      actions: [
        {
          id: `act-${Date.now()}`,
          creatureId: `hero-${Date.now()}`,
          name: request.attackName || (request.archetype ? `${request.archetype} Attack` : 'Ataque Principal'),
          type: 'Action',
          description: request.archetype
            ? `Ataque basado en arquetipo ${request.archetype} (${calculatedDpr ?? 0} DPR)`
            : `Ataque estándar del héroe ${request.name}.`,
          isAttack: true,
          attackBonus: calcMod(str) + 2,
          reachOrRangeFeet: 5,
          damageFormula: request.damageFormula || (calculatedDpr ? `${calculatedDpr} DPR` : '1d8'),
          damageType: 'Slashing',
          attackModifierAbility: request.attackModifierAbility || 'Strength',
          associatedMasteryId: null,
        },
      ],
      knownMasteries: [],
    };

    inMemoryCreatures.unshift(newHero);
    return newHero;
  },

  /**
   * Dispara la sincronización de monstruos desde el Bestiario de Open5e hacia la base de datos.
   */
  async syncOpen5eMonsters(limit = 50): Promise<IngestionResultDto> {
    try {
      const response = await fetch(`/api/ingestion/open5e/monsters/sync?limit=${limit}`, {
        method: 'POST',
      });
      if (response.ok) {
        return await response.json();
      }
    } catch {
      // Fallback
    }

    return {
      totalProcessed: limit,
      inserted: limit,
      updated: 0,
      errors: [],
    };
  },

  /**
   * Ejecuta una simulación estocástica de combate masiva por el método de Monte Carlo.
   */
  async runMontecarlo(request: MontecarloRequestDto): Promise<MontecarloResultDto> {
    try {
      const response = await fetch('/api/simulation/montecarlo', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(request),
      });

      if (response.ok) {
        return await response.json();
      }
    } catch {
      // Fallback
    }

    const it = request.iterations || 10000;
    const teamAReqs = request.teamA && request.teamA.length > 0
      ? request.teamA
      : request.combatantAId ? [{ creatureId: request.combatantAId, weaponId: request.weaponAId }] : [];
    const teamBReqs = request.teamB && request.teamB.length > 0
      ? request.teamB
      : request.combatantBId ? [{ creatureId: request.combatantBId, weaponId: request.weaponBId }] : [];

    const teamACreatures = teamAReqs.map(r => inMemoryCreatures.find(c => c.id === r.creatureId)).filter(Boolean) as CreatureResponseDto[];
    const teamBCreatures = teamBReqs.map(r => inMemoryCreatures.find(c => c.id === r.creatureId)).filter(Boolean) as CreatureResponseDto[];

    const creatureA = teamACreatures[0];
    const creatureB = teamBCreatures[0];
    const weaponA = request.weaponAId ? CANONICAL_WEAPONS.find((w) => w.id === request.weaponAId) : null;
    const weaponB = request.weaponBId ? CANONICAL_WEAPONS.find((w) => w.id === request.weaponBId) : null;

    const aWins = Math.round(it * 0.65);
    const bWins = it - aWins;
    const survivalRate = aWins > bWins ? 78.5 : 32.0;

    return {
      totalIterations: it,
      combatantAWins: aWins,
      combatantBWins: bWins,
      draws: 0,
      combatantAWinRate: 65.0,
      combatantBWinRate: 35.0,
      drawRate: 0.0,
      averageRounds: 3.8,
      combatantAAverageDpr: creatureA?.expectedDpr ?? 8.5,
      combatantBAverageDpr: creatureB?.expectedDpr ?? 5.7,
      combatantAName: teamACreatures.length > 1 ? `Party (${teamACreatures.map(c => c.name).join(', ')})` : (creatureA?.name ?? 'Combatant A'),
      combatantBName: teamBCreatures.length > 1 ? `Encounter (${teamBCreatures.map(c => c.name).join(', ')})` : (creatureB?.name ?? 'Combatant B'),
      weaponAName: weaponA?.name ?? (creatureA?.expectedDpr ? `DPR Baseline (${creatureA.expectedDpr} DPR)` : 'Unarmed'),
      weaponBName: weaponB?.name ?? (creatureB?.expectedDpr ? `DPR Baseline (${creatureB.expectedDpr} DPR)` : 'Unarmed'),
      executionTimeMs: 14,
      teamASurvivalRate: survivalRate,
      teamANames: teamACreatures.map(c => c.name),
      teamBNames: teamBCreatures.map(c => c.name),
    };
  },

  /**
   * Obtiene todas las Partys guardadas pertenecientes al usuario actual.
   */
  async getParties(): Promise<PartyResponseDto[]> {
    try {
      const response = await fetch(`${API_BASE_URL}/parties`, {
        headers: getAuthHeaders(),
      });
      if (response.ok) {
        const data = await response.json();
        inMemoryParties = data;
        return data;
      }
    } catch {
      // Fallback
    }
    return inMemoryParties;
  },

  /**
   * Obtiene una Party por su ID.
   */
  async getPartyById(id: string): Promise<PartyResponseDto | null> {
    try {
      const response = await fetch(`${API_BASE_URL}/parties/${id}`, {
        headers: getAuthHeaders(),
      });
      if (response.ok) {
        return await response.json();
      }
    } catch {
      // Fallback
    }
    return inMemoryParties.find((p) => p.id === id) || null;
  },

  /**
   * Crea una nueva Party de héroes.
   */
  async createParty(request: CreatePartyRequestDto): Promise<PartyResponseDto> {
    try {
      const response = await fetch(`${API_BASE_URL}/parties`, {
        method: 'POST',
        headers: getAuthHeaders({ 'Content-Type': 'application/json' }),
        body: JSON.stringify(request),
      });
      if (response.ok) {
        const data = await response.json();
        inMemoryParties = [data, ...inMemoryParties.filter((p) => p.id !== data.id)];
        return data;
      }
    } catch {
      // Fallback
    }

    const members = request.memberCreatureIds
      .map((id) => inMemoryCreatures.find((c) => c.id === id))
      .filter(Boolean) as CreatureResponseDto[];

    const newParty: PartyResponseDto = {
      id: `party-${Date.now()}-${Math.random().toString(36).substring(2, 7)}`,
      name: request.name,
      ownerUserId: 'mock-user-id',
      createdAt: new Date().toISOString(),
      members,
    };
    inMemoryParties = [newParty, ...inMemoryParties];
    return newParty;
  },

  /**
   * Elimina una Party por su ID.
   */
  async deleteParty(id: string): Promise<void> {
    try {
      const response = await fetch(`${API_BASE_URL}/parties/${id}`, {
        method: 'DELETE',
        headers: getAuthHeaders(),
      });
      if (response.ok) {
        inMemoryParties = inMemoryParties.filter((p) => p.id !== id);
        return;
      }
    } catch {
      // Fallback
    }
    inMemoryParties = inMemoryParties.filter((p) => p.id !== id);
  },

  /**
   * Obtiene todos los Encuentros guardados pertenecientes al usuario actual.
   */
  async getEncounters(): Promise<EncounterResponseDto[]> {
    try {
      const response = await fetch(`${API_BASE_URL}/encounters`, {
        headers: getAuthHeaders(),
      });
      if (response.ok) {
        const data = await response.json();
        inMemoryEncounters = data;
        return data;
      }
    } catch {
      // Fallback
    }
    return inMemoryEncounters;
  },

  /**
   * Obtiene un Encuentro por su ID.
   */
  async getEncounterById(id: string): Promise<EncounterResponseDto | null> {
    try {
      const response = await fetch(`${API_BASE_URL}/encounters/${id}`, {
        headers: getAuthHeaders(),
      });
      if (response.ok) {
        return await response.json();
      }
    } catch {
      // Fallback
    }
    return inMemoryEncounters.find((e) => e.id === id) || null;
  },

  /**
   * Crea un nuevo Encuentro táctico.
   */
  async createEncounter(request: CreateEncounterRequestDto): Promise<EncounterResponseDto> {
    try {
      const response = await fetch(`${API_BASE_URL}/encounters`, {
        method: 'POST',
        headers: getAuthHeaders({ 'Content-Type': 'application/json' }),
        body: JSON.stringify(request),
      });
      if (response.ok) {
        const data = await response.json();
        inMemoryEncounters = [data, ...inMemoryEncounters.filter((e) => e.id !== data.id)];
        return data;
      }
    } catch {
      // Fallback
    }

    const creatures = request.creatureIds
      .map((id) => inMemoryCreatures.find((c) => c.id === id))
      .filter(Boolean) as CreatureResponseDto[];

    const newEncounter: EncounterResponseDto = {
      id: `enc-${Date.now()}-${Math.random().toString(36).substring(2, 7)}`,
      name: request.name,
      ownerUserId: 'mock-user-id',
      difficultyNotes: request.difficultyNotes,
      createdAt: new Date().toISOString(),
      creatures,
    };
    inMemoryEncounters = [newEncounter, ...inMemoryEncounters];
    return newEncounter;
  },

  /**
   * Elimina un Encuentro por su ID.
   */
  async deleteEncounter(id: string): Promise<void> {
    try {
      const response = await fetch(`${API_BASE_URL}/encounters/${id}`, {
        method: 'DELETE',
        headers: getAuthHeaders(),
      });
      if (response.ok) {
        inMemoryEncounters = inMemoryEncounters.filter((e) => e.id !== id);
        return;
      }
    } catch {
      // Fallback
    }
    inMemoryEncounters = inMemoryEncounters.filter((e) => e.id !== id);
  },

  /**
   * Busca equipamiento u objetos mágicos en el SRD mediante el proxy backend con debounce.
   */
  async searchReferenceEquipment(name: string, signal?: AbortSignal): Promise<ReferenceItemSummaryDto[]> {
    try {
      const queryParam = name ? `?name=${encodeURIComponent(name)}` : '';
      const response = await fetch(`${API_BASE_URL}/reference/equipment${queryParam}`, {
        method: 'GET',
        headers: getAuthHeaders({ 'Accept': 'application/json' }),
        signal,
      });
      if (response.ok) {
        return await response.json();
      }
    } catch {
      // Fallback local
    }

    const lower = name.toLowerCase().trim();
    const fallbackCatalog: ReferenceItemSummaryDto[] = [
      { index: 'dagger', name: 'Dagger', url: '/api/v1/reference/equipment/dagger' },
      { index: 'bag-of-holding', name: 'Bag of Holding', url: '/api/v1/reference/equipment/bag-of-holding' },
      { index: 'potion-of-healing', name: 'Potion of Healing', url: '/api/v1/reference/equipment/potion-of-healing' },
      { index: 'ring-of-protection', name: 'Ring of Protection', url: '/api/v1/reference/equipment/ring-of-protection' },
      { index: 'cloak-of-elvenkind', name: 'Cloak of Elvenkind', url: '/api/v1/reference/equipment/cloak-of-elvenkind' },
      { index: 'boots-of-speed', name: 'Boots of Speed', url: '/api/v1/reference/equipment/boots-of-speed' },
      { index: 'sun-blade', name: 'Sun Blade', url: '/api/v1/reference/equipment/sun-blade' },
      { index: 'longsword', name: 'Longsword', url: '/api/v1/reference/equipment/longsword' },
      { index: 'abacus', name: 'Abacus', url: '/api/v1/reference/equipment/abacus' },
      { index: 'rope-hempen-50-feet', name: 'Rope, Hempen (50 feet)', url: '/api/v1/reference/equipment/rope-hempen-50-feet' },
    ];

    if (!lower) return fallbackCatalog;
    return fallbackCatalog.filter((item) =>
      item.name.toLowerCase().includes(lower) || item.index.toLowerCase().includes(lower)
    );
  },

  /**
   * Obtiene los detalles completos de un objeto de referencia oficial SRD.
   */
  async getReferenceEquipmentDetail(index: string, signal?: AbortSignal): Promise<ReferenceEquipmentDetailDto> {
    try {
      const response = await fetch(`${API_BASE_URL}/reference/equipment/${encodeURIComponent(index)}`, {
        method: 'GET',
        headers: getAuthHeaders({ 'Accept': 'application/json' }),
        signal,
      });
      if (response.ok) {
        return await response.json();
      }
    } catch {
      // Fallback local
    }

    const fallbackDetails: Record<string, ReferenceEquipmentDetailDto> = {
      'dagger': {
        index: 'dagger',
        name: 'Dagger',
        weight: 1,
        description: 'Daga de combate 1d4 perforante con propiedades Sutil, Ligera y Arrojadiza.',
        rarity: 'Common',
        itemType: 'Weapons',
        isOfficialSrd: true,
        cost: '2 gp',
      },
      'bag-of-holding': {
        index: 'bag-of-holding',
        name: 'Bag of Holding',
        weight: 15,
        description: 'Bolsa con espacio extradimensional que soporta hasta 500 libras.',
        rarity: 'Uncommon',
        itemType: 'Wondrous Items',
        isOfficialSrd: true,
      },
      'potion-of-healing': {
        index: 'potion-of-healing',
        name: 'Potion of Healing',
        weight: 0.5,
        description: 'Líquido mágico rojo brillante que restaura 2d4 + 2 puntos de golpe al beberse.',
        rarity: 'Common',
        itemType: 'Potion',
        isOfficialSrd: true,
        cost: '50 gp',
      },
      'ring-of-protection': {
        index: 'ring-of-protection',
        name: 'Ring of Protection',
        weight: 0,
        description: 'Otorga un bonificador +1 a la CA y a todas las tiradas de salvación.',
        rarity: 'Rare',
        itemType: 'Ring',
        isOfficialSrd: true,
      },
    };

    return (
      fallbackDetails[index] || {
        index,
        name: index.replace(/-/g, ' ').replace(/\b\w/g, (l) => l.toUpperCase()),
        weight: 1,
        description: `Objeto oficial SRD (${index}).`,
        rarity: 'Common',
        itemType: 'Adventuring Gear',
        isOfficialSrd: true,
      }
    );
  },

  /**
   * Consulta el inventario, fondos y cola de fabricación del alijo compartido de una Party.
   */
  async getPartyStash(partyId: string, signal?: AbortSignal): Promise<PartyStashDto> {
    try {
      const response = await fetch(`${API_BASE_URL}/parties/${partyId}/stash`, {
        method: 'GET',
        headers: getAuthHeaders({ 'Accept': 'application/json' }),
        signal,
      });
      if (response.ok) {
        return await response.json();
      }
    } catch {
      // Fallback local
    }

    // Filtrar objetos en memoria para esta party
    const party = inMemoryParties.find((p) => p.id === partyId);
    const items = inMemoryStashItems.filter((i) => i.partyId === partyId);
    const totalWeight = items.reduce((acc, curr) => acc + curr.weight * curr.quantity, 0);
    const funds = inMemoryPartyFunds[partyId] || {
      partyId,
      copperPieces: 0,
      silverPieces: 0,
      electrumPieces: 0,
      goldPieces: 100,
      platinumPieces: 0,
      totalGoldEquivalent: 100,
    };
    const craftingQueue = inMemoryCraftingQueue.filter((c) => c.partyId === partyId);

    return {
      partyId,
      partyName: party ? party.name : 'Party Desconocida',
      totalWeight,
      items,
      funds,
      craftingQueue,
    };
  },

  /**
   * Agrega un nuevo objeto al alijo compartido de la Party.
   */
  async addStashItem(partyId: string, request: CreateStashItemRequestDto): Promise<StashItemDto> {
    try {
      const response = await fetch(`${API_BASE_URL}/parties/${partyId}/stash`, {
        method: 'POST',
        headers: getAuthHeaders({ 'Content-Type': 'application/json' }),
        body: JSON.stringify(request),
      });
      if (response.ok) {
        const data = await response.json();
        inMemoryStashItems = [data, ...inMemoryStashItems.filter((i) => i.id !== data.id)];
        return data;
      }
    } catch {
      // Fallback local
    }

    const newItem: StashItemDto = {
      id: `stash-${Date.now()}-${Math.random().toString(36).substring(2, 7)}`,
      partyId,
      name: request.name,
      quantity: request.quantity,
      weight: request.weight,
      description: request.description || '',
      rarity: request.rarity || 'Common',
      itemType: request.itemType || 'Adventuring Gear',
      isOfficialSrd: !!request.isOfficialSrd,
      referenceIndex: request.referenceIndex || null,
      assignedCreatureId: request.assignedCreatureId || null,
      createdAt: new Date().toISOString(),
    };

    inMemoryStashItems = [newItem, ...inMemoryStashItems];
    inMemoryActivityLogs = [
      {
        id: `log-${Date.now()}`,
        partyId,
        userDisplayName: 'Héroe',
        actionType: 'ItemAdded',
        description: `Añadido ${newItem.quantity}x ${newItem.name} al alijo.`,
        timestampUtc: new Date().toISOString(),
      },
      ...inMemoryActivityLogs,
    ];
    return newItem;
  },

  /**
   * Remueve un objeto del alijo compartido de la Party.
   */
  async deleteStashItem(partyId: string, itemId: string): Promise<void> {
    const item = inMemoryStashItems.find((i) => i.id === itemId);
    try {
      const response = await fetch(`${API_BASE_URL}/parties/${partyId}/stash/${itemId}`, {
        method: 'DELETE',
        headers: getAuthHeaders(),
      });
      if (response.ok) {
        inMemoryStashItems = inMemoryStashItems.filter((i) => i.id !== itemId);
        return;
      }
    } catch {
      // Fallback local
    }

    inMemoryStashItems = inMemoryStashItems.filter((i) => i.id !== itemId);
    if (item) {
      inMemoryActivityLogs = [
        {
          id: `log-${Date.now()}`,
          partyId,
          userDisplayName: 'Héroe',
          actionType: 'ItemDeleted',
          description: `Eliminado ${item.name} del alijo.`,
          timestampUtc: new Date().toISOString(),
        },
        ...inMemoryActivityLogs,
      ];
    }
  },

  /**
   * Consulta los fondos monetarios (Party Fund) de una Party.
   */
  async getPartyFunds(partyId: string, signal?: AbortSignal): Promise<PartyFundDto> {
    try {
      const response = await fetch(`${API_BASE_URL}/parties/${partyId}/funds`, {
        method: 'GET',
        headers: getAuthHeaders({ 'Accept': 'application/json' }),
        signal,
      });
      if (response.ok) {
        return await response.json();
      }
    } catch {
      // Fallback local
    }

    return (
      inMemoryPartyFunds[partyId] || {
        partyId,
        copperPieces: 0,
        silverPieces: 0,
        electrumPieces: 0,
        goldPieces: 100,
        platinumPieces: 0,
        totalGoldEquivalent: 100,
      }
    );
  },

  /**
   * Actualiza las monedas del tesoro compartido (Party Fund).
   */
  async updatePartyFunds(partyId: string, request: UpdatePartyFundsRequestDto): Promise<PartyFundDto> {
    try {
      const response = await fetch(`${API_BASE_URL}/parties/${partyId}/funds`, {
        method: 'PUT',
        headers: getAuthHeaders({ 'Content-Type': 'application/json' }),
        body: JSON.stringify(request),
      });
      if (response.ok) {
        const data = await response.json();
        inMemoryPartyFunds[partyId] = data;
        return data;
      }
    } catch {
      // Fallback local
    }

    const totalGoldEquivalent =
      request.copperPieces / 100 +
      request.silverPieces / 10 +
      request.electrumPieces / 2 +
      request.goldPieces +
      request.platinumPieces * 10;

    const funds: PartyFundDto = {
      partyId,
      copperPieces: request.copperPieces,
      silverPieces: request.silverPieces,
      electrumPieces: request.electrumPieces,
      goldPieces: request.goldPieces,
      platinumPieces: request.platinumPieces,
      totalGoldEquivalent,
    };

    inMemoryPartyFunds[partyId] = funds;
    inMemoryActivityLogs = [
      {
        id: `log-${Date.now()}`,
        partyId,
        userDisplayName: 'Héroe',
        actionType: 'FundsUpdated',
        description: `Fondos actualizados a ${funds.goldPieces} PO, ${funds.silverPieces} PP. Total: ${totalGoldEquivalent.toFixed(1)} PO.`,
        timestampUtc: new Date().toISOString(),
      },
      ...inMemoryActivityLogs,
    ];

    return funds;
  },

  /**
   * Consulta los proyectos en cola de fabricación de una Party.
   */
  async getCraftingQueue(partyId: string, signal?: AbortSignal): Promise<CraftingQueueItemDto[]> {
    try {
      const response = await fetch(`${API_BASE_URL}/parties/${partyId}/crafting`, {
        method: 'GET',
        headers: getAuthHeaders({ 'Accept': 'application/json' }),
        signal,
      });
      if (response.ok) {
        return await response.json();
      }
    } catch {
      // Fallback local
    }

    return inMemoryCraftingQueue.filter((c) => c.partyId === partyId);
  },

  /**
   * Encola un nuevo objeto para fabricar durante el downtime.
   */
  async queueCraftingItem(partyId: string, request: QueueCraftingItemRequestDto): Promise<CraftingQueueItemDto> {
    try {
      const response = await fetch(`${API_BASE_URL}/parties/${partyId}/crafting`, {
        method: 'POST',
        headers: getAuthHeaders({ 'Content-Type': 'application/json' }),
        body: JSON.stringify(request),
      });
      if (response.ok) {
        const data = await response.json();
        inMemoryCraftingQueue = [data, ...inMemoryCraftingQueue.filter((c) => c.id !== data.id)];
        return data;
      }
    } catch {
      // Fallback local
    }

    // Descontar oro en memoria si existe fondo
    const funds = inMemoryPartyFunds[partyId] || {
      partyId,
      copperPieces: 0,
      silverPieces: 0,
      electrumPieces: 0,
      goldPieces: 100,
      platinumPieces: 0,
      totalGoldEquivalent: 100,
    };
    if (funds.goldPieces >= request.goldCost) {
      funds.goldPieces -= request.goldCost;
      funds.totalGoldEquivalent -= request.goldCost;
      inMemoryPartyFunds[partyId] = { ...funds };
    }

    const newItem: CraftingQueueItemDto = {
      id: `craft-${Date.now()}-${Math.random().toString(36).substring(2, 7)}`,
      partyId,
      itemName: request.itemName,
      itemType: request.itemType || 'Adventuring Gear',
      rarity: request.rarity || 'Common',
      goldCost: request.goldCost,
      daysRequired: request.daysRequired,
      daysRemaining: request.daysRequired,
      weight: request.weight,
      description: request.description || '',
      isOfficialSrd: !!request.isOfficialSrd,
      referenceIndex: request.referenceIndex || null,
      crafterName: request.crafterName || null,
      status: 'InProgress',
      createdAtUtc: new Date().toISOString(),
    };

    inMemoryCraftingQueue = [newItem, ...inMemoryCraftingQueue];
    inMemoryActivityLogs = [
      {
        id: `log-${Date.now()}`,
        partyId,
        userDisplayName: 'Héroe',
        actionType: 'CraftingStarted',
        description: `Iniciada fabricación de '${newItem.itemName}' (${newItem.daysRequired} días, -${newItem.goldCost} PO).`,
        timestampUtc: new Date().toISOString(),
      },
      ...inMemoryActivityLogs,
    ];

    return newItem;
  },

  /**
   * Acción del DM: Avanza 1 día de downtime para todos los proyectos activos de la Party.
   */
  async advanceDowntimeDay(partyId: string): Promise<DowntimeAdvanceResultDto> {
    try {
      const response = await fetch(`${API_BASE_URL}/parties/${partyId}/crafting/advance-day`, {
        method: 'POST',
        headers: getAuthHeaders({ 'Content-Type': 'application/json' }),
      });
      if (response.ok) {
        return await response.json();
      }
    } catch {
      // Fallback local
    }

    const activeItems = inMemoryCraftingQueue.filter(
      (c) => c.partyId === partyId && c.status === 'InProgress'
    );
    const completedNames: string[] = [];

    activeItems.forEach((item) => {
      item.daysRemaining = Math.max(0, item.daysRemaining - 1);
      if (item.daysRemaining === 0) {
        item.status = 'Completed';
        completedNames.push(item.itemName);
        // Transferir al stash
        inMemoryStashItems = [
          {
            id: `stash-crafted-${Date.now()}-${Math.random().toString(36).substring(2, 6)}`,
            partyId,
            name: item.itemName,
            quantity: 1,
            weight: item.weight,
            description: item.description,
            rarity: item.rarity,
            itemType: item.itemType,
            isOfficialSrd: item.isOfficialSrd,
            referenceIndex: item.referenceIndex,
            assignedCreatureId: null,
            createdAt: new Date().toISOString(),
          },
          ...inMemoryStashItems,
        ];
      }
    });

    const updatedQueue = inMemoryCraftingQueue.filter((c) => c.partyId === partyId);
    const updatedStash = inMemoryStashItems.filter((i) => i.partyId === partyId);

    inMemoryActivityLogs = [
      {
        id: `log-${Date.now()}`,
        partyId,
        userDisplayName: 'Dungeon Master',
        actionType: 'DowntimeAdvanced',
        description: `Avanzado 1 día de Downtime. ${completedNames.length} objeto(s) completado(s).`,
        timestampUtc: new Date().toISOString(),
      },
      ...inMemoryActivityLogs,
    ];

    return {
      daysAdvanced: 1,
      completedItemsCount: completedNames.length,
      activeItemsCount: updatedQueue.filter((c) => c.status === 'InProgress').length,
      completedItemNames: completedNames,
      updatedQueue,
      updatedStash,
    };
  },

  /**
   * Consulta el historial de actividad y transacciones de la Party.
   */
  async getStashActivityLogs(partyId: string, limit = 50, signal?: AbortSignal): Promise<StashActivityLogDto[]> {
    try {
      const response = await fetch(`${API_BASE_URL}/parties/${partyId}/stash/logs?limit=${limit}`, {
        method: 'GET',
        headers: getAuthHeaders({ 'Accept': 'application/json' }),
        signal,
      });
      if (response.ok) {
        return await response.json();
      }
    } catch {
      // Fallback local
    }

    return inMemoryActivityLogs.filter((l) => l.partyId === partyId).slice(0, limit);
  },

  /**
   * Obtiene todas las notas de sesión de una Party (DM Only).
   */
  async getSessionNotes(partyId: string, signal?: AbortSignal): Promise<SessionNoteResponseDto[]> {
    try {
      const response = await fetch(`${API_BASE_URL}/parties/${partyId}/notes`, {
        method: 'GET',
        headers: getAuthHeaders({ 'Accept': 'application/json' }),
        signal,
      });
      if (response.ok) {
        return await response.json();
      }
    } catch {
      // Fallback local
    }
    return inMemorySessionNotes.filter((n) => n.partyId === partyId);
  },

  /**
   * Crea una nueva nota de sesión para la Party (DM Only).
   */
  async createSessionNote(partyId: string, request: CreateSessionNoteRequest): Promise<SessionNoteResponseDto> {
    try {
      const response = await fetch(`${API_BASE_URL}/parties/${partyId}/notes`, {
        method: 'POST',
        headers: getAuthHeaders({ 'Content-Type': 'application/json' }),
        body: JSON.stringify(request),
      });
      if (response.ok) {
        const data = await response.json();
        inMemorySessionNotes = [data, ...inMemorySessionNotes.filter((n) => n.id !== data.id)];
        return data;
      }
    } catch {
      // Fallback local
    }

    const newNote: SessionNoteResponseDto = {
      id: `note-${Date.now()}-${Math.random().toString(36).substring(2, 7)}`,
      partyId,
      title: request.title,
      content: request.content,
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
    };
    inMemorySessionNotes = [newNote, ...inMemorySessionNotes];
    return newNote;
  },

  /**
   * Actualiza una nota de sesión existente (DM Only).
   */
  async updateSessionNote(partyId: string, noteId: string, request: UpdateSessionNoteRequest): Promise<SessionNoteResponseDto> {
    try {
      const response = await fetch(`${API_BASE_URL}/parties/${partyId}/notes/${noteId}`, {
        method: 'PUT',
        headers: getAuthHeaders({ 'Content-Type': 'application/json' }),
        body: JSON.stringify(request),
      });
      if (response.ok) {
        const data = await response.json();
        inMemorySessionNotes = inMemorySessionNotes.map((n) => (n.id === noteId ? data : n));
        return data;
      }
    } catch {
      // Fallback local
    }

    let updatedNote: SessionNoteResponseDto | null = null;
    inMemorySessionNotes = inMemorySessionNotes.map((n) => {
      if (n.id === noteId) {
        updatedNote = {
          ...n,
          title: request.title,
          content: request.content,
          updatedAt: new Date().toISOString(),
        };
        return updatedNote;
      }
      return n;
    });

    return updatedNote || {
      id: noteId,
      partyId,
      title: request.title,
      content: request.content,
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
    };
  },

  /**
   * Elimina una nota de sesión por ID (DM Only).
   */
  async deleteSessionNote(partyId: string, noteId: string): Promise<void> {
    try {
      const response = await fetch(`${API_BASE_URL}/parties/${partyId}/notes/${noteId}`, {
        method: 'DELETE',
        headers: getAuthHeaders(),
      });
      if (response.ok) {
        inMemorySessionNotes = inMemorySessionNotes.filter((n) => n.id !== noteId);
        return;
      }
    } catch {
      // Fallback local
    }
    inMemorySessionNotes = inMemorySessionNotes.filter((n) => n.id !== noteId);
  },
};

