/** Anonymous browsing — reads require no login, which is a product decision (PDR §3.0). */
import { expect, requireApi, test } from './fixtures';

requireApi();

test('an anonymous visitor gets a working catalogue', async ({ page }) => {
  await page.goto('/products');

  await expect(page.getByRole('heading', { name: 'Products' })).toBeVisible();
  await expect(page.getByRole('link', { name: 'Sign in' })).toBeVisible();

  // A stripped or broken shell would be wrong — anonymous browsing is supported, not degraded.
  await expect(page.getByRole('link', { name: '+ New product' })).toHaveCount(0);
  await expect(page.getByRole('table')).toBeVisible();
  await expect(page.getByText(/Showing 1–20 of \d+/)).toBeVisible();
});

test('a page change pushes history, so Back returns to the list', async ({ page }) => {
  await page.goto('/products');

  await page
    .getByRole('navigation', { name: 'Pagination' })
    .getByRole('button', { name: 'Next ›' })
    .click();

  await expect(page).toHaveURL(/page=2/);
  await expect(page.getByText(/Showing 21–40 of \d+/)).toBeVisible();

  // Back returns to the previous page rather than exiting the list entirely (§6.4).
  await page.goBack();
  await expect(page).toHaveURL(/\/products$/);
  await expect(page.getByText(/Showing 1–20 of \d+/)).toBeVisible();
});

test('a sort change replaces history and resets the page', async ({ page }) => {
  await page.goto('/products');

  await page
    .getByRole('navigation', { name: 'Pagination' })
    .getByRole('button', { name: 'Next ›' })
    .click();
  await expect(page).toHaveURL(/page=2/);

  await page.getByRole('button', { name: /^Price/ }).click();

  // Sorting resets the page, or the user can be left past the end (§3.4.6).
  await expect(page).toHaveURL(/sortBy=Price/);
  await expect(page).not.toHaveURL(/page=2/);
  await expect(page.getByRole('columnheader', { name: /Price/ })).toHaveAttribute(
    'aria-sort',
    'descending',
  );

  // Sort and filter changes replace rather than push (§6.4), so the page=2 entry is gone: Back
  // goes to the list's first page, not to page 2.
  await page.goBack();
  await expect(page).toHaveURL(/\/products$/);
});

test('a deep-linked filtered URL restores its own state', async ({ page }) => {
  await page.goto('/products?sortBy=Price&direction=Asc&minPrice=0&maxPrice=5');

  await expect(page.getByLabel('Min price')).toHaveValue('0');
  await expect(page.getByLabel('Max price')).toHaveValue('5');
  await expect(page.getByRole('columnheader', { name: /Price/ })).toHaveAttribute(
    'aria-sort',
    'ascending',
  );
});

test('searching by name switches to the search endpoint', async ({ page }) => {
  const searchRequest = page.waitForRequest((request) =>
    request.url().includes('/products/search'),
  );

  await page.goto('/products');
  await page.getByLabel('Search name').fill('a');

  await searchRequest; // §3.4.1 — filters go to /search, unfiltered browsing does not
  await expect(page).toHaveURL(/name=a/);
});

test('an invalid price range is caught before the request is sent', async ({ page }) => {
  await page.goto('/products');

  await page.getByLabel('Min price').fill('500');
  await page.getByLabel('Max price').fill('10');

  // The server reports this under `minPrice`; so does the UI (§4.4).
  await expect(page.getByText('minPrice must be less than or equal to maxPrice.')).toBeVisible();
});

test('a missing product renders a not-found page, not a crash', async ({ page }) => {
  await page.goto('/products/999999');
  await expect(page.getByText('Product not found')).toBeVisible();
});

test('a guarded route sends an anonymous visitor to sign in', async ({ page }) => {
  await page.goto('/products/new');
  await expect(page).toHaveURL(/\/login/);
});
