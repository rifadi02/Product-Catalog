/** Register page — PDR §3.3, §4.2. */
import { beforeEach, describe, expect, it } from 'vitest';
import { http, HttpResponse } from 'msw';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { RegisterPage } from './RegisterPage';
import { __resetRefreshLatch } from '../api/client';
import { clearTokens, getRefreshToken } from '../auth/tokenStore';
import { API, server } from '../test/server';
import { makeProblem } from '../test/factories';
import { renderWithUser } from '../test/render';

beforeEach(() => {
  clearTokens();
  __resetRefreshLatch();
});

async function fillForm(password = 'Str0ngPassw0rd', confirm = password) {
  await userEvent.type(screen.getByLabelText(/^email/i), 'newuser@example.com');
  await userEvent.type(screen.getByLabelText(/^password/i), password);
  await userEvent.type(screen.getByLabelText(/confirm password/i), confirm);
  await userEvent.click(screen.getByRole('button', { name: 'Create account' }));
}

describe('RegisterPage', () => {
  it('does not log the user in on success', async () => {
    // The 201 contains no tokens at all (§2.4).
    server.use(
      http.post(`${API}/auth/register`, () =>
        HttpResponse.json(
          {
            id: '0198c4a2-9b1e-7f04-a6d3-5c8e1b2f7a90',
            email: 'newuser@example.com',
            role: 'User',
            createdAt: '2026-08-15T09:41:12.482Z',
          },
          { status: 201, headers: { Location: '/api/v1/users/0198c4a2-9b1e-7f04-a6d3-5c8e1b2f7a90' } },
        ),
      ),
    );

    renderWithUser(<RegisterPage />, { route: '/register' });
    await fillForm();

    await waitFor(() => expect(getRefreshToken()).toBeNull());
  });

  it('ticks each password rule as it is satisfied', async () => {
    renderWithUser(<RegisterPage />, { route: '/register' });

    // Showing the rules up front avoids the wall of chained messages the server would return
    // (§3.3).
    expect(screen.getByText('One uppercase letter')).toHaveClass('text-ink-500');

    await userEvent.type(screen.getByLabelText(/^password/i), 'A');
    expect(screen.getByText('One uppercase letter')).toHaveClass('text-emerald-700');
    expect(screen.getByText('One digit')).toHaveClass('text-ink-500');

    await userEvent.type(screen.getByLabelText(/^password/i), 'bcdefg1');
    expect(screen.getByText('One digit')).toHaveClass('text-emerald-700');
    expect(screen.getByText('At least 8 characters')).toHaveClass('text-emerald-700');
  });

  it('maps a 409 onto the email field, which carries no errors bag', async () => {
    server.use(
      http.post(`${API}/auth/register`, () =>
        HttpResponse.json(makeProblem('email_already_registered', 409), { status: 409 }),
      ),
    );

    renderWithUser(<RegisterPage />, { route: '/register' });
    await fillForm();

    expect(
      await screen.findByText('An account with this email already exists.'),
    ).toBeInTheDocument();
    expect(screen.getByLabelText(/^email/i)).toHaveAttribute('aria-invalid', 'true');
  });

  it('renders every chained password message from the server', async () => {
    server.use(
      http.post(`${API}/auth/register`, () =>
        HttpResponse.json(
          makeProblem('validation_failed', 400, {
            errors: {
              password: [
                'Password must contain at least one uppercase letter.',
                'Password must contain at least one digit.',
              ],
            },
          }),
          { status: 400 },
        ),
      ),
    );

    renderWithUser(<RegisterPage />, { route: '/register' });
    await fillForm();

    expect(
      await screen.findByText(
        'Password must contain at least one uppercase letter. Password must contain at least one digit.',
      ),
    ).toBeInTheDocument();
  });

  it('rejects a denylisted password before sending it', async () => {
    // No handler registered — a request here would fail the suite.
    renderWithUser(<RegisterPage />, { route: '/register' });
    await fillForm('Password123');

    expect(
      await screen.findByText('Password is too common. Choose something less guessable.'),
    ).toBeInTheDocument();
  });

  it('reports a mismatch under the confirm field', async () => {
    renderWithUser(<RegisterPage />, { route: '/register' });
    await fillForm('Str0ngPassw0rd', 'Different1');

    expect(await screen.findByText('Passwords do not match.')).toBeInTheDocument();
  });

  it('counts down when the tight register rate limit trips', async () => {
    server.use(
      http.post(`${API}/auth/register`, () =>
        HttpResponse.json(makeProblem('rate_limited', 429), {
          status: 429,
          headers: { 'Retry-After': '45' },
        }),
      ),
    );

    renderWithUser(<RegisterPage />, { route: '/register' });
    await fillForm();

    // 5 per 15 minutes — easy to trip while testing (§3.3).
    expect(await screen.findByText(/try again in 45s/i)).toBeInTheDocument();
  });
});
