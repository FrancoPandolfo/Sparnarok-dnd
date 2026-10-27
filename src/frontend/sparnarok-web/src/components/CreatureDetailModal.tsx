import React from 'react';
import { useTranslation } from 'react-i18next';
import type { CreatureResponseDto, WeaponMasteryResponseDto } from '../types/dnd';

export interface CreatureDetailModalProps {
  creature: CreatureResponseDto | null;
  allMasteries: WeaponMasteryResponseDto[];
  onClose: () => void;
  onOpenAddAction: (creature: CreatureResponseDto) => void;
  onLearnMastery: (creatureId: string, masteryId: string) => void;
}

const formatModifier = (mod: number): string => (mod >= 0 ? `+${mod}` : `${mod}`);

/**
 * Modal detallado que renderiza el Bloque de Estadísticas canónico de D&D 5e (2024).
 */
export const CreatureDetailModal: React.FC<CreatureDetailModalProps> = ({
  creature,
  allMasteries,
  onClose,
  onOpenAddAction,
  onLearnMastery,
}) => {
  const { t } = useTranslation();

  if (!creature) return null;

  const unlearnedMasteries = allMasteries.filter(
    (m) => !creature.knownMasteries.some((km) => km.id === m.id)
  );

  return (
    <div className="modal-backdrop" onClick={onClose} role="dialog" aria-modal="true">
      <div className="modal-content stat-block-modal fade-in" onClick={(e) => e.stopPropagation()}>
        {/* Encabezado del bloque de estadísticas */}
        <header className="stat-block-header">
          <div>
            <h2 className="stat-creature-name">{creature.name}</h2>
            <p className="stat-creature-meta">
              {creature.size} {creature.type}, {creature.alignment}
            </p>
          </div>
          <button
            type="button"
            id="close-stat-modal-btn"
            className="modal-close-btn"
            onClick={onClose}
            aria-label={t('bestiary.form.cancel')}
          >
            ✕
          </button>
        </header>

        <div className="stat-block-divider" />

        {/* Parámetros defensivos y de movilidad */}
        <div className="stat-core-stats">
          <div className="stat-line">
            <span className="stat-label">{t('bestiary.stats.ac')}:</span>
            <span className="stat-value">
              {creature.effectiveArmorClass} (Base: {creature.armorClass.base}
              {creature.armorClass.shieldBonus > 0 && `, Escudo: +${creature.armorClass.shieldBonus}`}
              {creature.armorClass.allowsDexterityModifier && ', Destreza'})
            </span>
          </div>

          <div className="stat-line">
            <span className="stat-label">{t('bestiary.stats.hp')}:</span>
            <span className="stat-value">
              {creature.hitPoints.max}
              {creature.hitPoints.temporary > 0 && ` (+${creature.hitPoints.temporary} temp)`}
            </span>
          </div>

          <div className="stat-line">
            <span className="stat-label">{t('bestiary.stats.speed')}:</span>
            <span className="stat-value">{t('bestiary.stats.speedValue', { value: creature.speedFeet })}</span>
          </div>

          <div className="stat-line">
            <span className="stat-label">{t('bestiary.stats.cr')}:</span>
            <span className="stat-value">
              {creature.challengeRating} (Bono de Competencia: +{creature.proficiencyBonus})
            </span>
          </div>
        </div>

        <div className="stat-block-divider" />

        {/* Tabla de Características y Salvaciones canónicas (2024) */}
        <section className="stat-abilities-section">
          <h4 className="section-title">{t('bestiary.details.abilityScores')}</h4>
          <div className="stat-abilities-grid">
            <div className="stat-ability-col">
              <span className="col-name">{t('bestiary.stats.str')}</span>
              <span className="col-val">{creature.abilityScores.strength} ({formatModifier(creature.abilityScores.strengthModifier)})</span>
              <span className="col-dc">CD {8 + creature.proficiencyBonus + creature.abilityScores.strengthModifier}</span>
            </div>
            <div className="stat-ability-col">
              <span className="col-name">{t('bestiary.stats.dex')}</span>
              <span className="col-val">{creature.abilityScores.dexterity} ({formatModifier(creature.abilityScores.dexterityModifier)})</span>
              <span className="col-dc">CD {8 + creature.proficiencyBonus + creature.abilityScores.dexterityModifier}</span>
            </div>
            <div className="stat-ability-col">
              <span className="col-name">{t('bestiary.stats.con')}</span>
              <span className="col-val">{creature.abilityScores.constitution} ({formatModifier(creature.abilityScores.constitutionModifier)})</span>
              <span className="col-dc">CD {8 + creature.proficiencyBonus + creature.abilityScores.constitutionModifier}</span>
            </div>
            <div className="stat-ability-col">
              <span className="col-name">{t('bestiary.stats.int')}</span>
              <span className="col-val">{creature.abilityScores.intelligence} ({formatModifier(creature.abilityScores.intelligenceModifier)})</span>
              <span className="col-dc">CD {8 + creature.proficiencyBonus + creature.abilityScores.intelligenceModifier}</span>
            </div>
            <div className="stat-ability-col">
              <span className="col-name">{t('bestiary.stats.wis')}</span>
              <span className="col-val">{creature.abilityScores.wisdom} ({formatModifier(creature.abilityScores.wisdomModifier)})</span>
              <span className="col-dc">CD {8 + creature.proficiencyBonus + creature.abilityScores.wisdomModifier}</span>
            </div>
            <div className="stat-ability-col">
              <span className="col-name">{t('bestiary.stats.cha')}</span>
              <span className="col-val">{creature.abilityScores.charisma} ({formatModifier(creature.abilityScores.charismaModifier)})</span>
              <span className="col-dc">CD {8 + creature.proficiencyBonus + creature.abilityScores.charismaModifier}</span>
            </div>
          </div>
        </section>

        <div className="stat-block-divider" />

        {/* Sección de Acciones Tácticas */}
        <section className="stat-actions-section">
          <div className="section-header-row">
            <h4 className="section-title">{t('bestiary.details.actionsTitle')}</h4>
            <button
              type="button"
              id="add-action-btn"
              className="btn btn-gold btn-xs btn-small-gold"
              onClick={() => onOpenAddAction(creature)}
            >
              + {t('bestiary.details.addAction')}
            </button>
          </div>

          {creature.actions.length === 0 ? (
            <p className="empty-subtext">{t('bestiary.details.noActions')}</p>
          ) : (
            <div className="actions-list">
              {creature.actions.map((action) => (
                <div key={action.id} className="action-item">
                  <div className="action-heading">
                    <span className="action-name">⚔️ {action.name}</span>
                    <span className="action-type-pill">{action.type}</span>
                    {action.associatedMastery && (
                      <span className="action-mastery-pill">
                        ✦ {action.associatedMastery.name}
                      </span>
                    )}
                  </div>
                  <p className="action-meta">
                    {action.isAttack && action.attackBonus !== null && (
                      <span className="action-metric">+{action.attackBonus} para impactar &bull; </span>
                    )}
                    <span className="action-metric">Alcance {action.reachOrRangeFeet}' &bull; </span>
                    {action.damageFormula && (
                      <span className="action-metric">
                        {action.damageFormula} {action.damageType || ''}
                      </span>
                    )}
                  </p>
                  {action.description && <p className="action-desc">{action.description}</p>}
                </div>
              ))}
            </div>
          )}
        </section>

        <div className="stat-block-divider" />

        {/* Sección de Maestrías de Armas (2024) */}
        <section className="stat-masteries-section">
          <div className="section-header-row">
            <h4 className="section-title">{t('bestiary.details.masteriesTitle')}</h4>
            {unlearnedMasteries.length > 0 && (
              <div className="learn-mastery-group">
                <select
                  id="select-mastery"
                  className="mastery-select"
                  defaultValue=""
                  onChange={(e) => {
                    if (e.target.value) {
                      onLearnMastery(creature.id, e.target.value);
                      e.target.value = '';
                    }
                  }}
                >
                  <option value="" disabled>
                    + {t('bestiary.details.learnMastery')}...
                  </option>
                  {unlearnedMasteries.map((m) => (
                    <option key={m.id} value={m.id}>
                      {m.name} ({m.property})
                    </option>
                  ))}
                </select>
              </div>
            )}
          </div>

          {creature.knownMasteries.length === 0 ? (
            <p className="empty-subtext">{t('bestiary.details.noMasteries')}</p>
          ) : (
            <div className="masteries-detail-list">
              {creature.knownMasteries.map((m) => (
                <div key={m.id} className="mastery-detail-card">
                  <div className="mastery-card-header">
                    <span className="mastery-title">✦ {m.name}</span>
                    <span className="mastery-req">{m.prerequisiteWeapon}</span>
                  </div>
                  <p className="mastery-body">{m.description}</p>
                </div>
              ))}
            </div>
          )}
        </section>
      </div>
    </div>
  );
};
