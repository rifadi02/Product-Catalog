/**
 * Auth state — PDR §2.3, §2.7, §2.8.
 *
 * The context holds the user object and a boot flag. Tokens live in `tokenStore`; nothing in the
 * component tree ever touches them directly.
 */
import { createContext, useCallback, useEffect, useMemo, useRef, useState } from 'react';
import type { ReactNode } from 'react';
import { getMe, login as loginRequest, logout as logoutRequest } from '../api/auth';
import { refreshOnce } from '../api/client';
import type { AuthTokens, LoginRequest, UserSummary } from '../api/types';
import {
  clearTokens,
  getRefreshToken,
  isPlausibleRefreshToken,
  setAccessToken,
  setRefreshToken,
} from './tokenStore';

export interface AuthContextValue {
  user: UserSummary | null;
  /** True until the rehydration sequence settles. Route guards must not redirect while set. */
  isBooting: boolean;
  signIn: (credentials: LoginRequest) => Promise<AuthTokens>;
  signOut: () => Promise<void>;
  /** Local-only teardown, for the session-ended path where the server has already spoken. */
  endSession: () => void;
}

// eslint-disable-next-line react-refresh/only-export-components
export const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<UserSummary | null>(null);
  const [isBooting, setIsBooting] = useState(true);
  const booted = useRef(false);

  /**
   * Boot sequence (§2.7): a page reload keeps the refresh token in localStorage but loses the
   * in-memory access token, so refresh first, then repopulate the user from `/auth/me`.
   *
   * Nothing here redirects. An anonymous visitor is a supported state — `/products` works.
   */
  useEffect(() => {
    // Runs exactly once for the life of the app. StrictMode invokes effects twice in
    // development, and a second refresh would replay a token the first one just rotated —
    // reuse detection would then revoke every token for the user (§2.1).
    if (booted.current) return;
    booted.current = true;

    const stored = getRefreshToken();

    if (!stored) {
      setIsBooting(false);
      return;
    }

    if (!isPlausibleRefreshToken(stored)) {
      // Corrupt storage (§4.5) — a malformed token is a 400, not an expired session. Clear it
      // rather than sending a request whose failure the interceptor would misread.
      clearTokens();
      setIsBooting(false);
      return;
    }

    // Deliberately no cleanup and no `cancelled` guard. This provider lives as long as the app,
    // and StrictMode's simulated unmount would otherwise discard the results of the one run this
    // ref allows — leaving `isBooting` true forever and the user permanently anonymous.
    refreshOnce()
      .then(() => getMe())
      .then((me) => setUser(me))
      .catch(() => {
        clearTokens();
        setUser(null);
      })
      .finally(() => setIsBooting(false));
  }, []);

  const signIn = useCallback(async (credentials: LoginRequest) => {
    const tokens = await loginRequest(credentials);

    setAccessToken(tokens.accessToken);
    setRefreshToken(tokens.refreshToken);
    setUser(tokens.user); // already in the payload — no GET /auth/me here (§2.3)

    return tokens;
  }, []);

  const endSession = useCallback(() => {
    clearTokens();
    setUser(null);
  }, []);

  /**
   * §2.8 — the local cleanup must not depend on the response. Server-side revocation failing is
   * not worth telling the user about; the cleanup below is what ends their session in this
   * browser. Cache clearing and navigation are the caller's job.
   */
  const signOut = useCallback(async () => {
    const token = getRefreshToken();
    try {
      if (token) await logoutRequest(token);
    } catch {
      /* nothing to do, and nothing worth surfacing */
    } finally {
      clearTokens();
      setUser(null);
    }
  }, []);

  const value = useMemo<AuthContextValue>(
    () => ({ user, isBooting, signIn, signOut, endSession }),
    [user, isBooting, signIn, signOut, endSession],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
