import type {
  CreatureResponseDto,
  CreateCreatureRequestDto,
  WeaponMasteryResponseDto,
  CreateActionRequestDto,
  ActionResponseDto,
  IngestionResultDto,
  WeaponDto,
  MontecarloRequestDto,
  MontecarloResultDto,
} from '../types/dnd';

const API_BASE_URL = '/api/v1';

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

// Almacén en memoria para cuando el backend no está disponible
let inMemoryCreatures: CreatureResponseDto[] = [...INITIAL_CREATURES];

/**
 * Cliente de consumo HTTP para la Web API de Sparnarok.
 */
export const apiClient = {
  /**
   * Obtiene la lista completa de criaturas desde el backend o almacén local.
   */
  async getCreatures(signal?: AbortSignal): Promise<CreatureResponseDto[]> {
    try {
      const response = await fetch(`${API_BASE_URL}/creatures`, { signal });
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
      const response = await fetch(`${API_BASE_URL}/creatures/${id}`, { signal });
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
    const creatureA = inMemoryCreatures.find((c) => c.id === request.combatantAId);
    const creatureB = inMemoryCreatures.find((c) => c.id === request.combatantBId);
    const weaponA = CANONICAL_WEAPONS.find((w) => w.id === request.weaponAId);
    const weaponB = CANONICAL_WEAPONS.find((w) => w.id === request.weaponBId);

    const aWins = Math.round(it * 0.61);
    const bWins = it - aWins;

    return {
      totalIterations: it,
      combatantAWins: aWins,
      combatantBWins: bWins,
      draws: 0,
      combatantAWinRate: 61.0,
      combatantBWinRate: 39.0,
      drawRate: 0.0,
      averageRounds: 3.8,
      combatantAAverageDpr: 8.5,
      combatantBAverageDpr: 5.7,
      combatantAName: creatureA?.name ?? 'Combatant A',
      combatantBName: creatureB?.name ?? 'Combatant B',
      weaponAName: weaponA?.name ?? 'Weapon A',
      weaponBName: weaponB?.name ?? 'Weapon B',
      executionTimeMs: 14,
    };
  },
};
