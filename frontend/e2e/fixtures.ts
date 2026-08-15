import { test as base, expect, type Page } from '@playwright/test';

/** The seeded development accounts (PDR §0.5). */
export const ACCOUNTS = {
  admin: { email: 'admin@demo.local', password: 'Admin#2026Demo' },
  user: { email: 'user@demo.local', password: 'User#2026Demo' },
} as const;

export const test = base.extend({});

/**
 * Call once at the top of every spec file. It must be called from the spec's own module body,
 * not registered here: an imported module executes only once, so a hook declared at this file's
 * scope would attach to whichever spec happened to load first and silently leave the rest
 * running against a dead API.
 *
 * Skipping rather than failing is the same convention the backend's integration suite uses — see
 * the repository README on why a red run on a machine without Docker is worse than a skipped one.
 */
export function requireApi() {
  test.skip(
    () => process.env.E2E_API_READY !== '1',
    'The API is not reachable — start it with `docker compose up --build`.',
  );
}

export { expect };

/**
 * Signs in through the UI.
 *
 * **Call this once per browser context, not once per test.** Login is rate limited to 10 requests
 * per 5 minutes per IP (PDR §2.3), so a suite that signs in on every test exhausts the budget and
 * starts failing in ways that look like application bugs. The specs share one signed-in context
 * across a serial describe block instead.
 *
 * Reusing a saved `storageState` would be worse, not better: refresh tokens rotate on every use
 * and replaying a rotated one trips server-side reuse detection, which revokes every token for
 * that user (§2.1).
 */
export async function signIn(page: Page, account: keyof typeof ACCOUNTS) {
  const { email, password } = ACCOUNTS[account];

  await page.goto('/login');
  await page.getByLabel('Email').fill(email);
  await page.getByLabel('Password', { exact: false }).first().fill(password);
  await page.getByRole('button', { name: 'Sign in' }).click();

  // Make a tripped rate limit legible instead of a mystery timeout.
  const rateLimited = page.getByRole('alert').filter({ hasText: /too many/i });
  await expect
    .poll(
      async () =>
        (await page.getByRole('heading', { name: 'Products' }).count()) > 0
          ? 'signed-in'
          : (await rateLimited.count()) > 0
            ? 'rate-limited'
            : 'pending',
      { timeout: 15_000 },
    )
    .not.toBe('pending');

  if (await rateLimited.count()) {
    throw new Error(
      'Login is rate limited (10 requests / 5 minutes per IP). Wait for the window to lapse, ' +
        'or reduce the number of sign-ins in this suite.',
    );
  }

  await expect(page.getByRole('heading', { name: 'Products' })).toBeVisible();
}

/** Unique per run, so repeated runs against one database never collide. */
export const uniqueName = (prefix: string) => `${prefix} ${Date.now()}`;
