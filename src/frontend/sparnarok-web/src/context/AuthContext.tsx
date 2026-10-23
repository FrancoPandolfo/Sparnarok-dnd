import React, { createContext, useContext, useState, useEffect, useCallback } from 'react';
import type { AuthUser, AuthResponseDto } from '../types/auth';
import { apiClient } from '../services/apiClient';

export interface AuthContextType {
  user: AuthUser | null;
  token: string | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  loginWithGoogle: (idToken: string) => Promise<AuthResponseDto>;
  loginConventional: (email: string, password: string) => Promise<AuthResponseDto>;
  registerConventional: (email: string, password: string, displayName: string) => Promise<AuthResponseDto>;
  setPassword: (newPassword: string) => Promise<AuthResponseDto>;
  linkGoogle: (idToken: string) => Promise<AuthResponseDto>;
  updateProfile: (globalNickname: string | null) => Promise<void>;
  logout: () => void;
}

const STORAGE_KEY = 'sparnarok_auth';

export const AuthContext = createContext<AuthContextType | undefined>(undefined);

export interface AuthProviderProps {
  children: React.ReactNode;
}

/**
 * Proveedor global de contexto de autenticación e identidad de usuario (Google OAuth2 + Convencional).
 */
export const AuthProvider: React.FC<AuthProviderProps> = ({ children }) => {
  const [user, setUser] = useState<AuthUser | null>(null);
  const [isLoading, setIsLoading] = useState<boolean>(true);

  const saveAuthSession = useCallback((response: AuthResponseDto) => {
    const authUser: AuthUser = {
      id: response.userId,
      email: response.email,
      displayName: response.displayName,
      globalNickname: response.globalNickname ?? null,
      avatarUrl: response.avatarUrl ?? null,
      token: response.token,
      hasPassword: response.hasPassword,
      hasGoogleLinked: response.hasGoogleLinked,
    };
    apiClient.setAuthToken(response.token);
    localStorage.setItem(STORAGE_KEY, JSON.stringify(authUser));
    setUser(authUser);
  }, []);

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
            globalNickname: parsed.globalNickname ?? null,
            avatarUrl: parsed.avatarUrl ?? null,
            token: parsed.token,
            hasPassword: parsed.hasPassword ?? false,
            hasGoogleLinked: parsed.hasGoogleLinked ?? false,
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
   * Inicia sesión o registra mediante Google OAuth2.
   */
  const loginWithGoogle = useCallback(async (idToken: string): Promise<AuthResponseDto> => {
    setIsLoading(true);
    try {
      const response = await apiClient.loginWithGoogle(idToken);
      saveAuthSession(response);
      return response;
    } finally {
      setIsLoading(false);
    }
  }, [saveAuthSession]);

  /**
   * Inicia sesión con credenciales convencionales (email y contraseña).
   */
  const loginConventional = useCallback(async (email: string, password: string): Promise<AuthResponseDto> => {
    setIsLoading(true);
    try {
      const response = await apiClient.loginConventional({ email, password });
      saveAuthSession(response);
      return response;
    } finally {
      setIsLoading(false);
    }
  }, [saveAuthSession]);

  /**
   * Registra una nueva cuenta convencional con email y contraseña.
   */
  const registerConventional = useCallback(async (email: string, password: string, displayName: string): Promise<AuthResponseDto> => {
    setIsLoading(true);
    try {
      const response = await apiClient.registerConventional({ email, password, displayName });
      saveAuthSession(response);
      return response;
    } finally {
      setIsLoading(false);
    }
  }, [saveAuthSession]);

  /**
   * Configura o actualiza la contraseña del usuario logueado.
   */
  const setPassword = useCallback(async (newPassword: string): Promise<AuthResponseDto> => {
    setIsLoading(true);
    try {
      const response = await apiClient.setPassword({ newPassword });
      saveAuthSession(response);
      return response;
    } finally {
      setIsLoading(false);
    }
  }, [saveAuthSession]);

  /**
   * Vincula la cuenta de Google al usuario autenticado.
   */
  const linkGoogle = useCallback(async (idToken: string): Promise<AuthResponseDto> => {
    setIsLoading(true);
    try {
      const response = await apiClient.linkGoogle(idToken);
      saveAuthSession(response);
      return response;
    } finally {
      setIsLoading(false);
    }
  }, [saveAuthSession]);

  /**
   * Cierra la sesión activa y purga las credenciales del almacenamiento local.
   */
  const logout = useCallback(() => {
    localStorage.removeItem(STORAGE_KEY);
    apiClient.setAuthToken(null);
    setUser(null);
  }, []);

  /**
   * Actualiza el apodo o alias global del usuario (Fase 3.7).
   */
  const updateProfile = useCallback(async (globalNickname: string | null): Promise<void> => {
    setIsLoading(true);
    try {
      const res = await apiClient.updateProfile({ globalNickname });
      setUser((prev) => {
        if (!prev) return null;
        const updated: AuthUser = {
          ...prev,
          globalNickname: res.globalNickname ?? null,
        };
        localStorage.setItem(STORAGE_KEY, JSON.stringify(updated));
        return updated;
      });
    } finally {
      setIsLoading(false);
    }
  }, []);

  const value: AuthContextType = {
    user,
    token: user?.token ?? null,
    isAuthenticated: Boolean(user && user.token),
    isLoading,
    loginWithGoogle,
    loginConventional,
    registerConventional,
    setPassword,
    linkGoogle,
    updateProfile,
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
        hasPassword: true,
        hasGoogleLinked: false,
      }),
      loginConventional: async () => ({
        token: 'mock-token',
        userId: '00000000-0000-0000-0000-000000000001',
        email: 'mock@sparnarok.dnd',
        displayName: 'Aventurero del Códice',
        avatarUrl: null,
        hasPassword: true,
        hasGoogleLinked: false,
      }),
      registerConventional: async () => ({
        token: 'mock-token',
        userId: '00000000-0000-0000-0000-000000000001',
        email: 'mock@sparnarok.dnd',
        displayName: 'Aventurero del Códice',
        avatarUrl: null,
        hasPassword: true,
        hasGoogleLinked: false,
      }),
      setPassword: async () => ({
        token: 'mock-token',
        userId: '00000000-0000-0000-0000-000000000001',
        email: 'mock@sparnarok.dnd',
        displayName: 'Aventurero del Códice',
        avatarUrl: null,
        hasPassword: true,
        hasGoogleLinked: true,
      }),
      linkGoogle: async () => ({
        token: 'mock-token',
        userId: '00000000-0000-0000-0000-000000000001',
        email: 'mock@sparnarok.dnd',
        displayName: 'Aventurero del Códice',
        avatarUrl: null,
        hasPassword: true,
        hasGoogleLinked: true,
      }),
      updateProfile: async () => {},
      logout: () => {},
    };
  }
  return context;
}
