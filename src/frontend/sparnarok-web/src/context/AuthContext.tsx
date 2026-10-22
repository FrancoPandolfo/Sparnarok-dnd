import React, { createContext, useContext, useState, useEffect, useCallback } from 'react';
import type { AuthUser, AuthResponseDto } from '../types/auth';
import { apiClient } from '../services/apiClient';

export interface AuthContextType {
  user: AuthUser | null;
  token: string | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  loginWithGoogle: (idToken: string) => Promise<AuthResponseDto>;
  logout: () => void;
}

const STORAGE_KEY = 'sparnarok_auth';

export const AuthContext = createContext<AuthContextType | undefined>(undefined);

export interface AuthProviderProps {
  children: React.ReactNode;
}

/**
 * Proveedor global de contexto de autenticación e identidad de usuario (Google OAuth2 + JWT).
 */
export const AuthProvider: React.FC<AuthProviderProps> = ({ children }) => {
  const [user, setUser] = useState<AuthUser | null>(null);
  const [isLoading, setIsLoading] = useState<boolean>(true);

  // Restauración de sesión persistida en almacenamiento local
  useEffect(() => {
    try {
      const stored = localStorage.getItem(STORAGE_KEY);
      if (stored) {
        const parsed: AuthUser = JSON.parse(stored);
        if (parsed && parsed.token && (parsed.id || (parsed as any).userId)) {
          const normalizedUser: AuthUser = {
            id: parsed.id || (parsed as any).userId,
            email: parsed.email || '',
            displayName: parsed.displayName || 'Aventurero',
            avatarUrl: parsed.avatarUrl ?? null,
            token: parsed.token,
          };
          apiClient.setAuthToken(normalizedUser.token);
          setUser(normalizedUser);
        }
      }
    } catch {
      localStorage.removeItem(STORAGE_KEY);
    } finally {
      setIsLoading(false);
    }
  }, []);

  /**
   * Intercambia el idToken de Google por un JWT de Sparnarok y persiste la sesión.
   */
  const loginWithGoogle = useCallback(async (idToken: string): Promise<AuthResponseDto> => {
    setIsLoading(true);
    try {
      const response = await apiClient.loginWithGoogle(idToken);
      const authUser: AuthUser = {
        id: response.userId,
        email: response.email,
        displayName: response.displayName,
        avatarUrl: response.avatarUrl ?? null,
        token: response.token,
      };
      apiClient.setAuthToken(response.token);
      localStorage.setItem(STORAGE_KEY, JSON.stringify(authUser));
      setUser(authUser);
      return response;
    } finally {
      setIsLoading(false);
    }
  }, []);

  /**
   * Cierra la sesión activa y purga las credenciales del almacenamiento local.
   */
  const logout = useCallback(() => {
    localStorage.removeItem(STORAGE_KEY);
    apiClient.setAuthToken(null);
    setUser(null);
  }, []);

  const value: AuthContextType = {
    user,
    token: user?.token ?? null,
    isAuthenticated: Boolean(user && user.token),
    isLoading,
    loginWithGoogle,
    logout,
  };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
};

/**
 * Hook personalizado para acceder al estado y acciones de autenticación.
 */
export function useAuth(): AuthContextType {
  const context = useContext(AuthContext);
  if (!context) {
    return {
      user: null,
      token: null,
      isAuthenticated: false,
      isLoading: false,
      loginWithGoogle: async () => ({
        token: 'mock-token',
        userId: '00000000-0000-0000-0000-000000000001',
        email: 'mock@sparnarok.dnd',
        displayName: 'Aventurero del Códice',
        avatarUrl: null,
      }),
      logout: () => {},
    };
  }
  return context;
}

