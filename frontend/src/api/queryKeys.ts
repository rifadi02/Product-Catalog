import type { ProductQuery } from './types';

/**
 * §6.3 — the whole parameter object goes in the key so every distinct view caches separately,
 * and `productKeys.all` invalidates every cached page and filter combination in one call.
 */
export const productKeys = {
  all: ['products'] as const,
  list: (q: ProductQuery) => [...productKeys.all, 'list', q] as const,
  detail: (id: number) => [...productKeys.all, 'detail', id] as const,
};

export const authKeys = {
  me: ['auth', 'me'] as const,
};
