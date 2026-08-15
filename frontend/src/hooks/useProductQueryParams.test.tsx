/** URL as the source of truth — PDR §6.4. */
import { describe, expect, it } from 'vitest';
import { act, renderHook } from '@testing-library/react';
import { MemoryRouter, useLocation } from 'react-router-dom';
import type { ReactNode } from 'react';
import {
  parseProductQuery,
  toSearchParams,
  useProductQueryParams,
} from './useProductQueryParams';

describe('parseProductQuery', () => {
  it('falls back to the documented defaults', () => {
    expect(parseProductQuery(new URLSearchParams())).toEqual({
      page: 1,
      pageSize: 20,
      sortBy: 'CreatedAt',
      direction: 'Desc',
    });
  });

  it('reads a full query string', () => {
    const params = new URLSearchParams(
      'page=2&pageSize=50&sortBy=Price&direction=Asc&name=lamp&minPrice=10&maxPrice=50',
    );

    expect(parseProductQuery(params)).toEqual({
      page: 2,
      pageSize: 50,
      sortBy: 'Price',
      direction: 'Asc',
      name: 'lamp',
      minPrice: 10,
      maxPrice: 50,
    });
  });

  it('clamps hand-edited values that the API would reject with a 400', () => {
    // The UI owns these values entirely, so a 400 here would be a client bug the user cannot act
    // on (§3.4.2). Never send one.
    expect(parseProductQuery(new URLSearchParams('page=0')).page).toBe(1);
    expect(parseProductQuery(new URLSearchParams('pageSize=101')).pageSize).toBe(20);
    expect(parseProductQuery(new URLSearchParams('pageSize=7')).pageSize).toBe(20);
    expect(parseProductQuery(new URLSearchParams('sortBy=colour')).sortBy).toBe('CreatedAt');
    expect(parseProductQuery(new URLSearchParams('direction=sideways')).direction).toBe('Desc');
    expect(parseProductQuery(new URLSearchParams('minPrice=-5')).minPrice).toBeUndefined();
    expect(parseProductQuery(new URLSearchParams('maxPrice=1000000')).maxPrice).toBeUndefined();
  });

  it('drops the lower bound when it exceeds the upper one', () => {
    // The server reports this cross-field failure as a 400 under `minPrice` (§4.4).
    const query = parseProductQuery(new URLSearchParams('minPrice=50&maxPrice=10'));
    expect(query.minPrice).toBeUndefined();
    expect(query.maxPrice).toBe(10);
  });
});

describe('toSearchParams', () => {
  it('omits every parameter that is at its default', () => {
    const params = toSearchParams({
      page: 1,
      pageSize: 20,
      sortBy: 'CreatedAt',
      direction: 'Desc',
    });
    expect(params.toString()).toBe('');
  });

  it('round-trips a non-default query', () => {
    const query = {
      page: 3,
      pageSize: 50,
      sortBy: 'Name' as const,
      direction: 'Asc' as const,
      name: 'lamp',
      minPrice: 10,
    };
    expect(parseProductQuery(toSearchParams(query))).toEqual(query);
  });
});

function wrapper(initial: string) {
  return function Wrapper({ children }: { children: ReactNode }) {
    return <MemoryRouter initialEntries={[initial]}>{children}</MemoryRouter>;
  };
}

const useHarness = () => ({ ...useProductQueryParams(), location: useLocation() });

describe('useProductQueryParams', () => {
  it('toggles direction when the sorted column is clicked again', () => {
    const { result } = renderHook(useHarness, {
      wrapper: wrapper('/products?sortBy=Name&direction=Asc'),
    });

    act(() => result.current.toggleSort('Name'));
    expect(result.current.query.direction).toBe('Desc');
  });

  it('starts a newly sorted column at Desc and resets the page', () => {
    const { result } = renderHook(useHarness, {
      wrapper: wrapper('/products?page=4&sortBy=Name&direction=Asc'),
    });

    act(() => result.current.toggleSort('Price'));

    expect(result.current.query.sortBy).toBe('Price');
    expect(result.current.query.direction).toBe('Desc');
    // Leaving the user on page 4 of a re-sorted list can land them past the end (§3.4.6).
    expect(result.current.query.page).toBe(1);
  });

  it('resets the page when the page size changes', () => {
    const { result } = renderHook(useHarness, { wrapper: wrapper('/products?page=4') });

    act(() => result.current.setPageSize(100));

    expect(result.current.query.pageSize).toBe(100);
    expect(result.current.query.page).toBe(1);
  });

  it('resets the page when a filter changes', () => {
    const { result } = renderHook(useHarness, { wrapper: wrapper('/products?page=3') });

    act(() => result.current.setFilters({ name: 'lamp' }));

    expect(result.current.query.name).toBe('lamp');
    expect(result.current.query.page).toBe(1);
  });

  it('clears every filter, which switches the list back to GET /products', () => {
    const { result } = renderHook(useHarness, {
      wrapper: wrapper('/products?name=lamp&minPrice=10&maxPrice=50&sortBy=Price'),
    });

    act(() => result.current.clearFilters());

    expect(result.current.query.name).toBeUndefined();
    expect(result.current.query.minPrice).toBeUndefined();
    expect(result.current.query.maxPrice).toBeUndefined();
    // Sorting is not a filter and survives the reset.
    expect(result.current.query.sortBy).toBe('Price');
  });

  it('keeps the URL clean when returning to defaults', () => {
    const { result } = renderHook(useHarness, { wrapper: wrapper('/products?page=2') });

    act(() => result.current.setPage(1));

    expect(result.current.location.search).toBe('');
  });
});
