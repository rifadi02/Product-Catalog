/**
 * Paging, sorting and filter state as URL query parameters — PDR §6.4.
 *
 * The URL is the source of truth, which makes list views shareable, bookmarkable, and correct
 * under browser back/forward. Parameters at their default are omitted so the common URL stays
 * clean.
 *
 * Everything read out of the URL is clamped back into the legal range. The UI owns these values
 * entirely, so a 400 from `/products` is a client bug the user cannot act on (§3.4.2) — and a
 * hand-edited `?pageSize=999` is exactly that.
 */
import { useCallback, useMemo } from 'react';
import { useSearchParams } from 'react-router-dom';
import type { ProductQuery, ProductSortField, SortDirection } from '../api/types';
import {
  DEFAULT_DIRECTION,
  DEFAULT_PAGE,
  DEFAULT_PAGE_SIZE,
  DEFAULT_SORT_BY,
  NAME_MAX,
  PAGE_SIZES,
  PRICE_MAX,
} from '../lib/constants';

const SORT_FIELDS: ProductSortField[] = ['CreatedAt', 'Name', 'Price'];
const DIRECTIONS: SortDirection[] = ['Asc', 'Desc'];

export interface ProductFilters {
  name?: string;
  minPrice?: number;
  maxPrice?: number;
}

function readPrice(raw: string | null): number | undefined {
  if (raw === null || raw.trim() === '') return undefined;
  const value = Number(raw);
  if (!Number.isFinite(value) || value < 0 || value > PRICE_MAX) return undefined;
  return value;
}

export function parseProductQuery(params: URLSearchParams): ProductQuery {
  const page = Number(params.get('page'));
  const pageSize = Number(params.get('pageSize'));
  const sortBy = params.get('sortBy') as ProductSortField | null;
  const direction = params.get('direction') as SortDirection | null;
  const name = (params.get('name') ?? '').trim().slice(0, NAME_MAX);

  let minPrice = readPrice(params.get('minPrice'));
  const maxPrice = readPrice(params.get('maxPrice'));

  // The cross-field rule is a 400 on the server (§4.4). Drop the lower bound rather than send it.
  if (minPrice != null && maxPrice != null && minPrice > maxPrice) minPrice = undefined;

  return {
    page: Number.isInteger(page) && page >= 1 ? page : DEFAULT_PAGE,
    pageSize: (PAGE_SIZES as readonly number[]).includes(pageSize) ? pageSize : DEFAULT_PAGE_SIZE,
    sortBy: sortBy && SORT_FIELDS.includes(sortBy) ? sortBy : DEFAULT_SORT_BY,
    direction: direction && DIRECTIONS.includes(direction) ? direction : DEFAULT_DIRECTION,
    ...(name ? { name } : {}),
    ...(minPrice != null ? { minPrice } : {}),
    ...(maxPrice != null ? { maxPrice } : {}),
  };
}

export function toSearchParams(query: ProductQuery): URLSearchParams {
  const params = new URLSearchParams();

  if (query.page !== DEFAULT_PAGE) params.set('page', String(query.page));
  if (query.pageSize !== DEFAULT_PAGE_SIZE) params.set('pageSize', String(query.pageSize));
  if (query.sortBy !== DEFAULT_SORT_BY) params.set('sortBy', query.sortBy);
  if (query.direction !== DEFAULT_DIRECTION) params.set('direction', query.direction);
  if (query.name) params.set('name', query.name);
  if (query.minPrice != null) params.set('minPrice', String(query.minPrice));
  if (query.maxPrice != null) params.set('maxPrice', String(query.maxPrice));

  return params;
}

export function useProductQueryParams() {
  const [searchParams, setSearchParams] = useSearchParams();

  const query = useMemo(() => parseProductQuery(searchParams), [searchParams]);

  const write = useCallback(
    (next: ProductQuery, replace: boolean) => setSearchParams(toSearchParams(next), { replace }),
    [setSearchParams],
  );

  /** Page changes push, so Back returns to the previous page rather than exiting the list. */
  const setPage = useCallback((page: number) => write({ ...query, page }, false), [query, write]);

  /** Changing the size resets to page 1 — page 4 of 20-per-page is past the end at 100 (§6.5). */
  const setPageSize = useCallback(
    (pageSize: number) => write({ ...query, pageSize, page: DEFAULT_PAGE }, true),
    [query, write],
  );

  /**
   * Clicking a sorted column toggles direction; clicking a new column sets it with `Desc`.
   * Either way `page` resets — leaving the user on page 4 of a re-sorted list can land them
   * past the end (§3.4.6).
   */
  const toggleSort = useCallback(
    (column: ProductSortField) =>
      write(
        {
          ...query,
          sortBy: column,
          direction:
            query.sortBy === column ? (query.direction === 'Asc' ? 'Desc' : 'Asc') : 'Desc',
          page: DEFAULT_PAGE,
        },
        true,
      ),
    [query, write],
  );

  const setFilters = useCallback(
    (filters: ProductFilters) => {
      const next: ProductQuery = {
        page: DEFAULT_PAGE,
        pageSize: query.pageSize,
        sortBy: query.sortBy,
        direction: query.direction,
        ...(filters.name ? { name: filters.name } : {}),
        ...(filters.minPrice != null ? { minPrice: filters.minPrice } : {}),
        ...(filters.maxPrice != null ? { maxPrice: filters.maxPrice } : {}),
      };
      write(next, true);
    },
    [query, write],
  );

  /** "Clear" resets all three filters, which switches the list back to `GET /products`. */
  const clearFilters = useCallback(() => setFilters({}), [setFilters]);

  return { query, setPage, setPageSize, toggleSort, setFilters, clearFilters };
}
