import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { PartyOnboardingModal } from './PartyOnboardingModal';
import { apiClient } from '../services/apiClient';
import * as AuthContextModule from '../context/AuthContext';

describe('PartyOnboardingModal Component Suite', () => {
  const mockPartyDetails = {
    partyId: 'party-123',
    partyName: 'La Marca del Dragón',
    inviteToken: 'invite-token-abc',
    memberCount: 4,
  };

  const mockJoinedParty = {
    id: 'party-123',
    name: 'La Marca del Dragón',
    ownerUserId: 'dm-user-1',
    createdAt: new Date().toISOString(),
    members: [],
  };

  beforeEach(() => {
    vi.restoreAllMocks();
    vi.spyOn(AuthContextModule, 'useAuth').mockReturnValue({
      user: {
        id: 'player-user-1',
        email: 'elendur@middleearth.com',
        displayName: 'Elendur',
        globalNickname: 'ElendurElValiente',
        hasPassword: true,
        hasGoogleLinked: false,
        token: 'jwt-player-token',
      },
      token: 'jwt-player-token',
      isAuthenticated: true,
      isLoading: false,
      loginConventional: vi.fn(),
      registerConventional: vi.fn(),
      loginWithGoogle: vi.fn(),
      setPassword: vi.fn(),
      linkGoogle: vi.fn(),
      updateProfile: vi.fn(),
      logout: vi.fn(),
    });
  });

  it('fetches party preview and submits onboarding with chosen Role and InGameName', async () => {
    const getInviteDetailsSpy = vi
      .spyOn(apiClient, 'getPartyInviteDetails')
      .mockResolvedValue(mockPartyDetails);

    const joinPartyByTokenSpy = vi
      .spyOn(apiClient, 'joinPartyByToken')
      .mockResolvedValue(mockJoinedParty);

    const onJoinedSuccess = vi.fn();
    const onClose = vi.fn();

    render(
      <PartyOnboardingModal
        isOpen={true}
        inviteToken="invite-token-abc"
        onClose={onClose}
        onJoinedSuccess={onJoinedSuccess}
      />
    );

    // Debe cargar los detalles de la invitación
    expect(getInviteDetailsSpy).toHaveBeenCalledWith('invite-token-abc');

    await waitFor(() => {
      expect(screen.getByText('La Marca del Dragón')).toBeInTheDocument();
      expect(screen.getByText('4')).toBeInTheDocument();
    });

    const roleSelect = document.getElementById('onboarding-role-select') as HTMLSelectElement;
    expect(roleSelect).toBeInTheDocument();
    expect(roleSelect.value).toBe('Player');

    const inGameNameInput = document.getElementById('onboarding-ingame-name') as HTMLInputElement;
    expect(inGameNameInput).toBeInTheDocument();
    // Debe venir pre-llenado con el globalNickname
    expect(inGameNameInput.value).toBe('ElendurElValiente');

    // Cambiar nombre de personaje
    fireEvent.change(inGameNameInput, { target: { value: 'Elendur (Explorador)' } });

    // Hacer clic en Unirse
    const joinBtn = document.getElementById('confirm-join-party-btn')!;
    await userEvent.click(joinBtn);

    expect(joinPartyByTokenSpy).toHaveBeenCalledWith('invite-token-abc', {
      role: 'Player',
      inGameName: 'Elendur (Explorador)',
    });

    await waitFor(() => {
      expect(screen.getByText(/¡Te has unido exitosamente/)).toBeInTheDocument();
    });
  });

  it('allows switching role to DungeonMaster', async () => {
    vi.spyOn(apiClient, 'getPartyInviteDetails').mockResolvedValue(mockPartyDetails);
    const joinPartyByTokenSpy = vi
      .spyOn(apiClient, 'joinPartyByToken')
      .mockResolvedValue(mockJoinedParty);

    render(
      <PartyOnboardingModal
        isOpen={true}
        inviteToken="invite-token-abc"
        onClose={vi.fn()}
        onJoinedSuccess={vi.fn()}
      />
    );

    await waitFor(() => {
      expect(screen.getByText('La Marca del Dragón')).toBeInTheDocument();
    });

    const roleSelect = document.getElementById('onboarding-role-select') as HTMLSelectElement;
    await userEvent.selectOptions(roleSelect, 'DungeonMaster');
    expect(roleSelect.value).toBe('DungeonMaster');

    const joinBtn = document.getElementById('confirm-join-party-btn')!;
    await userEvent.click(joinBtn);

    expect(joinPartyByTokenSpy).toHaveBeenCalledWith('invite-token-abc', {
      role: 'DungeonMaster',
      inGameName: 'ElendurElValiente',
    });
  });
});
