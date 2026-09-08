import { useCallback, useEffect, useMemo, useState } from 'react';
import type { ReactNode } from 'react';
import { api, jsonBody, setAccessToken, setUnauthorizedHandler } from '../api/client';
import type { AuthResult, AuthenticatedUser } from '../api/types';
import { AuthContext } from './AuthContext';
import type { AuthContextValue } from './AuthContext';

const USER_KEY = 'neverorder.user';

function readStoredUser(): AuthenticatedUser | null {
  const raw = sessionStorage.getItem(USER_KEY);
  return raw ? (JSON.parse(raw) as AuthenticatedUser) : null;
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthenticatedUser | null>(readStoredUser);

  const applyResult = useCallback((result: AuthResult) => {
    setAccessToken(result.accessToken);
    sessionStorage.setItem(USER_KEY, JSON.stringify(result.user));
    setUser(result.user);
  }, []);

  const logout = useCallback(() => {
    setAccessToken(null);
    sessionStorage.removeItem(USER_KEY);
    setUser(null);
  }, []);

  useEffect(() => {
    setUnauthorizedHandler(() => {
      sessionStorage.removeItem(USER_KEY);
      setUser(null);
    });
  }, []);

  const login = useCallback(
    async (email: string, password: string) => {
      applyResult(
        await api<AuthResult>('/api/auth/login', {
          method: 'POST',
          body: jsonBody({ email, password }),
        }),
      );
    },
    [applyResult],
  );

  const register = useCallback(
    async (name: string, email: string, password: string) => {
      applyResult(
        await api<AuthResult>('/api/auth/register', {
          method: 'POST',
          body: jsonBody({ name, email, password }),
        }),
      );
    },
    [applyResult],
  );

  const value = useMemo<AuthContextValue>(
    () => ({ user, isAuthenticated: user !== null, login, register, logout }),
    [user, login, register, logout],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
