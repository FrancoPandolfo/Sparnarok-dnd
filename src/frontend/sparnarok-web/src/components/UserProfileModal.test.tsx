import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { GoogleOAuthProvider } from '@react-oauth/google';
import { UserProfileModal } from './UserProfileModal';
import * as AuthContextModule from '../context/AuthContext';

describe('UserProfileModal Component Suite', () => {
  const updateProfileMock = vi.fn();
  const setPasswordMock = vi.fn();
  const linkGoogleMock = vi.fn();
  const logoutMock = vi.fn();

  beforeEach(() => {
    vi.restoreAllMocks();
    updateProfileMock.mockReset();
    vi.spyOn(AuthContextModule, 'useAuth').mockReturnValue({
      user: {
        id: 'user-xyz',
        email: 'hero@dnd.com',
        displayName: 'Aventurero Principal',
        globalNickname: 'GandalfElGris',
        hasPassword: true,
        hasGoogleLinked: false,
        token: 'jwt-token',
      },
      token: 'jwt-token',
      isAuthenticated: true,
      isLoading: false,
      loginConventional: vi.fn(),
      registerConventional: vi.fn(),
      loginWithGoogle: vi.fn(),
      setPassword: setPasswordMock,
      linkGoogle: linkGoogleMock,
      updateProfile: updateProfileMock,
      logout: logoutMock,
    });
  });

  it('renders Mi Perfil section with initial global nickname and saves changes', async () => {
    updateProfileMock.mockResolvedValue({
      id: 'user-xyz',
      email: 'hero@dnd.com',
      displayName: 'Aventurero Principal',
      globalNickname: 'Mithrandir',
      hasPassword: true,
      hasGoogleLinked: false,
    });

    render(
      <GoogleOAuthProvider clientId="mock-test-client-id">
        <UserProfileModal isOpen={true} onClose={vi.fn()} />
      </GoogleOAuthProvider>
    );

    expect(screen.getByText('👤 Mi Perfil (Identidad Global)')).toBeInTheDocument();

    const nicknameInput = document.getElementById('global-nickname-input') as HTMLInputElement;
    expect(nicknameInput).toBeInTheDocument();
    expect(nicknameInput.value).toBe('GandalfElGris');

    fireEvent.change(nicknameInput, { target: { value: 'Mithrandir' } });

    const saveNicknameBtn = document.getElementById('save-global-nickname-btn')!;
    await userEvent.click(saveNicknameBtn);

    expect(updateProfileMock).toHaveBeenCalledWith('Mithrandir');

    await waitFor(() => {
      expect(screen.getByText(/¡Apodo global actualizado correctamente!/)).toBeInTheDocument();
    });
  });
});
