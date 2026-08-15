/** Product queries and mutations — PDR §3.6, §3.7, §6.3. */
import { useCallback } from 'react';
import {
  keepPreviousData,
  useMutation,
  useQuery,
  useQueryClient,
  type QueryClient,
} from '@tanstack/react-query';
import {
  createProduct,
  deleteProduct,
  getProduct,
  listProducts,
  updateProduct,
} from '../api/products';
import { productKeys } from '../api/queryKeys';
import { hasCode } from '../api/problem';
import type { PagedResponse, Product, ProductQuery, ProductSnapshot, ProductWriteRequest } from '../api/types';

export function useProducts(query: ProductQuery) {
  return useQuery<PagedResponse<Product>>({
    queryKey: productKeys.list(query),
    // Passing `signal` matters: an abandoned request that lands after a fast page-click would
    // otherwise render stale rows. Aborted requests are `request_cancelled` server-side, and silent.
    queryFn: ({ signal }) => listProducts(query, signal),
    // Keeps the previous page on screen while the next one loads, instead of collapsing the
    // table to a skeleton on every page click (§3.4.8).
    placeholderData: keepPreviousData,
    staleTime: 30_000,
  });
}

const detailOptions = (id: number) => ({
  queryKey: productKeys.detail(id),
  queryFn: ({ signal }: { signal: AbortSignal }) => getProduct(id, signal),
  staleTime: 30_000,
});

export function useProduct(id: number, enabled = true) {
  return useQuery<ProductSnapshot>({ ...detailOptions(id), enabled });
}

/** §6.3 — one extra call on hover makes paging feel instant. */
export function usePrefetchProducts() {
  const queryClient = useQueryClient();

  return useCallback(
    (query: ProductQuery) =>
      queryClient.prefetchQuery({
        queryKey: productKeys.list(query),
        queryFn: ({ signal }) => listProducts(query, signal),
        staleTime: 30_000,
      }),
    [queryClient],
  );
}

/**
 * The ETag captured by the detail GET is what makes the PUT a compare-and-swap. If it is missing
 * from cache, fetch the product to obtain one rather than sending an unconditional write (§3.6.3).
 */
async function resolveEtag(queryClient: QueryClient, id: number): Promise<string> {
  const cached = queryClient.getQueryData<ProductSnapshot>(productKeys.detail(id));
  if (cached?.etag) return cached.etag;

  const fresh = await queryClient.fetchQuery(detailOptions(id));
  if (!fresh.etag) throw new Error('missing_etag');

  return fresh.etag;
}

export function useCreateProduct() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (body: ProductWriteRequest) => createProduct(body),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: productKeys.all }),
  });
}

export function useUpdateProduct(id: number) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (body: ProductWriteRequest) => {
      const etag = await resolveEtag(queryClient, id);
      await updateProduct(id, body, etag);
    },
    // The 204 carries no body and no new ETag, so there is nothing to merge — invalidate and
    // refetch, or the stale ETag breaks the *next* edit (§3.6.2).
    //
    // A 409 is deliberately *not* handled here. The conflict flow needs the form to stay mounted
    // with the user's edits intact (§3.6.4), so the page refetches the detail itself — which
    // replaces the stale ETag in cache without ever dropping the query.
    onSuccess: () => queryClient.invalidateQueries({ queryKey: productKeys.all }),
  });
}

export function useDeleteProduct() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (id: number) => {
      try {
        await deleteProduct(id);
        return { alreadyGone: false };
      } catch (error) {
        // Deletion is a soft delete and a second DELETE returns 404, not another 204. From the
        // user's point of view the product is gone, which is what they asked for (§3.7).
        if (hasCode(error, 'resource_not_found')) return { alreadyGone: true };
        throw error;
      }
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: productKeys.all }),
  });
}
