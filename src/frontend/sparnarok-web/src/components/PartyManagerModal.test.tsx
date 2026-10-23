import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { PartyManagerModal } from './PartyManagerModal';
import { apiClient } from '../services/apiClient';
import type { PartyResponseDto } from '../types/partyEncounter';

vi.mock('../services/apiClient', () => ({
  apiClient: {
    createParty: vi.fn(),
    deleteParty: vi.fn(),
  },
}));

describe('PartyManagerModal Component Suite', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  const mockParties: PartyResponseDto[] = [
    {
      id: 'party-alpha',
      name: 'La Compañía del Alba',
      ownerUserId: 'user-1',
      inviteToken: 'invite-alpha-token',
      createdAt: '2026-10-23T10:00:00Z',
      members: [],
      partyMembers: [
        {
          id: 'pm-1',
          partyId: 'party-alpha',
          userId: 'user-1',
          role: 'DungeonMaster',
          inGameName: 'El Gran DM',
          joinedAt: '2026-10-23T10:00:00Z',
        },
      ],
    },
    {
      id: 'party-beta',
      name: 'Los Héroes de Neverwinter',
      ownerUserId: 'user-2',
      inviteToken: 'invite-beta-token',
      createdAt: '2026-10-22T10:00:00Z',
      members: [],
      partyMembers: [
        {
          id: 'pm-2',
          partyId: 'party-beta',
          userId: 'user-1',
          role: 'Player',
          inGameName: 'Kaelen Paladin',
          joinedAt: '2026-10-22T10:00:00Z',
        },
      ],
    },
  ];

  it('renders party list, badges, and copy invite buttons', () => {
    render(
      <PartyManagerModal
        isOpen={true}
        onClose={vi.fn()}
        parties={mockParties}
        currentUserId="user-1"
      />
    );

    expect(screen.getByText('La Compañía del Alba')).toBeInTheDocument();
    expect(screen.getByText('Los Héroes de Neverwinter')).toBeInTheDocument();
    expect(screen.getByText('Creador')).toBeInTheDocument();

    const copyButtons = screen.getAllByRole('button', { name: /Copiar Enlace/i });
    expect(copyButtons.length).toBe(2);
  });

  it('allows creating a new party with role and in-game name and triggers onPartyCreated', async () => {
    const onPartyCreated = vi.fn();
    const createdMock: PartyResponseDto = {
      id: 'party-new',
      name: 'Escuadrón Fénix',
      ownerUserId: 'user-1',
      inviteToken: 'invite-fenix',
      createdAt: new Date().toISOString(),
      members: [],
      partyMembers: [
        {
          id: 'pm-new',
          partyId: 'party-new',
          userId: 'user-1',
          role: 'DungeonMaster',
          inGameName: 'Master Franco',
          joinedAt: new Date().toISOString(),
        },
      ],
    };

    vi.mocked(apiClient.createParty).mockResolvedValue(createdMock);

    render(
      <PartyManagerModal
        isOpen={true}
        onClose={vi.fn()}
        parties={mockParties}
        currentUserId="user-1"
        currentUserDisplayName="Master Franco"
        onPartyCreated={onPartyCreated}
      />
    );

    // Click "+ Crear Nueva Party"
    const toggleBtn = screen.getByRole('button', { name: /\+ Crear Nueva Party/i });
    fireEvent.click(toggleBtn);

    // Form inputs
    const nameInput = screen.getByLabelText(/Nombre de la Party/i);
    fireEvent.change(nameInput, { target: { value: 'Escuadrón Fénix' } });

    const roleSelect = screen.getByLabelText(/Mi Rol Inicial/i);
    fireEvent.change(roleSelect, { target: { value: 'DungeonMaster' } });

    const nickInput = screen.getByLabelText(/Mi Apodo \/ Nombre en Juego/i);
    fireEvent.change(nickInput, { target: { value: 'Master Franco' } });

    // Submit
    const submitBtn = screen.getByRole('button', { name: /Crear Party/i });
    fireEvent.click(submitBtn);

    await waitFor(() => {
      expect(apiClient.createParty).toHaveBeenCalledWith({
        name: 'Escuadrón Fénix',
        memberCreatureIds: [],
        role: 'DungeonMaster',
        inGameName: 'Master Franco',
      });
      expect(onPartyCreated).toHaveBeenCalledWith(createdMock);
    });
  });

  it('copies invite link to clipboard with feedback', () => {
    const writeTextSpy = vi.fn();
    Object.assign(navigator, {
      clipboard: {
        writeText: writeTextSpy,
      },
    });

    render(
      <PartyManagerModal
        isOpen={true}
        onClose={vi.fn()}
        parties={mockParties}
        currentUserId="user-1"
      />
    );

    const copyBtn = screen.getAllByRole('button', { name: /Copiar Enlace/i })[0];
    fireEvent.click(copyBtn);

    expect(writeTextSpy).toHaveBeenCalledWith(
      expect.stringContaining('?inviteToken=invite-alpha-token')
    );
    expect(screen.getByText('✓ Enlace Copiado')).toBeInTheDocument();
  });

  it('allows owner to delete a party with confirmation', async () => {
    const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(true);
    const onPartyDeleted = vi.fn();
    vi.mocked(apiClient.deleteParty).mockResolvedValue();

    render(
      <PartyManagerModal
        isOpen={true}
        onClose={vi.fn()}
        parties={mockParties}
        currentUserId="user-1"
        onPartyDeleted={onPartyDeleted}
      />
    );

    const deleteBtn = screen.getByRole('button', { name: /Eliminar/i });
    fireEvent.click(deleteBtn);

    expect(confirmSpy).toHaveBeenCalled();
    await waitFor(() => {
      expect(apiClient.deleteParty).toHaveBeenCalledWith('party-alpha');
      expect(onPartyDeleted).toHaveBeenCalledWith('party-alpha');
    });
  });
});
