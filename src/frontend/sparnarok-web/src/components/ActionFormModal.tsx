import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type {
  ActionType,
  CreateActionRequestDto,
  CreatureResponseDto,
  DamageType,
  AbilityScoreType,
  WeaponMasteryResponseDto,
} from '../types/dnd';

export interface ActionFormModalProps {
  creature: CreatureResponseDto | null;
  allMasteries: WeaponMasteryResponseDto[];
  isOpen: boolean;
  onClose: () => void;
  onSubmit: (creatureId: string, data: CreateActionRequestDto) => Promise<void>;
}

const ACTION_TYPES: ActionType[] = [
  'Action',
  'BonusAction',
  'Reaction',
  'LegendaryAction',
  'LairAction',
  'Special',
];

const DAMAGE_TYPES: DamageType[] = [
  'Slashing',
  'Piercing',
  'Bludgeoning',
  'Fire',
  'Cold',
  'Lightning',
  'Thunder',
  'Poison',
  'Acid',
  'Psychic',
  'Necrotic',
  'Radiant',
  'Force',
];

const ABILITY_TYPES: AbilityScoreType[] = [
  'Strength',
  'Dexterity',
  'Constitution',
  'Intelligence',
  'Wisdom',
  'Charisma',
];

/**
 * Modal para incorporar una acción o ataque táctico a una criatura.
 */
export const ActionFormModal: React.FC<ActionFormModalProps> = ({
  creature,
  allMasteries,
  isOpen,
  onClose,
  onSubmit,
}) => {
  const { t } = useTranslation();

  const [name, setName] = useState('');
  const [type, setType] = useState<ActionType>('Action');
  const [isAttack, setIsAttack] = useState(true);
  const [reachFeet, setReachFeet] = useState(5);
  const [formula, setFormula] = useState('1d8+2');
  const [damageType, setDamageType] = useState<DamageType>('Piercing');
  const [ability, setAbility] = useState<AbilityScoreType>('Strength');
  const [masteryId, setMasteryId] = useState<string>('');
  const [description, setDescription] = useState('');

  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (!isOpen || !creature) return null;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!name.trim()) {
      setError('El nombre de la acción es obligatorio.');
      return;
    }

    setIsSubmitting(true);
    setError(null);

    try {
      await onSubmit(creature.id, {
        name: name.trim(),
        type,
        isAttack,
        reachOrRangeFeet: Number(reachFeet),
        damageFormula: formula.trim(),
        damageType: isAttack ? damageType : null,
        attackModifierAbility: isAttack ? ability : null,
        associatedMasteryId: masteryId ? masteryId : null,
        description: description.trim(),
      });
      onClose();
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Error al guardar la acción.');
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="modal-backdrop" onClick={onClose} role="dialog" aria-modal="true">
      <div className="modal-content form-modal fade-in" onClick={(e) => e.stopPropagation()}>
        <header className="modal-header">
          <h2 className="modal-title">
            ⚔️ {t('bestiary.actionForm.title', { name: creature.name })}
          </h2>
          <button type="button" className="modal-close-btn" onClick={onClose} aria-label="Close">
            ✕
          </button>
        </header>

        {error && <div className="form-error-banner">{error}</div>}

        <form onSubmit={handleSubmit} className="action-form">
          <div className="form-row two-cols">
            <div className="form-group">
              <label htmlFor="action-name">{t('bestiary.actionForm.name')} *</label>
              <input
                id="action-name"
                type="text"
                required
                value={name}
                onChange={(e) => setName(e.target.value)}
                placeholder="ej. Longsword Slash"
              />
            </div>

            <div className="form-group">
              <label htmlFor="action-type">{t('bestiary.actionForm.type')}</label>
              <select
                id="action-type"
                value={type}
                onChange={(e) => setType(e.target.value as ActionType)}
              >
                {ACTION_TYPES.map((at) => (
                  <option key={at} value={at}>{at}</option>
                ))}
              </select>
            </div>
          </div>

          <div className="form-row checkbox-row">
            <label className="checkbox-label" htmlFor="action-is-attack">
              <input
                id="action-is-attack"
                type="checkbox"
                checked={isAttack}
                onChange={(e) => setIsAttack(e.target.checked)}
              />
              <span>{t('bestiary.actionForm.isAttack')}</span>
            </label>
          </div>

          {isAttack && (
            <div className="form-row three-cols">
              <div className="form-group">
                <label htmlFor="action-reach">{t('bestiary.actionForm.reach')}</label>
                <input
                  id="action-reach"
                  type="number"
                  min="0"
                  value={reachFeet}
                  onChange={(e) => setReachFeet(Number(e.target.value))}
                />
              </div>

              <div className="form-group">
                <label htmlFor="action-formula">{t('bestiary.actionForm.formula')}</label>
                <input
                  id="action-formula"
                  type="text"
                  value={formula}
                  onChange={(e) => setFormula(e.target.value)}
                  placeholder="1d8+3"
                />
              </div>

              <div className="form-group">
                <label htmlFor="action-damage-type">{t('bestiary.actionForm.damageType')}</label>
                <select
                  id="action-damage-type"
                  value={damageType}
                  onChange={(e) => setDamageType(e.target.value as DamageType)}
                >
                  {DAMAGE_TYPES.map((dt) => (
                    <option key={dt} value={dt}>{dt}</option>
                  ))}
                </select>
              </div>
            </div>
          )}

          <div className="form-row two-cols">
            {isAttack && (
              <div className="form-group">
                <label htmlFor="action-ability">{t('bestiary.actionForm.ability')}</label>
                <select
                  id="action-ability"
                  value={ability}
                  onChange={(e) => setAbility(e.target.value as AbilityScoreType)}
                >
                  {ABILITY_TYPES.map((ab) => (
                    <option key={ab} value={ab}>{ab}</option>
                  ))}
                </select>
              </div>
            )}

            <div className="form-group">
              <label htmlFor="action-mastery">{t('bestiary.actionForm.mastery')}</label>
              <select
                id="action-mastery"
                value={masteryId}
                onChange={(e) => setMasteryId(e.target.value)}
              >
                <option value="">-- Ninguna --</option>
                {allMasteries.map((m) => (
                  <option key={m.id} value={m.id}>
                    {m.name} ({m.property})
                  </option>
                ))}
              </select>
            </div>
          </div>

          <div className="form-group">
            <label htmlFor="action-desc">{t('bestiary.actionForm.description')}</label>
            <textarea
              id="action-desc"
              rows={3}
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              placeholder="Detalla los efectos tácticos o condiciones impuestas..."
            />
          </div>

          <footer className="form-actions-footer">
            <button
              type="button"
              className="btn btn-secondary btn-cancel"
              onClick={onClose}
              disabled={isSubmitting}
            >
              {t('bestiary.actionForm.cancel')}
            </button>
            <button
              type="submit"
              id="submit-action-btn"
              className="btn btn-primary btn-submit-gold"
              disabled={isSubmitting}
            >
              {isSubmitting ? '...' : t('bestiary.actionForm.submit')}
            </button>
          </footer>
        </form>
      </div>
    </div>
  );
};
