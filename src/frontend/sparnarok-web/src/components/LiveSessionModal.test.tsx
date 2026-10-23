import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { LiveSessionModal } from './LiveSessionModal';
import type { PartyResponseDto } from '../types/partyEncounter';

vi.mock('../services/liveCombatHubService', () => ({
  liveCombatHubService: {
    startConnection: vi.fn(),
    stopConnection: vi.fn(),
    initializeCombat: vi.fn(),
    addQuickCombatant: vi.fn(),
  },
}));

vi.mock('./SessionNotesEditor', () => ({
  SessionNotesEditor: ({ partyId }: { partyId: string }) => (
    <div data-testid="session-notes-editor-mock">Mock Notes Editor for {partyId}</div>
  ),
}));

vi.mock('./LiveCombatTracker', () => ({
  LiveCombatTracker: ({ partyId, role }: { partyId: string; role: string }) => (
    <div data-testid="live-combat-tracker-mock">
      Tracker for {partyId} as {role}
    </div>
  ),
}));

describe('LiveSessionModal Component Suite', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  const mockParties: PartyResponseDto[] = [
    {
      id: 'party-1',
      name: 'La Compañía del Dragón',
      ownerUserId: 'dm-user-1',
      inviteToken: 'invite-12345',
      createdAt: new Date().toISOString(),
      members: [],
      partyMembers: [
        {
          id: 'pm-1',
          partyId: 'party-1',
          userId: 'dm-user-1',
          role: 'DungeonMaster',
          inGameName: 'Dungeon Master',
          joinedAt: new Date().toISOString(),
        },
        {
          id: 'pm-2',
          partyId: 'party-1',
          userId: 'player-user-2',
          role: 'Player',
          inGameName: 'Thorin',
          joinedAt: new Date().toISOString(),
        },
      ],
    },
  ];

  it('Player Role: "Mis Notas Privadas" tab is visible and accessible for role Player', () => {
    render(
      <LiveSessionModal
        isOpen={true}
        onClose={vi.fn()}
        parties={mockParties}
        activePartyId="party-1"
        currentUserId="player-user-2"
        initialRole="Player"
      />
    );

    // Verify notes tab exists and is labeled "Mis Notas Privadas"
    const notesTab = screen.getByRole('tab', { name: /Mis Notas Privadas/i });
    expect(notesTab).toBeInTheDocument();

    // Click notes tab and verify SessionNotesEditor is rendered
    fireEvent.click(notesTab);
    expect(screen.getByTestId('session-notes-editor-mock')).toBeInTheDocument();
  });

  it('Quick Session Mode: allows selecting "Sin Party (Modo Rápido)" and defaults to DM role', () => {
    render(
      <LiveSessionModal
        isOpen={true}
        onClose={vi.fn()}
        parties={mockParties}
        currentUserId="player-user-2"
      />
    );

    // Find party select dropdown
    const select = screen.getByLabelText(/Party:/i) as HTMLSelectElement;
    expect(select).toBeInTheDocument();

    // Select "Sin Party (Modo Rápido)"
    fireEvent.change(select, { target: { value: 'quick-session' } });
    expect(select.value).toBe('quick-session');

    // Role badge should switch to Dungeon Master
    const roleBadge = screen.getByText(/Dungeon Master/i);
    expect(roleBadge).toBeInTheDocument();

    // Live combat tracker is rendered for quick session
    expect(screen.getByText(/Tracker for quick-session as DungeonMaster/i)).toBeInTheDocument();
  });

  it('Invite Link: allows copying invite link when party has an inviteToken', () => {
    const writeTextSpy = vi.fn();
    Object.assign(navigator, {
      clipboard: {
        writeText: writeTextSpy,
      },
    });

    render(
      <LiveSessionModal
        isOpen={true}
        onClose={vi.fn()}
        parties={mockParties}
        activePartyId="party-1"
        currentUserId="dm-user-1"
        initialRole="DungeonMaster"
      />
    );

    const inviteBtn = screen.getByRole('button', { name: /Invitar Jugadores/i });
    expect(inviteBtn).toBeInTheDocument();

    fireEvent.click(inviteBtn);
    expect(writeTextSpy).toHaveBeenCalledWith(
      expect.stringContaining('?inviteToken=invite-12345')
    );
  });
});
