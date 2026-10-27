import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type {
  CreateCreatureRequestDto,
  CreatureSize,
  CreatureType,
} from '../types/dnd';

export interface CreatureFormModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSubmit: (data: CreateCreatureRequestDto) => Promise<void>;
}

const CREATURE_SIZES: CreatureSize[] = ['Tiny', 'Small', 'Medium', 'Large', 'Huge', 'Gargantuan'];

const CREATURE_TYPES: CreatureType[] = [
  'Aberration',
  'Beast',
  'Celestial',
  'Construct',
  'Dragon',
  'Elemental',
  'Fey',
  'Fiend',
  'Giant',
  'Humanoid',
  'Monstrosity',
  'Ooze',
  'Plant',
  'Undead',
];

const calcMod = (val: number): string => {
  const mod = Math.floor((val - 10) / 2);
  return mod >= 0 ? `+${mod}` : `${mod}`;
};

/**
 * Modal con formulario guiado para la creación de criaturas en el Códice.
 */
export const CreatureFormModal: React.FC<CreatureFormModalProps> = ({
  isOpen,
  onClose,
  onSubmit,
}) => {
  const { t } = useTranslation();

  const [name, setName] = useState('');
  const [size, setSize] = useState<CreatureSize>('Medium');
  const [type, setType] = useState<CreatureType>('Humanoid');
  const [alignment, setAlignment] = useState('Neutral');
  const [challengeRating, setChallengeRating] = useState(1);
  const [speedFeet, setSpeedFeet] = useState(30);

  const [strength, setStrength] = useState(10);
  const [dexterity, setDexterity] = useState(10);
  const [constitution, setConstitution] = useState(10);
  const [intelligence, setIntelligence] = useState(10);
  const [wisdom, setWisdom] = useState(10);
  const [charisma, setCharisma] = useState(10);

  const [maxHitPoints, setMaxHitPoints] = useState(15);
  const [armorClassBase, setArmorClassBase] = useState(12);
  const [shieldBonus, setShieldBonus] = useState(0);
  const [allowsDex, setAllowsDex] = useState(true);

  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (!isOpen) return null;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!name.trim()) {
      setError('El nombre de la criatura es obligatorio.');
      return;
    }

    setIsSubmitting(true);
    setError(null);

    try {
      await onSubmit({
        name: name.trim(),
        size,
        type,
        alignment: alignment.trim() || 'Unaligned',
        challengeRating: Number(challengeRating),
        speedFeet: Number(speedFeet),
        strength: Number(strength),
        dexterity: Number(dexterity),
        constitution: Number(constitution),
        intelligence: Number(intelligence),
        wisdom: Number(wisdom),
        charisma: Number(charisma),
        maxHitPoints: Number(maxHitPoints),
        armorClassBase: Number(armorClassBase),
        shieldBonus: Number(shieldBonus),
        allowsDexterityModifier: allowsDex,
        maxDexterityModifier: null,
      });
      onClose();
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Error al registrar la criatura.');
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="modal-backdrop" onClick={onClose} role="dialog" aria-modal="true">
      <div className="modal-content form-modal fade-in" onClick={(e) => e.stopPropagation()}>
        <header className="modal-header">
          <h2 className="modal-title">⚔️ {t('bestiary.form.createTitle')}</h2>
          <button type="button" className="modal-close-btn" onClick={onClose} aria-label="Close">
            ✕
          </button>
        </header>

        {error && <div className="form-error-banner">{error}</div>}

        <form onSubmit={handleSubmit} className="creature-form">
          {/* Fila 1: Nombre, Tamaño, Tipo */}
          <div className="form-row three-cols">
            <div className="form-group">
              <label htmlFor="creature-name">{t('bestiary.form.name')} *</label>
              <input
                id="creature-name"
                type="text"
                required
                value={name}
                onChange={(e) => setName(e.target.value)}
                placeholder="ej. Shadow Blade Assassin"
              />
            </div>

            <div className="form-group">
              <label htmlFor="creature-size">{t('bestiary.form.size')}</label>
              <select
                id="creature-size"
                value={size}
                onChange={(e) => setSize(e.target.value as CreatureSize)}
              >
                {CREATURE_SIZES.map((s) => (
                  <option key={s} value={s}>{s}</option>
                ))}
              </select>
            </div>

            <div className="form-group">
              <label htmlFor="creature-type">{t('bestiary.form.type')}</label>
              <select
                id="creature-type"
                value={type}
                onChange={(e) => setType(e.target.value as CreatureType)}
              >
                {CREATURE_TYPES.map((tp) => (
                  <option key={tp} value={tp}>{tp}</option>
                ))}
              </select>
            </div>
          </div>

          {/* Fila 2: Alineamiento, CR, Velocidad */}
          <div className="form-row three-cols">
            <div className="form-group">
              <label htmlFor="creature-alignment">{t('bestiary.form.alignment')}</label>
              <input
                id="creature-alignment"
                type="text"
                value={alignment}
                onChange={(e) => setAlignment(e.target.value)}
                placeholder="ej. Lawful Evil"
              />
            </div>

            <div className="form-group">
              <label htmlFor="creature-cr">{t('bestiary.form.cr')} (0 - 30)</label>
              <input
                id="creature-cr"
                type="number"
                min="0"
                max="30"
                step="0.5"
                value={challengeRating}
                onChange={(e) => setChallengeRating(Number(e.target.value))}
              />
            </div>

            <div className="form-group">
              <label htmlFor="creature-speed">{t('bestiary.form.speed')}</label>
              <input
                id="creature-speed"
                type="number"
                min="0"
                value={speedFeet}
                onChange={(e) => setSpeedFeet(Number(e.target.value))}
              />
            </div>
          </div>

          {/* Fila 3: Vitals (HP, CA Base, Escudo) */}
          <div className="form-row three-cols">
            <div className="form-group">
              <label htmlFor="creature-hp">{t('bestiary.form.maxHp')} *</label>
              <input
                id="creature-hp"
                type="number"
                min="1"
                required
                value={maxHitPoints}
                onChange={(e) => setMaxHitPoints(Number(e.target.value))}
              />
            </div>

            <div className="form-group">
              <label htmlFor="creature-ac">{t('bestiary.form.acBase')} *</label>
              <input
                id="creature-ac"
                type="number"
                min="1"
                required
                value={armorClassBase}
                onChange={(e) => setArmorClassBase(Number(e.target.value))}
              />
            </div>

            <div className="form-group">
              <label htmlFor="creature-shield">{t('bestiary.form.shieldBonus')}</label>
              <input
                id="creature-shield"
                type="number"
                min="0"
                value={shieldBonus}
                onChange={(e) => setShieldBonus(Number(e.target.value))}
              />
            </div>
          </div>

          <div className="form-row checkbox-row">
            <label className="checkbox-label" htmlFor="creature-allows-dex">
              <input
                id="creature-allows-dex"
                type="checkbox"
                checked={allowsDex}
                onChange={(e) => setAllowsDex(e.target.checked)}
              />
              <span>{t('bestiary.form.allowsDex')}</span>
            </label>
          </div>

          {/* Características D20 */}
          <div className="form-abilities-box">
            <h4 className="abilities-heading">{t('bestiary.form.abilitiesTitle')}</h4>
            <div className="abilities-inputs-grid">
              <div className="ability-field">
                <label htmlFor="ability-str">STR ({calcMod(strength)})</label>
                <input
                  id="ability-str"
                  type="number"
                  min="1"
                  max="30"
                  value={strength}
                  onChange={(e) => setStrength(Number(e.target.value))}
                />
              </div>

              <div className="ability-field">
                <label htmlFor="ability-dex">DEX ({calcMod(dexterity)})</label>
                <input
                  id="ability-dex"
                  type="number"
                  min="1"
                  max="30"
                  value={dexterity}
                  onChange={(e) => setDexterity(Number(e.target.value))}
                />
              </div>

              <div className="ability-field">
                <label htmlFor="ability-con">CON ({calcMod(constitution)})</label>
                <input
                  id="ability-con"
                  type="number"
                  min="1"
                  max="30"
                  value={constitution}
                  onChange={(e) => setConstitution(Number(e.target.value))}
                />
              </div>

              <div className="ability-field">
                <label htmlFor="ability-int">INT ({calcMod(intelligence)})</label>
                <input
                  id="ability-int"
                  type="number"
                  min="1"
                  max="30"
                  value={intelligence}
                  onChange={(e) => setIntelligence(Number(e.target.value))}
                />
              </div>

              <div className="ability-field">
                <label htmlFor="ability-wis">WIS ({calcMod(wisdom)})</label>
                <input
                  id="ability-wis"
                  type="number"
                  min="1"
                  max="30"
                  value={wisdom}
                  onChange={(e) => setWisdom(Number(e.target.value))}
                />
              </div>

              <div className="ability-field">
                <label htmlFor="ability-cha">CHA ({calcMod(charisma)})</label>
                <input
                  id="ability-cha"
                  type="number"
                  min="1"
                  max="30"
                  value={charisma}
                  onChange={(e) => setCharisma(Number(e.target.value))}
                />
              </div>
            </div>
          </div>

          <footer className="form-actions-footer">
            <button
              type="button"
              className="btn btn-secondary btn-cancel"
              onClick={onClose}
              disabled={isSubmitting}
            >
              {t('bestiary.form.cancel')}
            </button>
            <button
              type="submit"
              id="submit-creature-btn"
              className="btn btn-primary btn-submit-gold"
              disabled={isSubmitting}
            >
              {isSubmitting ? t('bestiary.form.saving') : t('bestiary.form.submit')}
            </button>
          </footer>
        </form>
      </div>
    </div>
  );
};
