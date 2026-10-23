import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Navbar } from './Navbar';
import { AuthContext, type AuthContextType } from '../context/AuthContext';
import { GoogleOAuthProvider } from '@react-oauth/google';
import i18n from '../i18n';

describe('Navbar Component Suite', () => {
  beforeEach(async () => {
    await i18n.changeLanguage('es');
  });

  const mockProps = {
    activeTab: 'bestiary' as const,
    onTabChange: vi.fn(),
    onOpenCreateModal: vi.fn(),
    onOpenIngestionModal: vi.fn(),
  };

  const renderWithAuth = (authValue: AuthContextType) => {
    return render(
      <GoogleOAuthProvider clientId="mock-client-id">
        <AuthContext.Provider value={authValue}>
          <Navbar {...mockProps} />
        </AuthContext.Provider>
      </GoogleOAuthProvider>
    );
  };

  it('renders Google sign in button when user is unauthenticated', () => {
    const unauthContext: AuthContextType = {
      user: null,
      token: null,
      isAuthenticated: false,
      isLoading: false,
      loginWithGoogle: vi.fn(),
      loginConventional: vi.fn(),
      registerConventional: vi.fn(),
      setPassword: vi.fn(),
      linkGoogle: vi.fn(),
      logout: vi.fn(),
    };

    renderWithAuth(unauthContext);

    expect(document.getElementById('google-login-btn')).toBeInTheDocument();
    expect(screen.getByText(/Iniciar sesión con Google/i)).toBeInTheDocument();
    expect(document.getElementById('user-profile-badge')).not.toBeInTheDocument();
  });

  it('renders user avatar, display name, and logout button when authenticated', async () => {
    const mockLogout = vi.fn();
    const authContext: AuthContextType = {
      user: {
        id: 'usr-42',
        email: 'aragorn@sparnarok.dnd',
        displayName: 'Aragorn Elessar',
        avatarUrl: 'https://example.com/aragorn.jpg',
        token: 'valid-jwt-token',
      },
      token: 'valid-jwt-token',
      isAuthenticated: true,
      isLoading: false,
      loginWithGoogle: vi.fn(),
      loginConventional: vi.fn(),
      registerConventional: vi.fn(),
      setPassword: vi.fn(),
      linkGoogle: vi.fn(),
      logout: mockLogout,
    };

    renderWithAuth(authContext);

    expect(document.getElementById('user-profile-badge')).toBeInTheDocument();
    expect(screen.getByText('Aragorn Elessar')).toBeInTheDocument();
    const avatarImg = document.getElementById('user-avatar') as HTMLImageElement;
    expect(avatarImg).toBeInTheDocument();
    expect(avatarImg.src).toContain('https://example.com/aragorn.jpg');

    const logoutBtn = document.getElementById('logout-btn')!;
    expect(logoutBtn).toBeInTheDocument();

    await userEvent.click(logoutBtn);
    expect(mockLogout).toHaveBeenCalledTimes(1);
  });
});
