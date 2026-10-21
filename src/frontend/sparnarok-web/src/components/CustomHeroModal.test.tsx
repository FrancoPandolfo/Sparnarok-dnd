import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { CustomHeroModal } from './CustomHeroModal';
import i18n from '../i18n';
import type { CreatureResponseDto } from '../types/dnd';

describe('CustomHeroModal Component', () => {
  beforeEach(async () => {
    await i18n.changeLanguage('es');
  });

  const mockSavedHero: CreatureResponseDto = {
    id: 'hero-1',
    name: 'Paladín Roland',
    size: 'Medium',
    type: 'Humanoid',
    alignment: 'Lawful Good',
    challengeRating: 5,
    speedFeet: 30,
    proficiencyBonus: 3,
    effectiveArmorClass: 18,
    isPlayerCharacter: true,
    abilityScores: {
      strength: 18,
      strengthModifier: 4,
      dexterity: 12,
      dexterityModifier: 1,
      constitution: 16,
      constitutionModifier: 3,
      intelligence: 10,
      intelligenceModifier: 0,
      wisdom: 12,
      wisdomModifier: 1,
      charisma: 16,
      charismaModifier: 3,
    },
    hitPoints: { max: 52, current: 52, temporary: 0 },
    armorClass: { base: 18, shieldBonus: 0, allowsDexterityModifier: false, maxDexterityModifier: null },
    actions: [
      {
        id: 'act-1',
        creatureId: 'hero-1',
        name: 'Martillo de Guerra',
        type: 'Action',
        description: 'Ataque de arma cuerpo a cuerpo: +7 al impacto, daño 1d8+4 contundente.',
        isAttack: true,
        attackBonus: 7,
        reachOrRangeFeet: 5,
        damageFormula: '1d8+4',
        damageType: 'Bludgeoning',
        attackModifierAbility: 'Strength',
        associatedMasteryId: null,
      },
    ],
    knownMasteries: [],
  };

  it('does not render when isOpen is false', () => {
    const { container } = render(
      <CustomHeroModal
        isOpen={false}
        onClose={vi.fn()}
        onSaveHero={vi.fn()}
        onHeroSaved={vi.fn()}
      />
    );
    expect(container.firstChild).toBeNull();
  });

  it('renders all form fields when isOpen is true', () => {
    render(
      <CustomHeroModal
        isOpen={true}
        onClose={vi.fn()}
        onSaveHero={vi.fn()}
        onHeroSaved={vi.fn()}
      />
    );

    expect(screen.getByText(/Añadir Héroe Custom/i)).toBeInTheDocument();
    expect(document.getElementById('hero-name-input')).toBeInTheDocument();
    expect(document.getElementById('hero-ac-input')).toBeInTheDocument();
    expect(document.getElementById('hero-hp-input')).toBeInTheDocument();
    expect(document.getElementById('hero-str-input')).toBeInTheDocument();
    expect(document.getElementById('hero-dex-input')).toBeInTheDocument();
    expect(document.getElementById('hero-con-input')).toBeInTheDocument();
    expect(document.getElementById('save-custom-hero-btn')).toBeInTheDocument();
  });

  it('submits valid hero data and triggers callbacks', async () => {
    const mockSave = vi.fn().mockResolvedValue(mockSavedHero);
    const mockSaved = vi.fn();
    const mockClose = vi.fn();

    render(
      <CustomHeroModal
        isOpen={true}
        onClose={mockClose}
        onSaveHero={mockSave}
        onHeroSaved={mockSaved}
      />
    );

    const nameInput = document.getElementById('hero-name-input') as HTMLInputElement;
    const acInput = document.getElementById('hero-ac-input') as HTMLInputElement;
    const hpInput = document.getElementById('hero-hp-input') as HTMLInputElement;

    await userEvent.type(nameInput, 'Paladín Roland');
    await userEvent.clear(acInput);
    await userEvent.type(acInput, '18');
    await userEvent.clear(hpInput);
    await userEvent.type(hpInput, '52');

    const saveBtn = document.getElementById('save-custom-hero-btn') as HTMLButtonElement;
    await userEvent.click(saveBtn);

    await waitFor(() => {
      expect(mockSave).toHaveBeenCalledWith({
        name: 'Paladín Roland',
        armorClass: 18,
        maxHitPoints: 52,
        strength: 16,
        dexterity: 14,
        constitution: 14,
        attackName: 'Espada Larga',
        damageFormula: '1d8+3',
        attackModifierAbility: 'Strength',
      });
      expect(mockSaved).toHaveBeenCalledWith(mockSavedHero);
      expect(mockClose).toHaveBeenCalled();
    });
  });

  it('displays error message if save fails', async () => {
    const mockSave = vi.fn().mockRejectedValue(new Error('Duplicate name or validation error'));

    render(
      <CustomHeroModal
        isOpen={true}
        onClose={vi.fn()}
        onSaveHero={mockSave}
        onHeroSaved={vi.fn()}
      />
    );

    const nameInput = document.getElementById('hero-name-input') as HTMLInputElement;
    await userEvent.type(nameInput, 'Hero Error');

    const saveBtn = document.getElementById('save-custom-hero-btn') as HTMLButtonElement;
    await userEvent.click(saveBtn);

    await waitFor(() => {
      expect(screen.getByText('Duplicate name or validation error')).toBeInTheDocument();
    });
  });
});
