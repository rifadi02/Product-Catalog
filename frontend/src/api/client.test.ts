/**
 * The 401 interceptor — PDR §2.6.
 *
 * Single-flight is not tidiness: the backend revokes the old refresh token on every rotation and
 * detects reuse, so a second concurrent refresh replays a rotated token and destroys *every*
 * session for that user (§2.1.2). These tests exist to keep that from regressing quietly.
 */
import { describe, expect, it, beforeEach } from 'vitest';
import axios, { type AxiosError } from 'axios';
import { http, HttpResponse } from 'msw';
import { api, __resetRefreshLatch, SESSION_ENDED_EVENT } from './client';
import { API, server } from '../test/server';
import { makeAccessToken, makeProblem, makeTokens } from '../test/factories';
import {
  clearTokens,
  getAccessToken,
  getRefreshToken,
  setAccessToken,
  setRefreshToken,
} from '../auth/tokenStore';

const VALID_REFRESH = 'kM3nP9qR7sT1vX4zA6cE8gJ0lN2pS5uW7yB9dF1hK3m';
const ROTATED_REFRESH = 'wY6bD8fH0jL2nQ4sV7xZ9aC1eG3iK5mO7qS9uW1yA3c';

beforeEach(() => {
  clearTokens();
  __resetRefreshLatch();
});

describe('request interceptor', () => {
  it('attaches the bearer token and a correlation id', async () => {
    const token = makeAccessToken();
    setAccessToken(token);

    let seen: Headers | undefined;
    server.use(
      http.get(`${API}/auth/me`, ({ request }) => {
        seen = request.headers;
        return HttpResponse.json({ id: 'x', email: 'a@b.co', role: 'User' });
      }),
    );

    await api.get('/auth/me');

    expect(seen?.get('authorization')).toBe(`Bearer ${token}`);
    expect(seen?.get('x-correlation-id')).toMatch(/[0-9a-f-]{36}/i);
  });

  it('refreshes proactively when the access token is within a minute of expiry', async () => {
    // Clock skew is zero server-side, so there is no grace period to lean on (§2.1.1).
    setAccessToken(makeAccessToken({ expiresInSeconds: 30 }));
    setRefreshToken(VALID_REFRESH);

    const fresh = makeAccessToken({ expiresInSeconds: 900 });
    let refreshCalls = 0;
    let sentAuth: string | null = null;

    server.use(
      http.post(`${API}/auth/refresh`, () => {
        refreshCalls++;
        return HttpResponse.json(
          makeTokens({ accessToken: fresh, refreshToken: ROTATED_REFRESH }),
        );
      }),
      http.get(`${API}/products`, ({ request }) => {
        sentAuth = request.headers.get('authorization');
        return HttpResponse.json({ items: [] });
      }),
    );

    await api.get('/products');

    expect(refreshCalls).toBe(1);
    expect(sentAuth).toBe(`Bearer ${fresh}`);
  });
});

describe('401 handling', () => {
  it('refreshes once for concurrent 401s and replays every request', async () => {
    setAccessToken('expired-token');
    setRefreshToken(VALID_REFRESH);

    let refreshCalls = 0;
    const fresh = makeAccessToken();

    server.use(
      http.post(`${API}/auth/refresh`, async ({ request }) => {
        refreshCalls++;
        const body = (await request.json()) as { refreshToken: string };
        expect(body.refreshToken).toBe(VALID_REFRESH);

        return HttpResponse.json(
          makeTokens({ accessToken: fresh, refreshToken: ROTATED_REFRESH }),
        );
      }),
      http.post(`${API}/products`, ({ request }) => {
        if (request.headers.get('authorization') !== `Bearer ${fresh}`) {
          return HttpResponse.json(makeProblem('token_expired', 401), { status: 401 });
        }
        return HttpResponse.json({ id: 1 }, { status: 201 });
      }),
    );

    const results = await Promise.all([
      api.post('/products', { name: 'a', price: 1 }),
      api.post('/products', { name: 'b', price: 2 }),
      api.post('/products', { name: 'c', price: 3 }),
    ]);

    expect(refreshCalls).toBe(1);
    expect(results.map((r) => r.status)).toEqual([201, 201, 201]);
    // The rotated token MUST be persisted — the old one is revoked the instant refresh responds.
    expect(getRefreshToken()).toBe(ROTATED_REFRESH);
    expect(getAccessToken()).toBe(fresh);
  });

  it('does not refresh a 401 from /auth/login', async () => {
    setRefreshToken(VALID_REFRESH);

    let refreshCalls = 0;
    server.use(
      http.post(`${API}/auth/refresh`, () => {
        refreshCalls++;
        return HttpResponse.json(makeTokens());
      }),
      http.post(`${API}/auth/login`, () =>
        HttpResponse.json(makeProblem('invalid_credentials', 401), { status: 401 }),
      ),
    );

    await expect(api.post('/auth/login', {})).rejects.toMatchObject({
      response: { status: 401 },
    });
    expect(refreshCalls).toBe(0);
    // A failed sign-in must not clear an existing session's refresh token.
    expect(getRefreshToken()).toBe(VALID_REFRESH);
  });

  it('gives up after one retry and ends the session', async () => {
    setAccessToken('expired-token');
    setRefreshToken(VALID_REFRESH);

    let attempts = 0;
    let sessionEnded = 0;
    const onEnded = () => sessionEnded++;
    window.addEventListener(SESSION_ENDED_EVENT, onEnded);

    server.use(
      http.post(`${API}/auth/refresh`, () =>
        HttpResponse.json(makeTokens({ accessToken: makeAccessToken() })),
      ),
      // Still 401 even with a fresh token — the retried request must not loop.
      http.get(`${API}/auth/me`, () => {
        attempts++;
        return HttpResponse.json(makeProblem('invalid_token', 401), { status: 401 });
      }),
    );

    await expect(api.get('/auth/me')).rejects.toBeDefined();
    window.removeEventListener(SESSION_ENDED_EVENT, onEnded);

    expect(attempts).toBe(2); // original + one replay
    expect(sessionEnded).toBe(0); // the refresh itself succeeded, so the session is not over
  });

  it('clears tokens and announces the end of the session when refresh is rejected', async () => {
    setAccessToken('expired-token');
    setRefreshToken(VALID_REFRESH);

    let sessionEnded = 0;
    const onEnded = () => sessionEnded++;
    window.addEventListener(SESSION_ENDED_EVENT, onEnded);

    server.use(
      // Unknown, expired, revoked, or already-rotated — one shape for all four (§2.5).
      http.post(`${API}/auth/refresh`, () =>
        HttpResponse.json(makeProblem('refresh_token_invalid', 401), { status: 401 }),
      ),
      http.get(`${API}/auth/me`, () =>
        HttpResponse.json(makeProblem('token_expired', 401), { status: 401 }),
      ),
    );

    await expect(api.get('/auth/me')).rejects.toBeDefined();
    window.removeEventListener(SESSION_ENDED_EVENT, onEnded);

    expect(sessionEnded).toBe(1);
    expect(getRefreshToken()).toBeNull();
    expect(getAccessToken()).toBeNull();
  });

  it('does not attempt a refresh when no refresh token is stored', async () => {
    setAccessToken('expired-token');

    let refreshCalls = 0;
    server.use(
      http.post(`${API}/auth/refresh`, () => {
        refreshCalls++;
        return HttpResponse.json(makeTokens());
      }),
      http.get(`${API}/auth/me`, () =>
        HttpResponse.json(makeProblem('missing_token', 401), { status: 401 }),
      ),
    );

    await expect(api.get('/auth/me')).rejects.toBeDefined();
    expect(refreshCalls).toBe(0);
  });

  it('does not refresh a corrupt stored refresh token', async () => {
    // 16–512 characters or the DTO returns 400, not 401 — retrying it forever is worse (§4.5).
    setAccessToken('expired-token');
    setRefreshToken('too-short');

    let refreshCalls = 0;
    server.use(
      http.post(`${API}/auth/refresh`, () => {
        refreshCalls++;
        return HttpResponse.json(makeTokens());
      }),
      http.get(`${API}/auth/me`, () =>
        HttpResponse.json(makeProblem('token_expired', 401), { status: 401 }),
      ),
    );

    await expect(api.get('/auth/me')).rejects.toBeDefined();
    expect(refreshCalls).toBe(0);
    expect(getRefreshToken()).toBeNull();
  });
});

describe('non-401 responses', () => {
  it('passes a 403 straight through without refreshing or logging out', async () => {
    setAccessToken(makeAccessToken());
    setRefreshToken(VALID_REFRESH);

    let refreshCalls = 0;
    server.use(
      http.post(`${API}/auth/refresh`, () => {
        refreshCalls++;
        return HttpResponse.json(makeTokens());
      }),
      http.delete(`${API}/products/42`, () =>
        HttpResponse.json(makeProblem('insufficient_role', 403), { status: 403 }),
      ),
    );

    await expect(api.delete('/products/42')).rejects.toMatchObject({
      response: { status: 403 },
    });

    expect(refreshCalls).toBe(0);
    expect(getRefreshToken()).toBe(VALID_REFRESH); // a permission problem is not an auth problem
  });

  it('leaves a network failure as a network failure', async () => {
    setAccessToken(makeAccessToken());
    setRefreshToken(VALID_REFRESH);

    server.use(http.get(`${API}/products`, () => HttpResponse.error()));

    // No `response` means no problem document to parse — it must not fall through to the 401
    // handler and log the user out (§1.5).
    const error = await api.get('/products').catch((e: unknown) => e);

    expect(axios.isAxiosError(error)).toBe(true);
    expect((error as AxiosError).response).toBeUndefined();
    expect(getRefreshToken()).toBe(VALID_REFRESH);
  });
});
