import React, { useState, useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import type { CreateCustomHeroRequestDto, CreatureResponseDto } from '../types/dnd';
import { calculateExpectedDpr } from '../services/apiClient';

export interface CustomHeroModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSaveHero: (request: CreateCustomHeroRequestDto) => Promise<CreatureResponseDto>;
  onHeroSaved: (hero: CreatureResponseDto) => void;
}

export const ARCHETYPES = [
  'Fighter 2H',
  'Ranger Ranged',
  'Paladin 2H',
  'Barbarian 2H',
  'Rogue Sneak Attack',
  'Warlock Eldritch Blast',
] as const;

/**
 * Modal interactivo para crear perfiles de combatientes customizados (Héroes / PCs)
 * con estadísticas finales directas y abstracción de DPR por nivel y arquetipo.
 */
export const CustomHeroModal: React.FC<CustomHeroModalProps> = ({
  isOpen,
  onClose,
  onSaveHero,
  onHeroSaved,
}) => {
  const { t } = useTranslation();

  const [name, setName] = useState('');
  const [armorClass, setArmorClass] = useState(16);
  const [maxHitPoints, setMaxHitPoints] = useState(35);
  const [strength, setStrength] = useState(16);
  const [dexterity, setDexterity] = useState(14);
  const [constitution, setConstitution] = useState(14);
  const [archetype, setArchetype] = useState<string>('Fighter 2H');
  const [level, setLevel] = useState<number>(5);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const currentDpr = useMemo(() => {
    return calculateExpectedDpr(archetype, level);
  }, [archetype, level]);

  if (!isOpen) return null;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!name.trim()) return;

    setIsSubmitting(true);
    setErrorMessage(null);

    try {
      const payload: CreateCustomHeroRequestDto = {
        name: name.trim(),
        armorClass: Number(armorClass),
        maxHitPoints: Number(maxHitPoints),
        strength: Number(strength),
        dexterity: Number(dexterity),
        constitution: Number(constitution),
        level: Number(level),
        archetype,
        expectedDpr: currentDpr,
        attackName: `${archetype} Baseline`,
        damageFormula: `${currentDpr} DPR`,
        attackModifierAbility: 'Strength',
      };

      const hero = await onSaveHero(payload);
      onHeroSaved(hero);
      handleClose();
    } catch (err: unknown) {
      setErrorMessage(err instanceof Error ? err.message : 'Error al guardar el héroe custom.');
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleClose = () => {
    setName('');
    setArmorClass(16);
    setMaxHitPoints(35);
    setStrength(16);
    setDexterity(14);
    setConstitution(14);
    setArchetype('Fighter 2H');
    setLevel(5);
    setErrorMessage(null);
    onClose();
  };

  return (
    <div className="modal-backdrop" onClick={handleClose} role="dialog" aria-modal="true">
      <div
        className="modal-content custom-hero-modal fade-in"
        id="custom-hero-modal"
        onClick={(e) => e.stopPropagation()}
      >
        <header className="modal-header">
          <div>
            <h3 className="modal-title">{t('simulation.customHeroModal.title')}</h3>
            <p className="modal-subtitle">{t('simulation.customHeroModal.subtitle')}</p>
          </div>
          <button
            type="button"
            id="close-custom-hero-modal-btn"
            className="modal-close-btn"
            onClick={handleClose}
            aria-label={t('simulation.customHeroModal.cancel')}
          >
            ✕
          </button>
        </header>

        {errorMessage && (
          <div className="alert alert-danger" role="alert">
            {errorMessage}
          </div>
        )}

        <form onSubmit={handleSubmit} className="hero-form">
          <div className="form-group">
            <label htmlFor="hero-name-input" className="form-label">
              {t('simulation.customHeroModal.name')}
            </label>
            <input
              id="hero-name-input"
              className="form-input"
              type="text"
              required
              placeholder={t('simulation.customHeroModal.namePlaceholder')}
              value={name}
              onChange={(e) => setName(e.target.value)}
              disabled={isSubmitting}
            />
          </div>

          <div className="form-row-2">
            <div className="form-group">
              <label htmlFor="hero-ac-input" className="form-label">
                {t('simulation.customHeroModal.ac')}
              </label>
              <input
                id="hero-ac-input"
                className="form-input"
                type="number"
                min="1"
                max="40"
                required
                value={armorClass}
                onChange={(e) => setArmorClass(Number(e.target.value))}
                disabled={isSubmitting}
              />
            </div>

            <div className="form-group">
              <label htmlFor="hero-hp-input" className="form-label">
                {t('simulation.customHeroModal.hp')}
              </label>
              <input
                id="hero-hp-input"
                className="form-input"
                type="number"
                min="1"
                max="1000"
                required
                value={maxHitPoints}
                onChange={(e) => setMaxHitPoints(Number(e.target.value))}
                disabled={isSubmitting}
              />
            </div>
          </div>

          <div className="form-row-3">
            <div className="form-group">
              <label htmlFor="hero-str-input" className="form-label">
                {t('simulation.customHeroModal.str')}
              </label>
              <input
                id="hero-str-input"
                className="form-input"
                type="number"
                min="1"
                max="30"
                value={strength}
                onChange={(e) => setStrength(Number(e.target.value))}
                disabled={isSubmitting}
              />
            </div>

            <div className="form-group">
              <label htmlFor="hero-dex-input" className="form-label">
                {t('simulation.customHeroModal.dex')}
              </label>
              <input
                id="hero-dex-input"
                className="form-input"
                type="number"
                min="1"
                max="30"
                value={dexterity}
                onChange={(e) => setDexterity(Number(e.target.value))}
                disabled={isSubmitting}
              />
            </div>

            <div className="form-group">
              <label htmlFor="hero-con-input" className="form-label">
                {t('simulation.customHeroModal.con')}
              </label>
              <input
                id="hero-con-input"
                className="form-input"
                type="number"
                min="1"
                max="30"
                value={constitution}
                onChange={(e) => setConstitution(Number(e.target.value))}
                disabled={isSubmitting}
              />
            </div>
          </div>

          {/* Abstracción de DPR por Arquetipo y Nivel */}
          <div className="form-row-2">
            <div className="form-group">
              <label htmlFor="hero-archetype-select" className="form-label">
                {t('simulation.customHeroModal.archetype')}
              </label>
              <select
                id="hero-archetype-select"
                className="form-select"
                value={archetype}
                onChange={(e) => setArchetype(e.target.value)}
                disabled={isSubmitting}
              >
                {ARCHETYPES.map((arch) => (
                  <option key={arch} value={arch}>
                    {arch}
                  </option>
                ))}
              </select>
            </div>

            <div className="form-group">
              <label htmlFor="hero-level-select" className="form-label">
                {t('simulation.customHeroModal.level')}
              </label>
              <select
                id="hero-level-select"
                className="form-select"
                value={level}
                onChange={(e) => setLevel(Number(e.target.value))}
                disabled={isSubmitting}
              >
                {Array.from({ length: 20 }, (_, i) => i + 1).map((lvl) => (
                  <option key={lvl} value={lvl}>
                    {lvl}
                  </option>
                ))}
              </select>
            </div>
          </div>

          {/* Panel con DPR Estimado y nota matemática */}
          <div className="dpr-indicator-panel" id="hero-expected-dpr-display">
            <div className="dpr-indicator-header">
              <span className="dpr-indicator-title">
                {t('simulation.customHeroModal.expectedDpr')}:
              </span>
              <span className="dpr-indicator-value">{currentDpr.toFixed(1)} DPR</span>
            </div>
            <p className="dpr-indicator-note">
              {t('simulation.customHeroModal.expectedDprNote')}
            </p>
          </div>

          <div className="modal-actions">
            <button
              type="button"
              id="cancel-custom-hero-btn"
              className="btn btn-secondary"
              onClick={handleClose}
              disabled={isSubmitting}
            >
              {t('simulation.customHeroModal.cancel')}
            </button>
            <button
              type="submit"
              id="save-custom-hero-btn"
              className="btn btn-primary"
              disabled={isSubmitting || !name.trim()}
            >
              {isSubmitting
                ? t('simulation.customHeroModal.saving')
                : t('simulation.customHeroModal.submit')}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};
