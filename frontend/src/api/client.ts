/**
 * The single configured axios instance and its one interceptor pair — PDR §2.6.
 *
 * Deliberately free of React imports: the terminal-failure path dispatches a DOM event and the
 * app root turns that into a cache clear and a redirect.
 */
import axios, { type AxiosError, type InternalAxiosRequestConfig } from 'axios';
import {
  clearTokens,
  getAccessToken,
  getRefreshToken,
  isPlausibleRefreshToken,
  setAccessToken,
  setRefreshToken,
} from '../auth/tokenStore';
import { secondsUntilExpiry } from '../auth/jwt';
import type { AuthTokens, ProblemDetails } from './types';

export const SESSION_ENDED_EVENT = 'auth:session-ended';

export const api = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL,
  headers: { 'Content-Type': 'application/json' },
});

// Bare instance for the refresh call — using `api` here would recurse through the interceptor.
const refreshClient = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL,
  headers: { 'Content-Type': 'application/json' },
});

type Retryable = InternalAxiosRequestConfig & { _retried?: boolean };

let inFlight: Promise<string> | null = null; // the single-flight latch

/**
 * Exactly one refresh at a time, shared by every caller — including the boot sequence in
 * AuthContext. Firing two races them: one wins, the other replays a now-rotated token, the
 * backend's reuse detection fires, and *every* session for that user dies (§2.1).
 */
export function refreshOnce(): Promise<string> {
  if (inFlight) return inFlight; // everybody waits on the same promise

  const stored = getRefreshToken();
  if (!isPlausibleRefreshToken(stored)) {
    // Either absent, or corrupt storage (§4.5) — a malformed value returns 400, not 401, and
    // retrying it forever would be worse than ending the session here.
    clearTokens();
    return Promise.reject(new Error('no_refresh_token'));
  }

  inFlight = refreshClient
    .post<AuthTokens>('/auth/refresh', { refreshToken: stored })
    .then(({ data }) => {
      setAccessToken(data.accessToken);
      setRefreshToken(data.refreshToken); // MUST overwrite — the old one is now revoked
      return data.accessToken;
    })
    .finally(() => {
      inFlight = null;
    });

  return inFlight;
}

/** Test seam: drop the latch between cases so one test's promise cannot leak into the next. */
export function __resetRefreshLatch() {
  inFlight = null;
}

/** Refresh this many seconds before expiry rather than waiting for the 401 (§2.6). */
const PROACTIVE_REFRESH_WINDOW_SECONDS = 60;

api.interceptors.request.use(async (config) => {
  // Allowed by CORS and echoed back as the problem document's `traceId`, which makes a browser
  // error and a Serilog scope share one id (§0.4).
  config.headers['X-Correlation-Id'] ??= crypto.randomUUID();

  let token = getAccessToken();

  // Optional refinement from §2.6: clock skew is zero server-side, so a token expires at exactly
  // 15 minutes. Refreshing just before that removes a user-visible stall. The reactive 401 path
  // below still exists for clock drift and suspended tabs.
  const remaining = secondsUntilExpiry(token);
  if (token && remaining !== null && remaining < PROACTIVE_REFRESH_WINDOW_SECONDS) {
    try {
      token = await refreshOnce();
    } catch {
      /* fall through with the stale token; the 401 handler takes it from here */
    }
  }

  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

// A 401 from either of these is terminal — /auth/login means bad credentials, /auth/refresh means
// the refresh token itself is dead. Refreshing in response to either is a loop.
const TERMINAL_PATHS = ['/auth/login', '/auth/refresh'];

api.interceptors.response.use(
  (response) => response,
  async (error: AxiosError<ProblemDetails>) => {
    const original = error.config as Retryable | undefined;
    const problem = error.response?.data;

    if (axios.isCancel(error)) throw error; // our own aborted request — silent (§1.2)
    if (!error.response || !original) throw error; // network / CORS failure (§1.5)
    if (error.response.status !== 401) throw error; // 403 and friends pass through
    if (TERMINAL_PATHS.some((p) => original.url?.includes(p))) throw error;
    if (original._retried) throw error; // one retry, then give up
    if (problem?.code === 'invalid_credentials') throw error;

    try {
      const fresh = await refreshOnce();
      original._retried = true;
      original.headers.Authorization = `Bearer ${fresh}`;

      // Returned, deliberately not awaited: the replay's own rejection must not fall into the
      // catch below. A second 401 is terminal for *that request* (the `_retried` guard above
      // rethrows it), but the refresh succeeded, so the session itself is not over.
      return api(original);
    } catch {
      clearTokens();
      window.dispatchEvent(new CustomEvent(SESSION_ENDED_EVENT));
      throw error;
    }
  },
);
