/** Product endpoints — PDR §3.4–§3.7. */
import { api } from './client';
import type {
  PagedResponse,
  Product,
  ProductQuery,
  ProductSnapshot,
  ProductWriteRequest,
} from './types';

/** A filter is active when any of the three search parameters is set (§3.4.1). */
export function isFiltered(query: ProductQuery): boolean {
  return Boolean(query.name || query.minPrice != null || query.maxPrice != null);
}

/**
 * `/products` caches its first three pages server-side and `/products/search` is deliberately
 * uncached, so routing unfiltered browsing through `/search` throws that cache away (§3.4.1).
 * Both return the identical envelope, so callers render from one code path either way.
 */
export async function listProducts(
  query: ProductQuery,
  signal?: AbortSignal,
): Promise<PagedResponse<Product>> {
  const { data } = await api.get<PagedResponse<Product>>(
    isFiltered(query) ? '/products/search' : '/products',
    { params: query, signal },
  );
  return data;
}

/**
 * §3.5 — the `ETag` comes back on the response header and is a strong quoted token: the quotes
 * are part of the value and the whole string is what `If-Match` must echo. It is CORS-exposed,
 * so this read works from the browser.
 */
export async function getProduct(id: number, signal?: AbortSignal): Promise<ProductSnapshot> {
  const response = await api.get<Product>(`/products/${id}`, { signal });
  return { product: response.data, etag: response.headers['etag'] ?? null };
}

/**
 * §3.6.1 — the 201 carries a full ProductDto but no `ETag`, so editing what you just created
 * still needs the detail GET first.
 */
export async function createProduct(body: ProductWriteRequest): Promise<Product> {
  const { data } = await api.post<Product>('/products', body);
  return data;
}

/**
 * §3.6.2 — a full replacement, not a patch: all three fields go every time, or an omitted
 * description silently becomes null. Returns 204 with an empty body and no new `ETag`, which is
 * why callers must invalidate rather than merge an optimistic value: a stale ETag breaks the
 * *next* edit.
 *
 * The server treats `If-Match` as optional; this client always sends it (§3.6.3). Omitting it is
 * last-write-wins, where the loser never learns they lost.
 */
export async function updateProduct(
  id: number,
  body: ProductWriteRequest,
  etag: string,
): Promise<void> {
  await api.put(`/products/${id}`, body, { headers: { 'If-Match': etag } });
}

/** §3.7 — Admin only. No body, no `If-Match`; the server does not read one on DELETE. */
export async function deleteProduct(id: number): Promise<void> {
  await api.delete(`/products/${id}`);
}
