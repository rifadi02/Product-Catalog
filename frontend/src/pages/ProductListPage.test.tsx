/** Product list — PDR §3.4, §6. */
import { beforeEach, describe, expect, it } from 'vitest';
import { http, HttpResponse } from 'msw';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ProductListPage } from './ProductListPage';
import { __resetRefreshLatch } from '../api/client';
import { clearTokens } from '../auth/tokenStore';
import { API, server } from '../test/server';
import { EDGE_CASE_PRODUCTS, makePage, makeProblem, makeProduct } from '../test/factories';
import { ADMIN, STANDARD_USER, renderWithUser } from '../test/render';

interface Call {
  path: string;
  params: URLSearchParams;
}

let calls: Call[] = [];

beforeEach(() => {
  clearTokens();
  __resetRefreshLatch();
  calls = [];
});

/** Records which endpoint the list chose, and with what parameters. */
function stubList(page = makePage(EDGE_CASE_PRODUCTS, { totalCount: 63, totalPages: 4 })) {
  const record = ({ request }: { request: Request }) => {
    const url = new URL(request.url);
    calls.push({ path: url.pathname, params: url.searchParams });
    return HttpResponse.json(page);
  };

  server.use(http.get(`${API}/products`, record), http.get(`${API}/products/search`, record));
}

/**
 * §6.3 prefetches the next page as soon as the current one settles, so `calls` legitimately
 * contains requests the user did not trigger. Assertions about what the *view* asked for look at
 * page-1 calls only.
 */
const pageOf = (call: Call) => call.params.get('page') ?? '1';
const viewCalls = () => calls.filter((call) => pageOf(call) === '1');
const lastViewCall = () => viewCalls().at(-1);
const searchCalls = () => viewCalls().filter((call) => call.path.endsWith('/search'));
const requestedPages = () => calls.map(pageOf);

describe('endpoint selection', () => {
  it('uses GET /products when no filter is active', async () => {
    // /products caches its first three pages server-side; routing unfiltered browsing through
    // /search throws that cache away (§3.4.1).
    stubList();
    renderWithUser(<ProductListPage />, { route: '/products' });

    await waitFor(() => expect(lastViewCall()).toBeDefined());
    expect(lastViewCall()?.path).toBe('/api/v1/products');
    expect(lastViewCall()?.params.get('sortBy')).toBe('CreatedAt');
    expect(lastViewCall()?.params.get('direction')).toBe('Desc');
    expect(lastViewCall()?.params.get('pageSize')).toBe('20');
  });

  it('switches to GET /products/search once a filter is set', async () => {
    stubList();
    renderWithUser(<ProductListPage />, { route: '/products?name=lamp&minPrice=10' });

    await waitFor(() => expect(searchCalls()).toHaveLength(1));
    expect(searchCalls()[0]?.params.get('name')).toBe('lamp');
    expect(searchCalls()[0]?.params.get('minPrice')).toBe('10');
  });

  it('debounces the name filter into a single request', async () => {
    // No inter-keystroke delay, so all four keystrokes land inside one 300 ms window — the case
    // the debounce exists for (§3.4.7).
    const user = userEvent.setup({ delay: null });
    stubList();
    renderWithUser(<ProductListPage />, { route: '/products' });
    await waitFor(() => expect(viewCalls()).toHaveLength(1));

    await user.type(screen.getByLabelText(/search name/i), 'lamp');

    // One search for the whole word, not one per keystroke.
    await waitFor(() => expect(searchCalls()).toHaveLength(1), { timeout: 2000 });
    expect(searchCalls()[0]?.params.get('name')).toBe('lamp');
  });

  it('never sends a request when minPrice exceeds maxPrice', async () => {
    const user = userEvent.setup({ delay: null });
    stubList();
    renderWithUser(<ProductListPage />, { route: '/products' });
    await waitFor(() => expect(viewCalls()).toHaveLength(1));

    await user.type(screen.getByLabelText(/min price/i), '50');
    await user.type(screen.getByLabelText(/max price/i), '10');

    // The cross-field rule is a 400 server-side; catch it here instead (§3.4.3, §4.4).
    expect(
      await screen.findByText('minPrice must be less than or equal to maxPrice.'),
    ).toBeInTheDocument();

    expect(searchCalls()).toHaveLength(0);
  });

  it('resumes searching once an invalid range is corrected', async () => {
    const user = userEvent.setup({ delay: null });
    stubList();
    renderWithUser(<ProductListPage />, { route: '/products' });
    await waitFor(() => expect(viewCalls()).toHaveLength(1));

    await user.type(screen.getByLabelText(/min price/i), '50');
    await user.type(screen.getByLabelText(/max price/i), '10');
    await screen.findByText('minPrice must be less than or equal to maxPrice.');

    await user.clear(screen.getByLabelText(/max price/i));
    await user.type(screen.getByLabelText(/max price/i), '100');

    await waitFor(() => expect(searchCalls().length).toBeGreaterThan(0), { timeout: 2000 });
    expect(searchCalls().at(-1)?.params.get('minPrice')).toBe('50');
    expect(searchCalls().at(-1)?.params.get('maxPrice')).toBe('100');
  });
});

describe('rendering', () => {
  it('renders the seeded edge cases without breaking', async () => {
    stubList();
    renderWithUser(<ProductListPage />, { route: '/products' });

    // Accents, emoji and CJK in one name (§0.5).
    expect(
      await screen.findByRole('link', { name: /Café Latte Máquina — Ünïcode/ }),
    ).toBeInTheDocument();

    // A zero price renders as 0,00, not an em-dash (§3.4.5). The gap after `Rp` is a
    // non-breaking space from Intl, which `\s` matches.
    expect(screen.getByText(/Rp\s?0,00/)).toBeInTheDocument();
    expect(screen.getByText(/Rp\s?999\.999,99/)).toBeInTheDocument();

    // The 2000-character description belongs on the detail page, not in a table cell (§3.4.5).
    expect(screen.queryByText('X'.repeat(2000))).not.toBeInTheDocument();
  });

  it('shows an em-dash for a product never updated', async () => {
    stubList(makePage([makeProduct({ updatedAt: null })]));
    renderWithUser(<ProductListPage />, { route: '/products' });

    const row = await screen.findByRole('row', { name: /Desk Lamp/ });
    expect(within(row).getByText('—')).toBeInTheDocument();
  });

  it('offers a create CTA on an empty catalogue when the user may write', async () => {
    stubList(makePage([], { totalCount: 0, totalPages: 0 }));
    renderWithUser(<ProductListPage />, { route: '/products', user: STANDARD_USER });

    expect(await screen.findByText('No products yet.')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Create the first one' })).toBeInTheDocument();
  });

  it('offers a clear button when filters produced nothing', async () => {
    stubList(makePage([], { totalCount: 0, totalPages: 0 }));
    renderWithUser(<ProductListPage />, { route: '/products?name=nothing' });

    expect(await screen.findByText('No products match your search.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Clear filters' })).toBeInTheDocument();
  });

  it('surfaces a server error with a retry affordance', async () => {
    server.use(
      http.get(`${API}/products`, () =>
        HttpResponse.json(makeProblem('internal_error', 500), { status: 500 }),
      ),
    );

    renderWithUser(<ProductListPage />, { route: '/products' });

    const alert = await screen.findByRole('alert', {}, { timeout: 3000 });
    expect(alert).toHaveTextContent('Something went wrong.');
    expect(within(alert).getByRole('button', { name: 'Retry' })).toBeInTheDocument();
  });
});

describe('sorting', () => {
  it('sends Name and toggles direction on a second click', async () => {
    stubList();
    renderWithUser(<ProductListPage />, { route: '/products' });
    await waitFor(() => expect(viewCalls()).toHaveLength(1));

    await userEvent.click(screen.getByRole('button', { name: /^Name/ }));
    // A newly sorted column starts at Desc (§3.4.6).
    await waitFor(() => expect(lastViewCall()?.params.get('sortBy')).toBe('Name'));
    expect(lastViewCall()?.params.get('direction')).toBe('Desc');

    await userEvent.click(screen.getByRole('button', { name: /^Name/ }));
    await waitFor(() => expect(lastViewCall()?.params.get('direction')).toBe('Asc'));
  });

  it('resets to page 1 when the sort changes', async () => {
    stubList(makePage(EDGE_CASE_PRODUCTS, { page: 4, totalCount: 63, totalPages: 4 }));
    renderWithUser(<ProductListPage />, { route: '/products?page=4' });
    await waitFor(() => expect(calls.length).toBeGreaterThan(0));

    await userEvent.click(screen.getByRole('button', { name: /^Price/ }));

    // Page 4 of a re-sorted list can be past the end (§3.4.6).
    await waitFor(() =>
      expect(
        viewCalls().some((call) => call.params.get('sortBy') === 'Price'),
      ).toBe(true),
    );
  });

  it('does not offer Updated as a sortable column', async () => {
    // The API has no sortBy=UpdatedAt (§3.4.6).
    stubList();
    renderWithUser(<ProductListPage />, { route: '/products' });

    await screen.findByRole('columnheader', { name: 'Updated' });
    expect(screen.queryByRole('button', { name: /^Updated/ })).not.toBeInTheDocument();
  });
});

describe('row actions', () => {
  it('hides both write actions from an anonymous visitor', async () => {
    stubList(makePage([makeProduct()]));
    renderWithUser(<ProductListPage />, { route: '/products', user: null });

    await screen.findByRole('link', { name: 'Desk Lamp' });
    expect(screen.queryByRole('link', { name: '+ New product' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /^Edit/ })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /^Delete/ })).not.toBeInTheDocument();
  });

  it('gives a standard user edit but not delete', async () => {
    // DELETE is Admin-only: an authenticated User gets a 403, not a 401 (§5.2).
    stubList(makePage([makeProduct()]));
    renderWithUser(<ProductListPage />, { route: '/products', user: STANDARD_USER });

    expect(await screen.findByRole('button', { name: 'Edit Desk Lamp' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Delete Desk Lamp' })).not.toBeInTheDocument();
  });

  it('gives an Admin both', async () => {
    stubList(makePage([makeProduct()]));
    renderWithUser(<ProductListPage />, { route: '/products', user: ADMIN });

    expect(await screen.findByRole('button', { name: 'Edit Desk Lamp' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Delete Desk Lamp' })).toBeInTheDocument();
  });
});

describe('delete', () => {
  const confirmDelete = async () => {
    await userEvent.click(await screen.findByRole('button', { name: 'Delete Desk Lamp' }));
    const dialog = await screen.findByRole('dialog');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Delete' }));
  };

  it('confirms, deletes, and does not confirm on Enter', async () => {
    stubList(makePage([makeProduct()]));

    let deleted = false;
    server.use(
      http.delete(`${API}/products/42`, () => {
        deleted = true;
        return new HttpResponse(null, { status: 204 });
      }),
    );

    renderWithUser(<ProductListPage />, { route: '/products', user: ADMIN });
    await userEvent.click(await screen.findByRole('button', { name: 'Delete Desk Lamp' }));

    const dialog = await screen.findByRole('dialog');
    expect(dialog).toHaveTextContent(/will be removed from the catalogue/i);
    // Cancel is autofocused and Enter must not confirm (§3.7).
    expect(within(dialog).getByRole('button', { name: 'Cancel' })).toHaveFocus();

    await userEvent.keyboard('{Enter}');
    expect(deleted).toBe(false);

    await confirmDelete();

    await waitFor(() => expect(deleted).toBe(true));
    expect(await screen.findByText(/was deleted/i)).toBeInTheDocument();
  });

  it('closes on Escape without deleting', async () => {
    stubList(makePage([makeProduct()]));
    renderWithUser(<ProductListPage />, { route: '/products', user: ADMIN });

    await userEvent.click(await screen.findByRole('button', { name: 'Delete Desk Lamp' }));
    await screen.findByRole('dialog');

    await userEvent.keyboard('{Escape}');
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
  });

  it('treats a 404 as success, because the product is gone either way', async () => {
    // Deletion is a soft delete and a second DELETE returns 404, not another 204 (§3.7).
    stubList(makePage([makeProduct()]));
    server.use(
      http.delete(`${API}/products/42`, () =>
        HttpResponse.json(makeProblem('resource_not_found', 404), { status: 404 }),
      ),
    );

    renderWithUser(<ProductListPage />, { route: '/products', user: ADMIN });
    await confirmDelete();

    expect(await screen.findByText(/had already been removed/i)).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('reports a 403 without logging the user out', async () => {
    stubList(makePage([makeProduct()]));
    server.use(
      http.delete(`${API}/products/42`, () =>
        HttpResponse.json(makeProblem('insufficient_role', 403), { status: 403 }),
      ),
    );

    // Hiding the button is cosmetic; the server is the gate (§5.4).
    renderWithUser(<ProductListPage />, { route: '/products', user: ADMIN });
    await confirmDelete();

    expect(await screen.findByText(/don't have permission/i)).toBeInTheDocument();
  });

  it('steps back a page after deleting the last row on it', async () => {
    stubList(
      makePage([makeProduct()], {
        page: 4,
        totalCount: 61,
        totalPages: 4,
        hasNextPage: false,
        hasPreviousPage: true,
      }),
    );
    server.use(http.delete(`${API}/products/42`, () => new HttpResponse(null, { status: 204 })));

    renderWithUser(<ProductListPage />, { route: '/products?page=4', user: ADMIN });
    await confirmDelete();

    // Otherwise the user lands on an empty view (§3.7, §6.2).
    await waitFor(() => expect(requestedPages()).toContain('3'));
  });
});
