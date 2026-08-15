/** Edit — PDR §3.6.2–§3.6.4, the optimistic-concurrency flow. */
import { beforeEach, describe, expect, it } from 'vitest';
import { http, HttpResponse } from 'msw';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ProductEditPage } from './ProductEditPage';
import { __resetRefreshLatch } from '../api/client';
import { clearTokens } from '../auth/tokenStore';
import { API, server } from '../test/server';
import { makeProblem, makeProduct } from '../test/factories';
import { STANDARD_USER, renderWithUser } from '../test/render';
import type { Product } from '../api/types';

interface PutCall {
  ifMatch: string | null;
  body: { name: string; description: string | null; price: number };
}

let puts: PutCall[] = [];

beforeEach(() => {
  clearTokens();
  __resetRefreshLatch();
  puts = [];
});

/** `ETag` is a strong quoted token and the quotes are part of the value (§3.5). */
function stubDetail(product: Product, etag = '"8125"') {
  server.use(
    http.get(`${API}/products/${product.id}`, () =>
      HttpResponse.json(product, { headers: { ETag: etag } }),
    ),
  );
}

function stubPut(respond: (call: PutCall) => Response) {
  server.use(
    http.put(`${API}/products/42`, async ({ request }) => {
      const call: PutCall = {
        ifMatch: request.headers.get('if-match'),
        body: (await request.json()) as PutCall['body'],
      };
      puts.push(call);
      return respond(call);
    }),
  );
}

const renderEdit = () =>
  renderWithUser(<ProductEditPage />, {
    route: '/products/42/edit',
    path: '/products/:id/edit',
    user: STANDARD_USER,
  });

const save = () => userEvent.click(screen.getByRole('button', { name: 'Save changes' }));

describe('form seeding', () => {
  it('seeds from the cached product and maps a null description to an empty field', async () => {
    stubDetail(makeProduct({ description: null, price: 0 }));
    renderEdit();

    expect(await screen.findByLabelText(/^name/i)).toHaveValue('Desk Lamp');
    expect(screen.getByLabelText(/^description/i)).toHaveValue('');
    // A Rp 0,00 product is real, and its price must survive the round trip (§4.3).
    expect(screen.getByLabelText(/^price/i)).toHaveValue(0);
  });

  it('disables submit until something is modified', async () => {
    stubDetail(makeProduct());
    renderEdit();

    expect(await screen.findByRole('button', { name: 'Save changes' })).toBeDisabled();

    await userEvent.type(screen.getByLabelText(/^name/i), ' Mk II');
    expect(screen.getByRole('button', { name: 'Save changes' })).toBeEnabled();
  });

  it('shows a not-found page for a soft-deleted product', async () => {
    // Soft-deleted ids 404 identically to ones that never existed (§3.5).
    server.use(
      http.get(`${API}/products/42`, () =>
        HttpResponse.json(makeProblem('resource_not_found', 404), { status: 404 }),
      ),
    );

    renderEdit();
    expect(await screen.findByText('Product not found')).toBeInTheDocument();
  });
});

describe('the PUT request', () => {
  it('always sends If-Match with the ETag captured by the detail GET', async () => {
    stubDetail(makeProduct(), '"8125"');
    stubPut(() => new HttpResponse(null, { status: 204 }));

    renderEdit();
    await userEvent.clear(await screen.findByLabelText(/^name/i));
    await userEvent.type(screen.getByLabelText(/^name/i), 'Desk Lamp (Refurbished)');
    await save();

    // Omitting the header is last-write-wins, where the loser never learns (§3.6.3).
    await waitFor(() => expect(puts).toHaveLength(1));
    expect(puts[0]?.ifMatch).toBe('"8125"');
  });

  it('sends all three fields, because PUT is a replacement rather than a patch', async () => {
    stubDetail(makeProduct({ description: 'Warm light, adjustable arm.' }));
    stubPut(() => new HttpResponse(null, { status: 204 }));

    renderEdit();
    await userEvent.clear(await screen.findByLabelText(/^price/i));
    await userEvent.type(screen.getByLabelText(/^price/i), '19.95');
    await save();

    await waitFor(() => expect(puts).toHaveLength(1));
    // Omitting description would silently null it (§3.6.2).
    expect(puts[0]?.body).toEqual({
      name: 'Desk Lamp',
      description: 'Warm light, adjustable arm.',
      price: 19.95,
    });
  });

  it('sends null rather than an empty string for a cleared description', async () => {
    stubDetail(makeProduct({ description: 'Something' }));
    stubPut(() => new HttpResponse(null, { status: 204 }));

    renderEdit();
    await userEvent.clear(await screen.findByLabelText(/^description/i));
    await save();

    await waitFor(() => expect(puts).toHaveLength(1));
    expect(puts[0]?.body.description).toBeNull();
  });

  it("rounds the price to the server's banker's rounding on blur", async () => {
    stubDetail(makeProduct());
    renderEdit();

    const price = await screen.findByLabelText(/^price/i);
    await userEvent.clear(price);
    await userEvent.type(price, '2.355');
    await userEvent.tab();

    // 2.355 stores as 2.36, so that is what the field should show (§4.3).
    await waitFor(() => expect(price).toHaveValue(2.36));
  });

  it('maps a 400 errors bag onto the fields and keeps the form populated', async () => {
    stubDetail(makeProduct());
    stubPut(() =>
      HttpResponse.json(
        makeProblem('validation_failed', 400, { errors: { name: ['Name is required.'] } }),
        { status: 400 },
      ),
    );

    renderEdit();
    await userEvent.type(await screen.findByLabelText(/^name/i), ' Mk II');
    await save();

    expect(await screen.findByText('Name is required.')).toBeInTheDocument();
    expect(screen.getByLabelText(/^name/i)).toHaveValue('Desk Lamp Mk II');
  });

  it('offers a way out when the product was deleted mid-edit', async () => {
    stubDetail(makeProduct());
    stubPut(() =>
      HttpResponse.json(makeProblem('resource_not_found', 404), { status: 404 }),
    );

    renderEdit();
    await userEvent.type(await screen.findByLabelText(/^name/i), ' Mk II');
    await save();

    const dialog = await screen.findByRole('dialog');
    expect(dialog).toHaveTextContent(/no longer exists/i);
    expect(within(dialog).getByRole('button', { name: 'Back to products' })).toBeInTheDocument();
  });
});

describe('the 409 conflict flow', () => {
  const theirs = makeProduct({
    name: 'Desk Lamp (Server Edit)',
    description: 'Changed by someone else.',
    price: 30,
  });

  /** 409 on the stale ETag, 204 once the fresh one arrives. */
  function stubConflictThenSuccess() {
    let etag = '"8125"';

    server.use(
      http.get(`${API}/products/42`, () => {
        // The second read is the conflict flow's refetch, and it carries the new version.
        const body = etag === '"8125"' ? makeProduct() : theirs;
        return HttpResponse.json(body, { headers: { ETag: etag } });
      }),
    );

    stubPut((call) => {
      if (call.ifMatch === '"8125"') {
        etag = '"9310"';
        return HttpResponse.json(makeProblem('concurrency_conflict', 409), { status: 409 });
      }
      return new HttpResponse(null, { status: 204 });
    });
  }

  it('keeps the edits, shows both versions, and resubmits against the fresh ETag', async () => {
    stubConflictThenSuccess();
    renderEdit();

    await userEvent.clear(await screen.findByLabelText(/^name/i));
    await userEvent.type(screen.getByLabelText(/^name/i), 'Desk Lamp (My Edit)');
    await save();

    const dialog = await screen.findByRole('dialog');
    expect(dialog).toHaveTextContent(/changed while you were editing/i);
    // Field by field: theirs and yours (§3.6.4).
    expect(within(dialog).getByText('Desk Lamp (Server Edit)')).toBeInTheDocument();
    expect(within(dialog).getByText('Desk Lamp (My Edit)')).toBeInTheDocument();

    // The user's edits are still in the form behind the dialog — never discarded.
    expect(screen.getByLabelText(/^name/i)).toHaveValue('Desk Lamp (My Edit)');

    await userEvent.click(within(dialog).getByRole('button', { name: 'Keep my changes' }));

    await waitFor(() => expect(puts).toHaveLength(2));
    expect(puts[0]?.ifMatch).toBe('"8125"'); // the stale one that produced the 409
    expect(puts[1]?.ifMatch).toBe('"9310"'); // the fresh one from the refetch
    expect(puts[1]?.body.name).toBe('Desk Lamp (My Edit)');
  });

  it('restores the server values when the user discards their own', async () => {
    stubConflictThenSuccess();
    renderEdit();

    await userEvent.clear(await screen.findByLabelText(/^name/i));
    await userEvent.type(screen.getByLabelText(/^name/i), 'Desk Lamp (My Edit)');
    await save();

    const dialog = await screen.findByRole('dialog');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Discard mine' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(screen.getByLabelText(/^name/i)).toHaveValue('Desk Lamp (Server Edit)');
    expect(screen.getByLabelText(/^description/i)).toHaveValue('Changed by someone else.');
    expect(puts).toHaveLength(1); // nothing was resubmitted
  });

  it('autofocuses the non-destructive choice', async () => {
    stubConflictThenSuccess();
    renderEdit();

    await userEvent.type(await screen.findByLabelText(/^name/i), ' Mk II');
    await save();

    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByRole('button', { name: 'Discard mine' })).toHaveFocus();
  });
});
