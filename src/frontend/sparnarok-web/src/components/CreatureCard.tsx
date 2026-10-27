import React from 'react';
import { useTranslation } from 'react-i18next';
import type { CreatureResponseDto, MasteryProperty } from '../types/dnd';

export interface CreatureCardProps {
  creature: CreatureResponseDto;
  onSelect: (creature: CreatureResponseDto) => void;
  onDelete: (id: string, name: string) => void;
}

const MASTERY_COLORS: Record<MasteryProperty, string> = {
  Cleave: '#f97316',
  Graze: '#d97706',
  Nick: '#10b981',
  Push: '#6366f1',
  Sap: '#f43f5e',
  Slow: '#0ea5e9',
  Topple: '#8b5cf6',
  Vex: '#06b6d4',
};

const formatModifier = (mod: number): string => (mod >= 0 ? `+${mod}` : `${mod}`);

/**
 * Tarjeta táctica resumida para visualización de una criatura en el Bestiario.
 */
export const CreatureCard: React.FC<CreatureCardProps> = ({ creature, onSelect, onDelete }) => {
  const { t } = useTranslation();

  return (
    <article className="creature-card fade-in" data-testid={`creature-card-${creature.id}`}>
      <header className="card-header">
        <div className="card-identity">
          <h3 className="card-name">{creature.name}</h3>
          <span className="card-meta">
            {creature.size} {creature.type} &bull; {creature.alignment}
          </span>
        </div>
        <div className="card-cr-badge" title="Challenge Rating & Proficiency Bonus">
          <span className="cr-label">{t('bestiary.stats.cr')} {creature.challengeRating}</span>
          <span className="pb-sub">+{creature.proficiencyBonus} {t('bestiary.stats.pb')}</span>
        </div>
      </header>

      {/* Vitals: AC, HP, Speed */}
      <div className="card-vitals">
        <div className="vital-item ac-badge" title="Clase de Armadura">
          <span className="vital-icon">🛡️</span>
          <div className="vital-content">
            <span className="vital-value">{creature.effectiveArmorClass}</span>
            <span className="vital-label">{t('bestiary.stats.ac')}</span>
          </div>
        </div>

        <div className="vital-item hp-badge" title="Puntos de Golpe Máximos">
          <span className="vital-icon">❤️</span>
          <div className="vital-content">
            <span className="vital-value">{creature.hitPoints.max}</span>
            <span className="vital-label">{t('bestiary.stats.hp')}</span>
          </div>
        </div>

        <div className="vital-item speed-badge" title="Velocidad Terrestre">
          <span className="vital-icon">⚡</span>
          <div className="vital-content">
            <span className="vital-value">{creature.speedFeet}'</span>
            <span className="vital-label">{t('bestiary.stats.speed')}</span>
          </div>
        </div>
      </div>

      {/* Grid de 6 características D20 */}
      <div className="card-abilities">
        <div className="ability-chip">
          <span className="ability-name">{t('bestiary.stats.str')}</span>
          <span className="ability-score">{creature.abilityScores.strength}</span>
          <span className="ability-mod">{formatModifier(creature.abilityScores.strengthModifier)}</span>
        </div>
        <div className="ability-chip">
          <span className="ability-name">{t('bestiary.stats.dex')}</span>
          <span className="ability-score">{creature.abilityScores.dexterity}</span>
          <span className="ability-mod">{formatModifier(creature.abilityScores.dexterityModifier)}</span>
        </div>
        <div className="ability-chip">
          <span className="ability-name">{t('bestiary.stats.con')}</span>
          <span className="ability-score">{creature.abilityScores.constitution}</span>
          <span className="ability-mod">{formatModifier(creature.abilityScores.constitutionModifier)}</span>
        </div>
        <div className="ability-chip">
          <span className="ability-name">{t('bestiary.stats.int')}</span>
          <span className="ability-score">{creature.abilityScores.intelligence}</span>
          <span className="ability-mod">{formatModifier(creature.abilityScores.intelligenceModifier)}</span>
        </div>
        <div className="ability-chip">
          <span className="ability-name">{t('bestiary.stats.wis')}</span>
          <span className="ability-score">{creature.abilityScores.wisdom}</span>
          <span className="ability-mod">{formatModifier(creature.abilityScores.wisdomModifier)}</span>
        </div>
        <div className="ability-chip">
          <span className="ability-name">{t('bestiary.stats.cha')}</span>
          <span className="ability-score">{creature.abilityScores.charisma}</span>
          <span className="ability-mod">{formatModifier(creature.abilityScores.charismaModifier)}</span>
        </div>
      </div>

      {/* Maestrías de Armas (2024) */}
      {creature.knownMasteries.length > 0 && (
        <div className="card-masteries">
          {creature.knownMasteries.map((mastery) => (
            <span
              key={mastery.id}
              className="mastery-tag"
              style={{
                borderColor: MASTERY_COLORS[mastery.property] || '#8b5cf6',
                color: MASTERY_COLORS[mastery.property] || '#8b5cf6',
              }}
              title={mastery.description}
            >
              ✦ {mastery.name}
            </span>
          ))}
        </div>
      )}

      {/* Acciones de pie de tarjeta */}
      <footer className="card-footer">
        <button
          type="button"
          className="btn btn-outline btn-sm btn-inspect"
          onClick={() => onSelect(creature)}
        >
          {t('bestiary.viewDetails')} &rarr;
        </button>
        <button
          type="button"
          className="btn btn-danger btn-sm btn-icon btn-delete"
          onClick={() => onDelete(creature.id, creature.name)}
          title={t('bestiary.delete')}
          aria-label={`${t('bestiary.delete')} ${creature.name}`}
        >
          🗑️
        </button>
      </footer>
    </article>
  );
};
