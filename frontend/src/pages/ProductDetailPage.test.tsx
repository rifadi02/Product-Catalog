/** Product detail — PDR §3.5. */
import { beforeEach, describe, expect, it } from 'vitest';
import { http, HttpResponse } from 'msw';
import { screen } from '@testing-library/react';
import { ProductDetailPage } from './ProductDetailPage';
import { __resetRefreshLatch } from '../api/client';
import { clearTokens } from '../auth/tokenStore';
import { productKeys } from '../api/queryKeys';
import { API, server } from '../test/server';
import { makeProblem, makeProduct } from '../test/factories';
import { ADMIN, STANDARD_USER, makeTestQueryClient, renderWithUser } from '../test/render';
import type { ProductSnapshot, UserSummary } from '../api/types';

beforeEach(() => {
  clearTokens();
  __resetRefreshLatch();
});

const renderDetail = (route = '/products/42', user: UserSummary | null = null) =>
  renderWithUser(<ProductDetailPage />, { route, path: '/products/:id', user });

describe('ProductDetailPage', () => {
  it('captures the ETag into the cache for the edit flow', async () => {
    server.use(
      http.get(`${API}/products/42`, () =>
        HttpResponse.json(makeProduct(), { headers: { ETag: '"8125"' } }),
      ),
    );

    const queryClient = makeTestQueryClient();
    renderWithUser(<ProductDetailPage />, {
      route: '/products/42',
      path: '/products/:id',
      queryClient,
    });

    await screen.findByRole('heading', { level: 1, name: /Desk Lamp/ });

    // Quotes included — the whole string is what If-Match must echo (§3.5).
    const snapshot = queryClient.getQueryData<ProductSnapshot>(productKeys.detail(42));
    expect(snapshot?.etag).toBe('"8125"');
  });

  it('renders a 2000-character description without truncating it', async () => {
    const description = 'X'.repeat(2000);
    server.use(
      http.get(`${API}/products/42`, () =>
        HttpResponse.json(makeProduct({ description }), { headers: { ETag: '"1"' } }),
      ),
    );

    renderDetail();
    expect(await screen.findByText(description)).toBeInTheDocument();
  });

  it('says so plainly when there is no description', async () => {
    server.use(
      http.get(`${API}/products/42`, () =>
        HttpResponse.json(makeProduct({ description: null, price: 0 }), {
          headers: { ETag: '"1"' },
        }),
      ),
    );

    renderDetail();

    expect(await screen.findByText('No description.')).toBeInTheDocument();
    // A Rp 0,00 product is real seed data (§0.5).
    expect(screen.getByText(/Rp\s?0,00/)).toBeInTheDocument();
  });

  it('hides the updated row entirely until the first PUT', async () => {
    server.use(
      http.get(`${API}/products/42`, () =>
        HttpResponse.json(makeProduct({ updatedAt: null }), { headers: { ETag: '"1"' } }),
      ),
    );

    renderDetail();
    await screen.findByRole('heading', { level: 1, name: /Desk Lamp/ });

    expect(screen.getByText('Created')).toBeInTheDocument();
    expect(screen.queryByText('Last updated')).not.toBeInTheDocument();
  });

  it('shows a full-page not-found for a 404', async () => {
    server.use(
      http.get(`${API}/products/999`, () =>
        HttpResponse.json(makeProblem('resource_not_found', 404), { status: 404 }),
      ),
    );

    renderDetail('/products/999');

    expect(await screen.findByText('Product not found')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Back to products' })).toBeInTheDocument();
  });

  it('treats a non-numeric id as not found without calling the API', async () => {
    // `/products/abc` and `/products/0` are 400s server-side; the route guard makes them
    // unreachable from the UI (§3.5). No handler is registered here.
    renderDetail('/products/abc');
    expect(await screen.findByText('Page not found')).toBeInTheDocument();

    renderDetail('/products/0');
    expect(await screen.findAllByText('Page not found')).toHaveLength(2);
  });

  it('shows Edit but not Delete to a standard user', async () => {
    server.use(
      http.get(`${API}/products/42`, () =>
        HttpResponse.json(makeProduct(), { headers: { ETag: '"1"' } }),
      ),
    );

    renderDetail('/products/42', STANDARD_USER);

    expect(await screen.findByRole('link', { name: 'Edit' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Delete' })).not.toBeInTheDocument();
  });

  it('shows both actions to an Admin and neither to an anonymous visitor', async () => {
    server.use(
      http.get(`${API}/products/42`, () =>
        HttpResponse.json(makeProduct(), { headers: { ETag: '"1"' } }),
      ),
    );

    const admin = renderDetail('/products/42', ADMIN);
    expect(await screen.findByRole('button', { name: 'Delete' })).toBeInTheDocument();
    admin.unmount();

    renderDetail('/products/42', null);
    await screen.findByRole('heading', { level: 1, name: /Desk Lamp/ });
    expect(screen.queryByRole('link', { name: 'Edit' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Delete' })).not.toBeInTheDocument();
  });
});
