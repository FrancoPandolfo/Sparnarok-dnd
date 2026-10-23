import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { LiveCombatTracker } from './LiveCombatTracker';
import { liveCombatHubService } from '../services/liveCombatHubService';
import type {
  LiveCombatDmProjectionDto,
  LiveCombatPlayerProjectionDto,
} from '../types/liveCombat';

vi.mock('../services/apiClient', () => ({
  apiClient: {
    getCreatures: vi.fn().mockResolvedValue([
      {
        id: 'creature-1',
        name: 'Goblin Boss',
        hitPoints: { max: 21 },
        armorClass: { base: 17 },
        effectiveArmorClass: 17,
        challengeRating: '1',
      },
      {
        id: 'creature-2',
        name: 'Bugbear Stalker',
        hitPoints: { max: 27 },
        armorClass: { base: 16 },
        effectiveArmorClass: 16,
        challengeRating: '2',
      },
    ]),
  },
}));

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
    forceAddPartyMemberToCombat: vi.fn(),
    joinEncounter: vi.fn(),
    updateCombatantInitiative: vi.fn(),
    removeCombatant: vi.fn(),
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
    const input = document.getElementById('player-initiative-input') as HTMLInputElement;
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

    const playerInput = document.getElementById('player-initiative-input') as HTMLInputElement;
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

  it('Player Lobby: shows lobby when combat is active and player is not in initiative list', () => {
    const mockParty = {
      id: 'party-1',
      name: 'La Compañía del Dragón',
      ownerUserId: 'dm-1',
      createdAt: new Date().toISOString(),
      members: [],
      partyMembers: [
        {
          id: 'pm-2',
          partyId: 'party-1',
          userId: 'player-user-99',
          role: 'Player' as const,
          inGameName: 'Legolas',
          joinedAt: new Date().toISOString(),
        },
      ],
    };

    const activeEncounterState: LiveCombatPlayerProjectionDto = {
      partyId: 'party-1',
      partyName: 'La Compañía del Dragón',
      status: 'InProgress',
      currentRound: 1,
      currentTurnIndex: 0,
      activeCombatantId: 'monster-1',
      activeCombatantName: 'Orc Chieftain',
      combatants: [
        {
          id: 'monster-1',
          name: 'Orc Chieftain',
          initiative: 15,
          isPlayerCharacter: false,
          healthStatus: 'Sano',
        },
      ],
    };

    render(
      <LiveCombatTracker
        partyId="party-1"
        party={mockParty}
        role="Player"
        combatState={activeEncounterState}
        currentUserId="player-user-99"
      />
    );

    // Should render player combat lobby
    expect(screen.getByText(/Un encuentro está en curso/i)).toBeInTheDocument();
    const joinBtn = screen.getByRole('button', { name: /Unirse al Combate/i });
    expect(joinBtn).toBeInTheDocument();

    // Clicking join opens player join modal
    fireEvent.click(joinBtn);
    expect(screen.getByRole('button', { name: /Entrar al Combate/i })).toBeInTheDocument();

    // Confirming join calls liveCombatHubService.joinEncounter
    fireEvent.click(screen.getByRole('button', { name: /Entrar al Combate/i }));
    expect(liveCombatHubService.joinEncounter).toHaveBeenCalledWith(
      'party-1',
      expect.objectContaining({
        name: 'Legolas',
        assignedPlayerUserId: 'player-user-99',
      })
    );
  });

  it('DM Party Members Panel: lists unjoined members and clicking "+ Añadir al Combate" calls forceAddPartyMemberToCombat', () => {
    const mockParty = {
      id: 'party-1',
      name: 'La Compañía del Dragón',
      ownerUserId: 'dm-1',
      createdAt: new Date().toISOString(),
      members: [],
      partyMembers: [
        {
          id: 'pm-1',
          partyId: 'party-1',
          userId: 'dm-1',
          role: 'DungeonMaster' as const,
          inGameName: 'Dungeon Master',
          joinedAt: new Date().toISOString(),
        },
        {
          id: 'pm-2',
          partyId: 'party-1',
          userId: 'player-user-2',
          role: 'Player' as const,
          inGameName: 'Thorin',
          joinedAt: new Date().toISOString(),
        },
      ],
    };

    render(
      <LiveCombatTracker
        partyId="party-1"
        party={mockParty}
        role="DungeonMaster"
        combatState={null}
      />
    );

    // Open party members panel
    const toggleBtn = screen.getByRole('button', { name: /Miembros de la Party/i });
    expect(toggleBtn).toBeInTheDocument();
    fireEvent.click(toggleBtn);

    // Player Thorin is listed as unjoined
    expect(screen.getByText(/Thorin/i)).toBeInTheDocument();
    const addBtn = screen.getByRole('button', { name: /\+ Añadir al Combate/i });
    expect(addBtn).toBeInTheDocument();

    // Click force add
    fireEvent.click(addBtn);
    expect(liveCombatHubService.forceAddPartyMemberToCombat).toHaveBeenCalledWith(
      'party-1',
      'player-user-2'
    );
  });

  it('Player View: player can edit their own initiative directly on row input (onBlur & Enter) but cannot edit others', () => {
    const twoHeroState: LiveCombatPlayerProjectionDto = {
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
          assignedPlayerUserId: 'player-user-1',
        } as any,
        {
          id: 'hero-2',
          name: 'Thorin Dwarf',
          initiative: 10,
          isPlayerCharacter: true,
          currentHp: 32,
          maxHp: 32,
          healthStatus: 'Healthy',
          assignedPlayerUserId: 'player-user-2',
        } as any,
      ],
    };

    const mockParty = {
      id: 'party-1',
      name: 'La Compañía del Dragón',
      ownerUserId: 'dm-1',
      createdAt: new Date().toISOString(),
      members: [],
      partyMembers: [
        {
          id: 'pm-1',
          partyId: 'party-1',
          userId: 'player-user-1',
          role: 'Player' as const,
          inGameName: 'Kaelen Paladin',
          joinedAt: new Date().toISOString(),
        },
        {
          id: 'pm-2',
          partyId: 'party-1',
          userId: 'player-user-2',
          role: 'Player' as const,
          inGameName: 'Thorin Dwarf',
          joinedAt: new Date().toISOString(),
        },
      ],
    };

    render(
      <LiveCombatTracker
        partyId="party-1"
        party={mockParty}
        role="Player"
        combatState={twoHeroState}
        currentUserId="player-user-1"
      />
    );

    const ownInput = document.getElementById('combatant-initiative-input-hero-1') as HTMLInputElement;
    const otherInput = document.getElementById('combatant-initiative-input-hero-2') as HTMLInputElement;

    expect(ownInput).toBeInTheDocument();
    expect(otherInput).toBeInTheDocument();
    expect(ownInput).not.toBeDisabled();
    expect(otherInput).toBeDisabled();

    // 1. Trigger onBlur update
    fireEvent.change(ownInput, { target: { value: '22' } });
    fireEvent.blur(ownInput);
    expect(liveCombatHubService.updateCombatantInitiative).toHaveBeenCalledWith('party-1', 'hero-1', 22);

    // 2. Trigger onKeyDown Enter update
    fireEvent.change(ownInput, { target: { value: '25' } });
    fireEvent.keyDown(ownInput, { key: 'Enter', code: 'Enter' });
    expect(liveCombatHubService.updateCombatantInitiative).toHaveBeenCalledWith('party-1', 'hero-1', 25);
  });

  it('DM View: displays delete button on each combatant, confirms before calling removeCombatant, button absent in player view', () => {
    const confirmSpy = vi.spyOn(window, 'confirm');

    // Render DM View
    const { unmount } = render(
      <LiveCombatTracker
        partyId="party-1"
        role="DungeonMaster"
        combatState={dmState}
      />
    );

    const deleteBtn = document.getElementById('remove-combatant-btn-hero-1') as HTMLButtonElement;
    expect(deleteBtn).toBeInTheDocument();

    // 1. Cancel confirmation -> should not call removeCombatant
    confirmSpy.mockReturnValueOnce(false);
    fireEvent.click(deleteBtn);
    expect(confirmSpy).toHaveBeenCalled();
    expect(liveCombatHubService.removeCombatant).not.toHaveBeenCalled();

    // 2. Accept confirmation -> should call removeCombatant
    confirmSpy.mockReturnValueOnce(true);
    fireEvent.click(deleteBtn);
    expect(liveCombatHubService.removeCombatant).toHaveBeenCalledWith('party-1', 'hero-1');

    unmount();

    // Render Player View: delete button should NOT be rendered
    render(
      <LiveCombatTracker
        partyId="party-1"
        role="Player"
        combatState={playerState}
      />
    );

    expect(document.getElementById('remove-combatant-btn-hero-1')).toBeNull();

    confirmSpy.mockRestore();
  });

  it('DM View: quick combatant modal offers bestiary autocomplete, autofills stats, allows custom editing, and submits', async () => {
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

    const nameInput = document.getElementById('quick-combatant-name') as HTMLInputElement;
    const hpInput = document.getElementById('quick-combatant-hp') as HTMLInputElement;
    const acInput = document.getElementById('quick-combatant-ac') as HTMLInputElement;

    expect(nameInput).toBeInTheDocument();
    expect(hpInput).toBeInTheDocument();
    expect(acInput).toBeInTheDocument();

    // Type "Goblin" into bestiary combobox
    fireEvent.change(nameInput, { target: { value: 'Goblin' } });

    // Suggestion item for "Goblin Boss" should be visible
    const suggestion = await screen.findByTestId('creature-suggestion-goblin-boss');
    expect(suggestion).toBeInTheDocument();

    // Click suggestion
    fireEvent.click(suggestion);

    // Values should be autofilled from bestiary mock (HP: 21, AC: 17)
    expect(nameInput.value).toBe('Goblin Boss');
    expect(hpInput.value).toBe('21');
    expect(acInput.value).toBe('17');

    // DM customizes / overrides HP to 30
    fireEvent.change(hpInput, { target: { value: '30' } });
    expect(hpInput.value).toBe('30');

    // Submit form
    const submitBtn = document.getElementById('quick-combatant-submit-btn') as HTMLButtonElement;
    fireEvent.click(submitBtn);

    // addQuickCombatant should be called with autofilled & customized values
    expect(liveCombatHubService.addQuickCombatant).toHaveBeenCalledWith(
      'party-1',
      expect.objectContaining({
        name: 'Goblin Boss',
        maxHp: 30,
        armorClass: 17,
        isPlayerCharacter: false,
      })
    );
  });
});


