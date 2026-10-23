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
    reorderCombatant: vi.fn(),
    autoRollMonsters: vi.fn(),
    startCombat: vi.fn(),
    nextTurn: vi.fn(),
    previousTurn: vi.fn(),
    modifyHp: vi.fn(),
    toggleVisibility: vi.fn(),
    endCombat: vi.fn(),
    addQuickCombatant: vi.fn(),
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

  it('Drag Handles: renders drag handle (≡) only for DM and never in Player view', () => {
    // 1. Player View
    const { unmount } = render(
      <LiveCombatTracker
        partyId="party-1"
        role="Player"
        combatState={playerState}
      />
    );

    expect(screen.queryByTestId('drag-handle-hero-1')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Reordenar/i })).not.toBeInTheDocument();

    unmount();

    // 2. DM View
    render(
      <LiveCombatTracker
        partyId="party-1"
        role="DungeonMaster"
        combatState={dmState}
      />
    );

    const dmHandleHero = screen.getByTestId('drag-handle-hero-1');
    const dmHandleGoblin = screen.getByTestId('drag-handle-goblin-boss-1');

    expect(dmHandleHero).toBeInTheDocument();
    expect(dmHandleHero).toHaveTextContent('≡');
    expect(dmHandleGoblin).toBeInTheDocument();
    expect(dmHandleGoblin).toHaveTextContent('≡');
  });

  it('Allows clearing initiative without crashing UI and submits null initiative', () => {
    // 1. DM view allows setting initiative to null/empty
    const { unmount } = render(
      <LiveCombatTracker
        partyId="party-1"
        role="DungeonMaster"
        combatState={dmState}
      />
    );

    const editButtons = screen.getAllByRole('button', { name: /✏️/i });
    fireEvent.click(editButtons[0]); // Edit Kaelen's initiative

    const overrideInput = screen.getByLabelText(/Modificar Iniciativa/i);
    fireEvent.change(overrideInput, { target: { value: '' } });

    const confirmBtn = screen.getByRole('button', { name: '✓' });
    fireEvent.click(confirmBtn);

    expect(liveCombatHubService.overrideInitiative).toHaveBeenCalledWith('party-1', 'hero-1', null);

    unmount();

    // 2. Player view allows submitting empty initiative
    render(
      <LiveCombatTracker
        partyId="party-1"
        role="Player"
        combatState={playerState}
      />
    );

    const playerInput = screen.getByRole('spinbutton');
    fireEvent.change(playerInput, { target: { value: '' } });

    const submitBtn = screen.getByRole('button', { name: /Enviar Iniciativa/i });
    fireEvent.click(submitBtn);

    expect(liveCombatHubService.submitInitiative).toHaveBeenCalledWith('party-1', 'hero-1', null);
  });

  it('DM View: opens quick combatant modal and adds combatant on-the-fly without database persistence', async () => {
    render(
      <LiveCombatTracker
        partyId="party-1"
        role="DungeonMaster"
        combatState={dmState}
      />
    );

    // Open quick combatant modal
    const addBtn = screen.getByRole('button', { name: /\+ Añadir Combatiente Rápido/i });
    fireEvent.click(addBtn);

    // Modal is open
    expect(screen.getByRole('dialog', { hidden: true })).toBeInTheDocument();

    // Fill form
    const nameInput = screen.getByLabelText(/Nombre/i);
    fireEvent.change(nameInput, { target: { value: 'Bandido Rápido' } });

    const hpInput = screen.getByLabelText(/Puntos de Golpe/i);
    fireEvent.change(hpInput, { target: { value: '15' } });

    const acInput = screen.getByLabelText(/Clase de Armadura/i);
    fireEvent.change(acInput, { target: { value: '13' } });

    // Submit
    const submitBtn = screen.getByRole('button', { name: /^\+ Añadir$/i });
    fireEvent.click(submitBtn);

    expect(liveCombatHubService.addQuickCombatant).toHaveBeenCalledWith('party-1', {
      name: 'Bandido Rápido',
      maxHp: 15,
      armorClass: 13,
      isPlayerCharacter: false,
      isVisibleToPlayers: true,
      initiative: null,
    });

    // Optimistic addition is visible
    expect(screen.getByText('Bandido Rápido')).toBeInTheDocument();
  });

  it('Empty State: unblocks DM by providing "+ Añadir Combatiente Rápido" button when combatState is null', () => {
    render(
      <LiveCombatTracker
        partyId="quick-session"
        role="DungeonMaster"
        combatState={null}
      />
    );

    // DM should see unblocked action buttons even without an active combat state
    const quickButtons = screen.getAllByRole('button', { name: /\+ Añadir Combatiente Rápido/i });
    expect(quickButtons.length).toBeGreaterThanOrEqual(1);

    // Clicking it opens the quick combatant modal
    fireEvent.click(quickButtons[0]);
    expect(screen.getByLabelText(/Nombre/i)).toBeInTheDocument();
  });
});

