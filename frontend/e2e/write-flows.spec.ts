/**
 * Create, edit, delete, and the role boundary between User and Admin (PDR §3.6, §3.7, §5.2).
 *
 * Each describe block signs in **once** and shares the context across its tests. Login is rate
 * limited to 10 requests per 5 minutes per IP (§2.3), so one sign-in per test exhausts the budget
 * partway through the file and produces failures that look like application bugs.
 */
import type { Browser, BrowserContext, Page } from '@playwright/test';
import { expect, requireApi, signIn, test, uniqueName } from './fixtures';

requireApi();

async function signedInPage(browser: Browser, account: 'admin' | 'user') {
  const context = await browser.newContext();
  const page = await context.newPage();
  await signIn(page, account);
  return { context, page };
}

/** The first product link in the table, whatever the current sort produced. */
const firstProductLink = (page: Page) => page.getByRole('table').getByRole('link').first();

test.describe('as a standard user', () => {
  test.describe.configure({ mode: 'serial' });

  let context: BrowserContext;
  let page: Page;
  let productPath = '';

  test.beforeAll(async ({ browser }) => {
    ({ context, page } = await signedInPage(browser, 'user'));
  });

  test.afterAll(async () => {
    await context?.close();
  });

  test('creates a product', async () => {
    const name = uniqueName('E2E Desk Lamp');

    await page.getByRole('link', { name: '+ New product' }).click();
    await page.getByLabel('Name').fill(name);
    await page.getByLabel('Description').fill('Created by the end-to-end suite.');
    await page.getByLabel('Price').fill('24.99');
    await page.getByRole('button', { name: 'Create product' }).click();

    // The 201 carries the id in its body, which is what the redirect uses (§3.6.1).
    await expect(page).toHaveURL(/\/products\/\d+$/);
    await expect(page.getByRole('heading', { level: 1 })).toContainText(name);
    await expect(page.getByText(/Rp\s?24,99/)).toBeVisible();

    productPath = new URL(page.url()).pathname;
  });

  test('edits the product it just created', async () => {
    // Editing needs the ETag from the detail GET, which the create flow's redirect captured
    // (§3.6.1, §3.6.3).
    await page.goto(productPath);
    await page.getByRole('link', { name: 'Edit' }).click();

    await page.getByLabel('Price').fill('19.95');
    await page.getByRole('button', { name: 'Save changes' }).click();

    await expect(page).toHaveURL(new RegExp(`${productPath}$`));
    await expect(page.getByText(/Rp\s?19,95/)).toBeVisible();
    // updatedAt is null until the first successful PUT, so this row appears only now (§3.5).
    await expect(page.getByText('Last updated')).toBeVisible();
  });

  test('rounds a price to the precision the server stores', async () => {
    await page.goto(`${productPath}/edit`);

    await page.getByLabel('Price').fill('2.355');
    await page.getByLabel('Name').click(); // blur

    // Banker's rounding, matching the server's decimal arithmetic (§4.3).
    await expect(page.getByLabel('Price')).toHaveValue('2.36');
  });

  test('accepts a genuine zero price', async () => {
    const name = uniqueName('E2E Zero Cost');

    await page.goto('/products/new');
    await page.getByLabel('Name').fill(name);
    await page.getByLabel('Price').fill('0');
    await page.getByRole('button', { name: 'Create product' }).click();

    // A Rp 0,00 product is a legitimate entry, not an empty field (§4.3).
    await expect(page.getByText(/Rp\s?0,00/)).toBeVisible();
  });

  test('is offered no delete control anywhere', async () => {
    // DELETE is Admin-only: this user would get a 403, not a 401 (§5.2).
    await page.goto('/products');
    await expect(page.getByRole('button', { name: /^Delete / })).toHaveCount(0);

    await firstProductLink(page).click();
    await expect(page).toHaveURL(/\/products\/\d+$/);
    await expect(page.getByRole('link', { name: 'Edit' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Delete', exact: true })).toHaveCount(0);
  });

  test('survives a reload on a guarded route', async () => {
    await page.goto('/products/new');

    // Wait for the boot sequence to settle before reloading. `goto` resolves on the load event,
    // not when in-flight requests finish, and reloading mid-refresh aborts the response *after*
    // the server has already rotated the token — the client never learns the replacement, so the
    // next boot replays a revoked one and reuse detection ends the session (§2.1). That race is
    // inherent to a refresh token delivered in a response body; it is not what this test is for.
    await expect(page.getByRole('button', { name: 'Sign out' })).toBeVisible();

    await page.reload();

    // The refresh token survives in localStorage; the access token does not, so this only works
    // if the boot sequence rehydrates before the guard decides (§2.2, §2.7, §5.6).
    await expect(page.getByRole('heading', { name: 'New product' })).toBeVisible();
    await expect(page).toHaveURL(/\/products\/new$/);
  });
});

test.describe('as an admin', () => {
  test.describe.configure({ mode: 'serial' });

  let context: BrowserContext;
  let page: Page;

  test.beforeAll(async ({ browser }) => {
    ({ context, page } = await signedInPage(browser, 'admin'));
  });

  test.afterAll(async () => {
    await context?.close();
  });

  test('reports the role on a read-only profile page', async () => {
    await page.getByRole('link', { name: 'Profile' }).click();

    await expect(page.locator('#main').getByText('admin@demo.local')).toBeVisible();
    await expect(page.getByText('Administrator')).toBeVisible();
    await expect(page.getByText('Delete products')).toBeVisible();
    // This API exposes no profile mutations, so there is nothing to type into (§3.8).
    await expect(page.locator('#main input')).toHaveCount(0);
  });

  test('deletes a product, and the confirmation behaves itself', async () => {
    const name = uniqueName('E2E Doomed Product');

    await page.goto('/products/new');
    await page.getByLabel('Name').fill(name);
    await page.getByLabel('Price').fill('5.00');
    await page.getByRole('button', { name: 'Create product' }).click();
    await expect(page.getByRole('heading', { level: 1 })).toContainText(name);

    await page.getByRole('button', { name: 'Delete', exact: true }).click();

    const dialog = page.getByRole('dialog');
    await expect(dialog).toContainText('will be removed from the catalogue');
    // Cancel is focused, so a reflexive Enter dismisses rather than destroys (§3.7).
    await expect(dialog.getByRole('button', { name: 'Cancel' })).toBeFocused();

    await page.keyboard.press('Escape');
    await expect(dialog).toBeHidden();

    await page.getByRole('button', { name: 'Delete', exact: true }).click();
    await page.getByRole('dialog').getByRole('button', { name: 'Delete' }).click();

    await expect(page).toHaveURL(/\/products$/);
    await expect(page.getByText(`“${name}” was deleted.`)).toBeVisible();

    // Soft-deleted products never appear in a list (§0.5, §3.5).
    await page.getByLabel('Search name').fill(name);
    await expect(page.getByText('No products match your search.')).toBeVisible();
  });

  test('signs out, clearing the stored refresh token', async () => {
    await page.goto('/products');
    await page.getByRole('banner').getByRole('button', { name: 'Sign out' }).click();
    await expect(page).toHaveURL(/\/login/);

    expect(await page.evaluate(() => localStorage.getItem('catalog.refreshToken'))).toBeNull();

    // The catalogue still browses anonymously afterwards.
    await page.goto('/products');
    await expect(page.getByRole('link', { name: 'Sign in' })).toBeVisible();
    await expect(page.getByRole('table')).toBeVisible();
  });
});
