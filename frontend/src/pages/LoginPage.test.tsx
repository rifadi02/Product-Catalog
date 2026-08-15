/** Login page — PDR §3.2. */
import { beforeEach, describe, expect, it } from 'vitest';
import { http, HttpResponse } from 'msw';
import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { LoginPage } from './LoginPage';
import { __resetRefreshLatch } from '../api/client';
import { clearTokens, getRefreshToken } from '../auth/tokenStore';
import { API, server } from '../test/server';
import { makeProblem, makeTokens } from '../test/factories';
import { renderApp } from '../test/render';

beforeEach(() => {
  clearTokens();
  __resetRefreshLatch();
});

async function signIn(email = 'user@demo.local', password = 'User#2026Demo') {
  await userEvent.type(screen.getByLabelText(/email/i), email);
  await userEvent.type(screen.getByLabelText(/password/i), password);
  await userEvent.click(screen.getByRole('button', { name: 'Sign in' }));
}

describe('LoginPage', () => {
  it('stores both tokens on success without a follow-up /auth/me call', async () => {
    // The login response already contains the full user object (§2.3). A handler for /auth/me is
    // deliberately absent: the suite fails any unhandled request.
    server.use(http.post(`${API}/auth/login`, () => HttpResponse.json(makeTokens())));

    renderApp(<LoginPage />, { route: '/login' });
    await signIn();

    await expect
      .poll(() => getRefreshToken())
      .toBe('kM3nP9qR7sT1vX4zA6cE8gJ0lN2pS5uW7yB9dF1hK3m');
  });

  it('shows one form-level message for bad credentials, never a field-level one', async () => {
    server.use(
      http.post(`${API}/auth/login`, () =>
        HttpResponse.json(makeProblem('invalid_credentials', 401), { status: 401 }),
      ),
    );

    renderApp(<LoginPage />, { route: '/login' });
    await signIn('nobody@example.com', 'wrong-password');

    // The server deliberately returns an identical 401 for a wrong password and an unknown
    // email, so the UI must not imply which one it was (§3.2).
    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Email or password is incorrect.');
    expect(screen.getByLabelText(/email/i)).not.toHaveAttribute('aria-invalid');
  });

  it('maps a 400 errors bag onto the fields', async () => {
    server.use(
      http.post(`${API}/auth/login`, () =>
        HttpResponse.json(
          makeProblem('validation_failed', 400, {
            errors: { email: ['Email is required.'], password: ['Password is required.'] },
          }),
          { status: 400 },
        ),
      ),
    );

    renderApp(<LoginPage />, { route: '/login' });
    // Client validation is only an affordance, so submit something it accepts and let the server
    // be authoritative (§4 preamble).
    await signIn('a@b.co', 'x');

    expect(await screen.findByText('Email is required.')).toBeInTheDocument();
    expect(screen.getByText('Password is required.')).toBeInTheDocument();
  });

  it('counts down from Retry-After on a 429 and keeps submit disabled', async () => {
    server.use(
      http.post(`${API}/auth/login`, () =>
        HttpResponse.json(makeProblem('rate_limited', 429), {
          status: 429,
          headers: { 'Retry-After': '30' },
        }),
      ),
    );

    renderApp(<LoginPage />, { route: '/login' });
    await signIn();

    expect(await screen.findByText(/try again in 30s/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /wait 30s/i })).toBeDisabled();
  });

  it('says the server is unreachable rather than blaming the credentials', async () => {
    server.use(http.post(`${API}/auth/login`, () => HttpResponse.error()));

    renderApp(<LoginPage />, { route: '/login' });
    await signIn();

    expect(await screen.findByRole('alert')).toHaveTextContent(/can't reach the server/i);
  });

  it('validates client-side before sending anything', async () => {
    // No handler registered — a request here would fail the suite.
    renderApp(<LoginPage />, { route: '/login' });

    await userEvent.click(screen.getByRole('button', { name: 'Sign in' }));

    expect(await screen.findByText('Email is required.')).toBeInTheDocument();
    expect(screen.getByText('Password is required.')).toBeInTheDocument();
  });

  it('explains an expired session when redirected there by the interceptor', async () => {
    renderApp(<LoginPage />, { route: '/login?reason=expired' });

    expect(
      await screen.findByText(/your session has ended\. please sign in again\./i),
    ).toBeInTheDocument();
  });

  it('confirms a successful registration', async () => {
    renderApp(<LoginPage />, { route: '/login?registered=1' });

    expect(await screen.findByText(/account created\. please sign in\./i)).toBeInTheDocument();
  });
});
