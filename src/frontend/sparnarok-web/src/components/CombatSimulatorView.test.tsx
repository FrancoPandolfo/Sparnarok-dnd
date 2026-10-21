import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { CombatSimulatorView } from './CombatSimulatorView';
import i18n from '../i18n';
import type { CreatureResponseDto, WeaponDto, MontecarloResultDto } from '../types/dnd';

describe('CombatSimulatorView Component Suite', () => {
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
    combatantAName: 'Bugbear Stalker',
    combatantBName: 'Gorgon Vanguard',
    weaponAName: 'Longsword',
    weaponBName: 'Greatsword',
    executionTimeMs: 12,
  };

  it('renders combatant cards, weapon dropdowns, and run button', () => {
    const mockRun = vi.fn();
    render(
      <CombatSimulatorView
        creatures={mockCreatures}
        weapons={mockWeapons}
        onRunSimulation={mockRun}
      />
    );

    expect(screen.getByText(/Simulador de Combate de Montecarlo \(1v1\)/i)).toBeInTheDocument();
    expect(screen.getByText(/Combatiente A \(Iniciador\)/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Simular Combate \(Montecarlo\)/i })).toBeInTheDocument();
    expect(document.getElementById('combatant-a-select')).toBeInTheDocument();
    expect(document.getElementById('combatant-b-select')).toBeInTheDocument();
    expect(document.getElementById('iterations-select')).toBeInTheDocument();
  });

  it('triggers Monte Carlo simulation and renders win rates and metrics', async () => {
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

    expect(mockRun).toHaveBeenCalledWith({
      combatantAId: 'c-1',
      weaponAId: 'w-1',
      combatantBId: 'c-2',
      weaponBId: 'w-2',
      iterations: 10000,
    });

    await waitFor(() => {
      expect(document.getElementById('montecarlo-results-panel')).toBeInTheDocument();
    });

    // Check Win Rates
    const winRateA = document.getElementById('win-rate-a');
    const winRateB = document.getElementById('win-rate-b');
    expect(winRateA).toHaveTextContent('32%');
    expect(winRateB).toHaveTextContent('68%');

    // Check Metric Cards
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
});

