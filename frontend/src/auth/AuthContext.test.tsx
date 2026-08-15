/** The boot sequence and sign-out — PDR §2.7, §2.8. */
import { beforeEach, describe, expect, it } from 'vitest';
import { http, HttpResponse } from 'msw';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Route, Routes } from 'react-router-dom';
import { __resetRefreshLatch } from '../api/client';
import { clearTokens, getRefreshToken, setRefreshToken } from './tokenStore';
import { useAuth } from './useAuth';
import { RequireCapability } from './RequireCapability';
import { API, server } from '../test/server';
import { makeProblem, makeTokens } from '../test/factories';
import { renderApp } from '../test/render';

const VALID_REFRESH = 'kM3nP9qR7sT1vX4zA6cE8gJ0lN2pS5uW7yB9dF1hK3m';
const ROTATED_REFRESH = 'wY6bD8fH0jL2nQ4sV7xZ9aC1eG3iK5mO7qS9uW1yA3c';

beforeEach(() => {
  clearTokens();
  __resetRefreshLatch();
});

function Probe() {
  const { user, isBooting } = useAuth();

  if (isBooting) return <p>booting</p>;
  return <p>{user ? `signed in as ${user.email}` : 'anonymous'}</p>;
}

describe('boot sequence', () => {
  it('settles as anonymous when no refresh token is stored', async () => {
    renderApp(<Probe />);
    // Render public routes; /products still works anonymously (§2.7).
    expect(await screen.findByText('anonymous')).toBeInTheDocument();
  });

  it('rehydrates the session from a stored refresh token', async () => {
    setRefreshToken(VALID_REFRESH);

    server.use(
      http.post(`${API}/auth/refresh`, () =>
        HttpResponse.json(makeTokens({ refreshToken: ROTATED_REFRESH })),
      ),
      http.get(`${API}/auth/me`, () =>
        HttpResponse.json({
          id: '0198c3f1-4a2b-7c3d-8e9f-0a1b2c3d4e5f',
          email: 'user@demo.local',
          role: 'User',
        }),
      ),
    );

    renderApp(<Probe />);

    expect(await screen.findByText('signed in as user@demo.local')).toBeInTheDocument();
    expect(getRefreshToken()).toBe(ROTATED_REFRESH);
  });

  it('clears a refresh token the server rejects', async () => {
    setRefreshToken(VALID_REFRESH);

    server.use(
      http.post(`${API}/auth/refresh`, () =>
        HttpResponse.json(makeProblem('refresh_token_invalid', 401), { status: 401 }),
      ),
    );

    renderApp(<Probe />);

    expect(await screen.findByText('anonymous')).toBeInTheDocument();
    await waitFor(() => expect(getRefreshToken()).toBeNull());
  });

  it('rehydrates under StrictMode, which is how the app actually renders', async () => {
    // Regression: the boot effect used to discard its own results when StrictMode's simulated
    // unmount fired, leaving isBooting true forever and every reload anonymous — while the
    // non-strict tests above passed. Caught by the end-to-end suite, pinned here.
    setRefreshToken(VALID_REFRESH);

    let refreshCalls = 0;
    server.use(
      http.post(`${API}/auth/refresh`, () => {
        refreshCalls++;
        return HttpResponse.json(makeTokens({ refreshToken: ROTATED_REFRESH }));
      }),
      http.get(`${API}/auth/me`, () =>
        HttpResponse.json({ id: 'x', email: 'user@demo.local', role: 'User' }),
      ),
    );

    renderApp(<Probe />, { strict: true });

    expect(await screen.findByText('signed in as user@demo.local')).toBeInTheDocument();
    // And still exactly one refresh: a second would replay a rotated token (§2.1).
    expect(refreshCalls).toBe(1);
  });

  it('does not call the API for a corrupt stored token', async () => {
    // Outside 16–512 characters the DTO returns a 400, not a 401 (§4.5). No handler is
    // registered, so any request here would fail the suite's onUnhandledRequest: 'error'.
    setRefreshToken('short');

    renderApp(<Probe />);

    expect(await screen.findByText('anonymous')).toBeInTheDocument();
    expect(getRefreshToken()).toBeNull();
  });
});

describe('route guards during boot', () => {
  it('waits for rehydration instead of bouncing a reload to /login', async () => {
    setRefreshToken(VALID_REFRESH);

    let releaseRefresh: (() => void) | undefined;
    const refreshGate = new Promise<void>((resolve) => {
      releaseRefresh = resolve;
    });

    server.use(
      http.post(`${API}/auth/refresh`, async () => {
        await refreshGate;
        return HttpResponse.json(makeTokens({ refreshToken: ROTATED_REFRESH }));
      }),
      http.get(`${API}/auth/me`, () =>
        HttpResponse.json({
          id: '0198c3f1-4a2b-7c3d-8e9f-0a1b2c3d4e5f',
          email: 'user@demo.local',
          role: 'User',
        }),
      ),
    );

    renderApp(
      <Routes>
        <Route
          path="/products/new"
          element={
            <RequireCapability capability="write">
              <p>create form</p>
            </RequireCapability>
          }
        />
        <Route path="/login" element={<p>login page</p>} />
      </Routes>,
      { route: '/products/new' },
    );

    // Mid-rehydration: neither the form nor a redirect.
    expect(await screen.findByText(/restoring your session/i)).toBeInTheDocument();
    expect(screen.queryByText('login page')).not.toBeInTheDocument();

    releaseRefresh?.();

    expect(await screen.findByText('create form')).toBeInTheDocument();
  });

  it('redirects an anonymous visitor once boot has settled', async () => {
    renderApp(
      <Routes>
        <Route
          path="/products/new"
          element={
            <RequireCapability capability="write">
              <p>create form</p>
            </RequireCapability>
          }
        />
        <Route path="/login" element={<p>login page</p>} />
      </Routes>,
      { route: '/products/new' },
    );

    expect(await screen.findByText('login page')).toBeInTheDocument();
  });

  it('shows a forbidden page rather than a redirect when the role is wrong', async () => {
    setRefreshToken(VALID_REFRESH);

    server.use(
      http.post(`${API}/auth/refresh`, () => HttpResponse.json(makeTokens())),
      http.get(`${API}/auth/me`, () =>
        HttpResponse.json({
          id: '0198c3f1-4a2b-7c3d-8e9f-0a1b2c3d4e5f',
          email: 'user@demo.local',
          role: 'User',
        }),
      ),
    );

    renderApp(
      <Routes>
        <Route
          path="/admin-only"
          element={
            <RequireCapability capability="delete">
              <p>admin tools</p>
            </RequireCapability>
          }
        />
        <Route path="/login" element={<p>login page</p>} />
      </Routes>,
      { route: '/admin-only' },
    );

    // Signed in but wrong role: 403 territory, not 401 (§5.2).
    expect(await screen.findByText(/don’t have access/i)).toBeInTheDocument();
    expect(screen.queryByText('login page')).not.toBeInTheDocument();
  });
});

describe('sign out', () => {
  function SignOutProbe() {
    const { user, signOut } = useAuth();
    return (
      <div>
        <p>{user ? 'signed in' : 'signed out'}</p>
        <button onClick={() => void signOut()}>Sign out</button>
      </div>
    );
  }

  it('clears local state even when the server call fails', async () => {
    setRefreshToken(VALID_REFRESH);

    server.use(
      http.post(`${API}/auth/refresh`, () => HttpResponse.json(makeTokens())),
      http.get(`${API}/auth/me`, () =>
        HttpResponse.json({ id: 'x', email: 'user@demo.local', role: 'User' }),
      ),
      // Server-side revocation failing is not worth telling the user about; the local cleanup is
      // what ends their session in this browser (§2.8).
      http.post(`${API}/auth/logout`, () =>
        HttpResponse.json(makeProblem('internal_error', 500), { status: 500 }),
      ),
    );

    renderApp(<SignOutProbe />);
    expect(await screen.findByText('signed in')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Sign out' }));

    expect(await screen.findByText('signed out')).toBeInTheDocument();
    expect(getRefreshToken()).toBeNull();
  });
});
