import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { CombatSimulatorView } from './CombatSimulatorView';
import i18n from '../i18n';
import type { CreatureResponseDto, WeaponDto, MontecarloResultDto } from '../types/dnd';

describe('CombatSimulatorView Component Suite (Party vs Encounter)', () => {
  beforeEach(async () => {
    await i18n.changeLanguage('es');
  });

  const mockCreatures: CreatureResponseDto[] = [
    {
      id: 'c-1',
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
      actions: [],
      knownMasteries: [],
    },
    {
      id: 'c-2',
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
      actions: [],
      knownMasteries: [],
    },
  ];

  const mockWeapons: WeaponDto[] = [
    {
      id: 'w-1',
      name: 'Longsword',
      damage: '1d8',
      damageType: 'Slashing',
      properties: 'Versatile (1d10)',
      weight: 3.0,
      cost: '15 gp',
    },
    {
      id: 'w-2',
      name: 'Greatsword',
      damage: '2d6',
      damageType: 'Slashing',
      properties: 'Heavy, Two-Handed',
      weight: 6.0,
      cost: '50 gp',
    },
  ];

  const mockSimulationResult: MontecarloResultDto = {
    totalIterations: 10000,
    combatantAWins: 3200,
    combatantBWins: 6800,
    draws: 0,
    combatantAWinRate: 32.0,
    combatantBWinRate: 68.0,
    drawRate: 0.0,
    averageRounds: 4.25,
    combatantAAverageDpr: 6.8,
    combatantBAverageDpr: 11.4,
    combatantAName: 'Party (Bugbear Stalker)',
    combatantBName: 'Encounter (Gorgon Vanguard)',
    weaponAName: 'Longsword',
    weaponBName: 'Greatsword',
    executionTimeMs: 12,
    teamASurvivalRate: 28.5,
  };

  it('renders team roster cards, dynamic add buttons, targeting selector, and run button', () => {
    const mockRun = vi.fn();
    render(
      <CombatSimulatorView
        creatures={mockCreatures}
        weapons={mockWeapons}
        onRunSimulation={mockRun}
      />
    );

    expect(screen.getByText(/Simulador de Combate de Montecarlo \(Party vs Encounter\)/i)).toBeInTheDocument();
    expect(screen.getByText(/Equipo A \(La Party \/ Héroes\)/i)).toBeInTheDocument();
    expect(screen.getByText(/Equipo B \(El Encuentro \/ Monstruos\)/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Simular Combate \(Montecarlo\)/i })).toBeInTheDocument();
    expect(document.getElementById('add-member-a-btn')).toBeInTheDocument();
    expect(document.getElementById('add-member-b-btn')).toBeInTheDocument();
    expect(document.getElementById('targeting-strategy-select')).toBeInTheDocument();
    expect(document.getElementById('combatant-a-select')).toBeInTheDocument();
    expect(document.getElementById('combatant-b-select')).toBeInTheDocument();
    expect(document.getElementById('iterations-select')).toBeInTheDocument();
  });

  it('allows adding and removing members dynamically in teams', async () => {
    render(
      <CombatSimulatorView
        creatures={mockCreatures}
        weapons={mockWeapons}
        onRunSimulation={vi.fn()}
      />
    );

    const addMemberABtn = document.getElementById('add-member-a-btn')!;
    await userEvent.click(addMemberABtn);

    // Now Team A should have 2 members and a remove button for the second member
    expect(document.getElementById('combatant-a-select-1')).toBeInTheDocument();
    const removeBtn = document.getElementById('remove-member-a-btn-1')!;
    expect(removeBtn).toBeInTheDocument();

    // Clicking remove should remove member 2
    await userEvent.click(removeBtn);
    expect(document.getElementById('combatant-a-select-1')).not.toBeInTheDocument();
  });

  it('triggers Monte Carlo simulation and renders win rates, metrics, and Party Survival Rate', async () => {
    const mockRun = vi.fn().mockResolvedValue(mockSimulationResult);
    const mockCompleted = vi.fn();

    render(
      <CombatSimulatorView
        creatures={mockCreatures}
        weapons={mockWeapons}
        onRunSimulation={mockRun}
        onSimulationCompleted={mockCompleted}
      />
    );

    const runBtn = screen.getByRole('button', { name: /Simular Combate \(Montecarlo\)/i });
    await userEvent.click(runBtn);

    expect(mockRun).toHaveBeenCalledWith(
      expect.objectContaining({
        teamA: [{ creatureId: 'c-1', weaponId: 'w-1' }],
        teamB: [{ creatureId: 'c-2', weaponId: 'w-2' }],
        targetingStrategy: 'LowestHp',
        iterations: 10000,
      })
    );

    await waitFor(() => {
      expect(document.getElementById('montecarlo-results-panel')).toBeInTheDocument();
    });

    // Check Win Rates
    const winRateA = document.getElementById('win-rate-a');
    const winRateB = document.getElementById('win-rate-b');
    expect(winRateA).toHaveTextContent('32%');
    expect(winRateB).toHaveTextContent('68%');

    // Check Party Survival Rate
    const survivalRate = document.getElementById('metric-party-survival-rate');
    expect(survivalRate).toBeInTheDocument();
    expect(survivalRate).toHaveTextContent('28.5%');

    // Check Other Metric Cards
    const avgRounds = document.getElementById('metric-avg-rounds');
    const dprA = document.getElementById('metric-dpr-a');
    const dprB = document.getElementById('metric-dpr-b');
    const totalIt = document.getElementById('metric-total-iterations');

    expect(avgRounds).toHaveTextContent('4.25');
    expect(dprA).toHaveTextContent('6.8');
    expect(dprB).toHaveTextContent('11.4');
    expect(totalIt?.textContent).toMatch(/10[.,]000/);

    expect(mockCompleted).toHaveBeenCalledWith(mockSimulationResult);
  });

  it('passes selected targeting strategy to simulation payload', async () => {
    const mockRun = vi.fn().mockResolvedValue(mockSimulationResult);

    render(
      <CombatSimulatorView
        creatures={mockCreatures}
        weapons={mockWeapons}
        onRunSimulation={mockRun}
      />
    );

    const targetingSelect = document.getElementById('targeting-strategy-select') as HTMLSelectElement;
    await userEvent.selectOptions(targetingSelect, 'Random');

    const runBtn = screen.getByRole('button', { name: /Simular Combate \(Montecarlo\)/i });
    await userEvent.click(runBtn);

    expect(mockRun).toHaveBeenCalledWith(
      expect.objectContaining({
        targetingStrategy: 'Random',
      })
    );
  });

  it('displays error alert when simulation service throws an error', async () => {
    const mockRun = vi.fn().mockRejectedValue(new Error('Network error'));

    render(
      <CombatSimulatorView
        creatures={mockCreatures}
        weapons={mockWeapons}
        onRunSimulation={mockRun}
      />
    );

    const runBtn = screen.getByRole('button', { name: /Simular Combate \(Montecarlo\)/i });
    await userEvent.click(runBtn);

    await waitFor(() => {
      expect(document.getElementById('sim-error-message')).toBeInTheDocument();
      expect(document.getElementById('sim-error-message')).toHaveTextContent(/error al ejecutar la simulación/i);
    });

    expect(document.getElementById('montecarlo-results-panel')).not.toBeInTheDocument();
  });

  it('opens Custom Hero modal when clicking the Añadir Héroe Custom button', async () => {
    render(
      <CombatSimulatorView
        creatures={mockCreatures}
        weapons={mockWeapons}
        onRunSimulation={vi.fn()}
      />
    );

    const openBtn = document.getElementById('open-custom-hero-btn');
    expect(openBtn).toBeInTheDocument();

    await userEvent.click(openBtn!);

    expect(document.getElementById('custom-hero-modal')).toBeInTheDocument();
  });

  it('runs Monte Carlo simulation for hero with expectedDpr without requiring weapon', async () => {
    const heroCreature: CreatureResponseDto = {
      ...mockCreatures[0],
      id: 'hero-dpr-1',
      name: 'Fighter Hero',
      isPlayerCharacter: true,
      level: 5,
      archetype: 'Fighter 2H',
      expectedDpr: 19.4,
    };

    const mockRun = vi.fn().mockResolvedValue(mockSimulationResult);

    render(
      <CombatSimulatorView
        creatures={[heroCreature, mockCreatures[1]]}
        weapons={mockWeapons}
        onRunSimulation={mockRun}
      />
    );

    // Weapon A select should be disabled and show DPR baseline text
    const weaponASelect = document.getElementById('weapon-a-select') as HTMLSelectElement;
    expect(weaponASelect).toBeDisabled();
    expect(weaponASelect.value).toBe('');

    const runBtn = screen.getByRole('button', { name: /Simular Combate \(Montecarlo\)/i });
    await userEvent.click(runBtn);

    expect(mockRun).toHaveBeenCalledWith(
      expect.objectContaining({
        teamA: [{ creatureId: 'hero-dpr-1', weaponId: undefined }],
        teamB: [{ creatureId: 'c-2', weaponId: 'w-2' }],
      })
    );
  });
});
