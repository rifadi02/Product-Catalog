/**
 * Optimistic concurrency against the real API — PDR §3.6.3, §3.6.4.
 *
 * Two browser contexts edit one product. Whoever saves second must be told, not silently
 * overwritten: that is the whole point of always sending `If-Match`.
 */
import { expect, requireApi, signIn, test, uniqueName } from './fixtures';

requireApi();

test('the second writer is shown a conflict rather than silently losing', async ({ browser }) => {
  const first = await browser.newContext();
  const second = await browser.newContext();

  const pageA = await first.newPage();
  const pageB = await second.newPage();

  try {
    await signIn(pageA, 'admin');
    await signIn(pageB, 'user');

    // Create the contested product in the first context.
    const name = uniqueName('E2E Contested');
    await pageA.goto('/products/new');
    await pageA.getByLabel('Name').fill(name);
    await pageA.getByLabel('Price').fill('10.00');
    await pageA.getByRole('button', { name: 'Create product' }).click();
    await expect(pageA).toHaveURL(/\/products\/\d+$/);

    const url = new URL(pageA.url());
    const productPath = url.pathname;

    // Both open the edit form, so both hold the same ETag.
    await pageA.goto(`${productPath}/edit`);
    await pageB.goto(`${productPath}/edit`);
    await expect(pageA.getByLabel('Name')).toHaveValue(name);
    await expect(pageB.getByLabel('Name')).toHaveValue(name);

    // A saves first and wins.
    await pageA.getByLabel('Price').fill('11.00');
    await pageA.getByRole('button', { name: 'Save changes' }).click();
    await expect(pageA).toHaveURL(new RegExp(`${productPath}$`));

    // B saves second against a now-stale ETag.
    await pageB.getByLabel('Price').fill('22.00');
    await pageB.getByRole('button', { name: 'Save changes' }).click();

    const dialog = pageB.getByRole('dialog');
    await expect(dialog).toContainText('changed while you were editing');
    await expect(dialog).toContainText(/Rp\s?11,00/); // theirs
    await expect(dialog).toContainText(/Rp\s?22,00/); // mine

    // B's edits are still in the form behind the dialog — never discarded (§3.6.4).
    await expect(pageB.getByLabel('Price')).toHaveValue('22.00');

    // Keeping them resubmits against the fresh ETag the refetch supplied.
    await dialog.getByRole('button', { name: 'Keep my changes' }).click();
    await expect(pageB).toHaveURL(new RegExp(`${productPath}$`));
    await expect(pageB.getByText(/Rp\s?22,00/)).toBeVisible();

    // Clean up. No reload here: `goto` already loads the page fresh, and reloading straight on top
    // of it can abort the boot refresh after the server rotated the token, which ends pageA's
    // session and leaves nothing to click (see write-flows.spec.ts for the same trap).
    await pageA.goto(productPath);
    await expect(pageA.getByRole('button', { name: 'Sign out' })).toBeVisible();

    await pageA.getByRole('button', { name: 'Delete', exact: true }).click();
    await pageA.getByRole('dialog').getByRole('button', { name: 'Delete' }).click();
    await expect(pageA).toHaveURL(/\/products$/);
  } finally {
    await first.close();
    await second.close();
  }
});
