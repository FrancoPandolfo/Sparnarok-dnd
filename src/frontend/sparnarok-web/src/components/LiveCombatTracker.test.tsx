import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { LiveCombatTracker } from './LiveCombatTracker';
import { liveCombatHubService } from '../services/liveCombatHubService';
import type {
  LiveCombatDmProjectionDto,
  LiveCombatPlayerProjectionDto,
} from '../types/liveCombat';

vi.mock('../services/liveCombatHubService', () => ({
  liveCombatHubService: {
    submitInitiative: vi.fn(),
    overrideInitiative: vi.fn(),
    autoRollMonsters: vi.fn(),
    startCombat: vi.fn(),
    nextTurn: vi.fn(),
    previousTurn: vi.fn(),
    modifyHp: vi.fn(),
    toggleVisibility: vi.fn(),
    endCombat: vi.fn(),
  },
}));

describe('LiveCombatTracker Component Suite', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  const playerState: LiveCombatPlayerProjectionDto = {
    partyId: 'party-1',
    partyName: 'La Compañía del Dragón',
    status: 'WaitingForInitiative',
    currentRound: 1,
    currentTurnIndex: 0,
    activeCombatantId: 'hero-1',
    activeCombatantName: 'Kaelen Paladin',
    combatants: [
      {
        id: 'hero-1',
        name: 'Kaelen Paladin',
        initiative: 14,
        isPlayerCharacter: true,
        currentHp: 28,
        maxHp: 28,
        healthStatus: 'Healthy',
      },
      // Note: Goblin Boss is NOT present in player projection (censored at backend)
    ],
  };

  const dmState: LiveCombatDmProjectionDto = {
    partyId: 'party-1',
    partyName: 'La Compañía del Dragón',
    status: 'WaitingForInitiative',
    currentRound: 1,
    currentTurnIndex: 0,
    activeCombatantId: 'hero-1',
    activeCombatantName: 'Kaelen Paladin',
    combatants: [
      {
        id: 'hero-1',
        name: 'Kaelen Paladin',
        initiative: 14,
        maxHp: 28,
        currentHp: 28,
        armorClass: 18,
        isPlayerCharacter: true,
        isVisibleToPlayers: true,
      },
      {
        id: 'goblin-boss-1',
        name: 'Goblin Boss',
        initiative: 12,
        maxHp: 21,
        currentHp: 21,
        armorClass: 17,
        isPlayerCharacter: false,
        isVisibleToPlayers: false, // Hidden
      },
    ],
  };

  it('Player View: renders hero, allows initiative submission, and does not show monster AC or hidden monsters', () => {
    render(
      <LiveCombatTracker
        partyId="party-1"
        role="Player"
        combatState={playerState}
      />
    );

    // Assert hero is rendered
    expect(screen.getByText('Kaelen Paladin')).toBeInTheDocument();
    // Assert hidden monster does not exist in player view
    expect(screen.queryByText('Goblin Boss')).not.toBeInTheDocument();
    // Assert no AC column header in player view
    expect(screen.queryByText('CA')).not.toBeInTheDocument();

    // Input own initiative and submit
    const input = screen.getByRole('spinbutton');
    fireEvent.change(input, { target: { value: '18' } });
    const submitBtn = screen.getByRole('button', { name: /Enviar Iniciativa/i });
    fireEvent.click(submitBtn);

    expect(liveCombatHubService.submitInitiative).toHaveBeenCalledWith('party-1', 'hero-1', 18);
  });

  it('DM View: renders full roster including hidden monsters, AC and master control buttons', () => {
    render(
      <LiveCombatTracker
        partyId="party-1"
        role="DungeonMaster"
        combatState={dmState}
      />
    );

    // Both hero and goblin boss are visible to the DM
    expect(screen.getByText('Kaelen Paladin')).toBeInTheDocument();
    expect(screen.getByText('Goblin Boss')).toBeInTheDocument();
    expect(screen.getByText('🛡️ 18')).toBeInTheDocument();
    expect(screen.getByText('🛡️ 17')).toBeInTheDocument();

    // DM master buttons
    expect(screen.getByRole('button', { name: /Auto-Roll Monstruos/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Iniciar Combate/i })).toBeInTheDocument();

    // Trigger auto-roll
    fireEvent.click(screen.getByRole('button', { name: /Auto-Roll Monstruos/i }));
    expect(liveCombatHubService.autoRollMonsters).toHaveBeenCalledWith('party-1');

    // Trigger start combat
    fireEvent.click(screen.getByRole('button', { name: /Iniciar Combate/i }));
    expect(liveCombatHubService.startCombat).toHaveBeenCalledWith('party-1');
  });

  it('DM View: allows quick HP modification and visibility toggle', () => {
    render(
      <LiveCombatTracker
        partyId="party-1"
        role="DungeonMaster"
        combatState={dmState}
      />
    );

    // Click -5 HP on goblin boss
    const minusFiveButtons = screen.getAllByTitle('-5 HP');
    fireEvent.click(minusFiveButtons[1]); // second combatant is goblin boss
    expect(liveCombatHubService.modifyHp).toHaveBeenCalledWith('party-1', 'goblin-boss-1', -5);

    // Click visibility toggle
    const toggleBtn = screen.getByRole('button', { name: /Oculto/i });
    fireEvent.click(toggleBtn);
    expect(liveCombatHubService.toggleVisibility).toHaveBeenCalledWith('party-1', 'goblin-boss-1', true);
  });
});
