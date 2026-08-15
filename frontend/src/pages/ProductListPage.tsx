/**
 * Product list — PDR §3.4. The busiest page: it drives two endpoints and owns all paging,
 * sorting, and filter state (which lives in the URL, §6.4).
 */
import { useEffect, useMemo, useRef, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { describeError, getProblem, logClientBug } from '../api/problem';
import type { Product } from '../api/types';
import { isFiltered } from '../api/products';
import { useDeleteProduct, usePrefetchProducts, useProducts } from '../hooks/useProducts';
import { useProductQueryParams } from '../hooks/useProductQueryParams';
import { useDebouncedValue } from '../hooks/useDebouncedValue';
import { usePermissions } from '../auth/usePermissions';
import { useToast } from '../components/ui/useToast';
import { PRICE_MAX, SEARCH_DEBOUNCE_MS, SORTABLE_COLUMNS } from '../lib/constants';
import { formatDate, formatDateTime, formatPrice, truncate } from '../lib/format';
import { Button } from '../components/ui/Button';
import { PencilIcon, TrashIcon } from '../components/ui/icons';
import { TableSkeleton } from '../components/ui/Skeleton';
import { ErrorState } from '../components/ErrorState';
import { Pagination } from '../components/Pagination';
import { ProductFilters, type FilterDraft } from '../components/products/ProductFilters';
import { ConfirmDeleteModal } from '../components/products/ConfirmDeleteModal';
import type { ProductQuery, ProductSortField } from '../api/types';

const COLUMN_COUNT = 5;

function draftFromQuery(query: ProductQuery): FilterDraft {
  return {
    name: query.name ?? '',
    minPrice: query.minPrice?.toString() ?? '',
    maxPrice: query.maxPrice?.toString() ?? '',
  };
}

const signature = (draft: FilterDraft) => `${draft.name.trim()}|${draft.minPrice}|${draft.maxPrice}`;

function parsePrice(raw: string): { value?: number; invalid: boolean } {
  const trimmed = raw.trim();
  if (trimmed === '') return { invalid: false };

  const value = Number(trimmed);
  if (!Number.isFinite(value) || value < 0 || value > PRICE_MAX) return { invalid: true };

  return { value, invalid: false };
}

export function ProductListPage() {
  const { query, setPage, setPageSize, toggleSort, setFilters, clearFilters } =
    useProductQueryParams();
  const { canWrite, canDelete } = usePermissions();
  const navigate = useNavigate();
  const toast = useToast();

  const { data, error, isPending, isFetching, isPlaceholderData, refetch } = useProducts(query);
  const prefetch = usePrefetchProducts();
  const deleteMutation = useDeleteProduct();

  const [draft, setDraft] = useState<FilterDraft>(() => draftFromQuery(query));
  const [pendingDelete, setPendingDelete] = useState<Product | null>(null);

  // §3.4.7 — the whole draft is debounced together, so typing a name and a bound in one motion
  // produces one request rather than three.
  const debouncedDraft = useDebouncedValue(draft, SEARCH_DEBOUNCE_MS);

  /** Tracks what we last pushed, so our own URL write does not bounce back into the inputs. */
  const lastPushed = useRef(signature(draftFromQuery(query)));

  const min = parsePrice(debouncedDraft.minPrice);
  const max = parsePrice(debouncedDraft.maxPrice);

  const crossFieldError =
    min.value != null && max.value != null && min.value > max.value
      ? 'minPrice must be less than or equal to maxPrice.'
      : min.invalid || max.invalid
        ? `Price must be between 0 and ${PRICE_MAX}.`
        : undefined;

  // URL → inputs. Only fires for navigation we did not cause (Back/Forward, a pasted link).
  useEffect(() => {
    const fromUrl = draftFromQuery(query);
    if (signature(fromUrl) === lastPushed.current) return;

    lastPushed.current = signature(fromUrl);
    setDraft(fromUrl);
  }, [query]);

  // Inputs → URL. `debouncedDraft !== draft` means the debounce has not settled yet; pushing
  // then would resurrect a filter the user just navigated away from.
  useEffect(() => {
    if (debouncedDraft !== draft) return;
    if (crossFieldError) return; // never send a request the server would 400 (§3.4.3)

    const next = signature(debouncedDraft);
    if (next === lastPushed.current) return;

    lastPushed.current = next;
    setFilters({
      name: debouncedDraft.name.trim() || undefined,
      minPrice: min.value,
      maxPrice: max.value,
    });
  }, [debouncedDraft, draft, crossFieldError, min.value, max.value, setFilters]);

  /**
   * §3.4.2 — the UI owns every paging and sorting value, so a 400 here is our bug, not something
   * the user can act on. Log it and fall back to defaults.
   *
   * The ref matters: `clearFilters` changes identity whenever the query does, so without it a
   * reset that does not actually change the URL would re-enter this effect and spin.
   */
  const recoveredFromBadQuery = useRef(false);

  useEffect(() => {
    if (!error) {
      recoveredFromBadQuery.current = false;
      return;
    }
    if (getProblem(error)?.code !== 'validation_failed') return;
    if (recoveredFromBadQuery.current) return;

    recoveredFromBadQuery.current = true;
    logClientBug('GET /products rejected our query parameters', error);
    lastPushed.current = '||';
    setDraft({ name: '', minPrice: '', maxPrice: '' });
    clearFilters();
  }, [error, clearFilters]);

  const nextPageQuery = useMemo<ProductQuery | null>(
    () => (data?.hasNextPage ? { ...query, page: query.page + 1 } : null),
    [data?.hasNextPage, query],
  );

  // §6.3 — prefetch the next page once the current one settles; paging then feels instant.
  useEffect(() => {
    if (nextPageQuery) void prefetch(nextPageQuery);
  }, [nextPageQuery, prefetch]);

  function handleDelete() {
    const target = pendingDelete;
    if (!target) return;

    deleteMutation.mutate(target.id, {
      onSuccess: ({ alreadyGone }) => {
        setPendingDelete(null);

        if (alreadyGone) toast.info(`“${target.name}” had already been removed.`);
        else toast.success(`“${target.name}” was deleted.`);

        // Deleting the only row on a page leaves the user staring at an empty view (§6.2).
        if (data && data.items.length === 1 && query.page > 1) setPage(query.page - 1);
      },
      onError: (deleteError) => {
        setPendingDelete(null);
        const info = describeError(deleteError);
        if (!info.silent) toast.error(info.message, info.traceId);
      },
    });
  }

  const filtered = isFiltered(query);
  const isEmpty = data !== undefined && data.items.length === 0;
  // A validation_failed is handled above by resetting; anything else is worth showing.
  const showError = error && getProblem(error)?.code !== 'validation_failed';

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between gap-4">
        <h1 className="text-2xl font-semibold tracking-tight">Products</h1>
        {canWrite && (
          <Link to="/products/new" className="btn">
            + New product
          </Link>
        )}
      </div>

      {showError && <ErrorState error={error} onRetry={() => void refetch()} />}

      <div className="card overflow-hidden">
        <ProductFilters
          value={draft}
          onChange={setDraft}
          error={crossFieldError}
          onClear={() => {
            lastPushed.current = '||';
            setDraft({ name: '', minPrice: '', maxPrice: '' });
            clearFilters();
          }}
        />

        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead className="bg-slate-50 text-xs uppercase tracking-wide text-ink-500">
              <tr>
                {SORTABLE_COLUMNS.map((column) => (
                  <SortableHeader
                    key={column.key}
                    column={column.key}
                    label={column.label}
                    activeColumn={query.sortBy}
                    direction={query.direction}
                    onSort={toggleSort}
                    numeric={column.key === 'Price'}
                  />
                ))}
                {/* Updated is not sortable — the API has no sortBy=UpdatedAt (§3.4.6). */}
                <th scope="col" className="px-4 py-3 font-medium">
                  Updated
                </th>
                <th scope="col" className="px-4 py-3 text-right font-medium">
                  <span className="sr-only">Actions</span>
                </th>
              </tr>
            </thead>

            <tbody
              // Paging keeps the previous page visible and dims it rather than collapsing to a
              // skeleton on every click (§3.4.8).
              className={isPlaceholderData ? 'opacity-50 transition-opacity' : ''}
            >
              {isPending && <TableSkeleton rows={query.pageSize} columns={COLUMN_COUNT} />}

              {isEmpty && (
                <tr>
                  <td colSpan={COLUMN_COUNT} className="px-4 py-16 text-center">
                    {filtered ? (
                      <>
                        <p className="text-ink-700">No products match your search.</p>
                        <Button
                          variant="secondary"
                          className="mt-4"
                          onClick={() => {
                            lastPushed.current = '||';
                            setDraft({ name: '', minPrice: '', maxPrice: '' });
                            clearFilters();
                          }}
                        >
                          Clear filters
                        </Button>
                      </>
                    ) : (
                      <>
                        <p className="text-ink-700">No products yet.</p>
                        {canWrite && (
                          <Link to="/products/new" className="btn mt-4">
                            Create the first one
                          </Link>
                        )}
                      </>
                    )}
                  </td>
                </tr>
              )}

              {data?.items.map((product) => (
                <tr key={product.id} className="border-t border-slate-100 hover:bg-slate-50">
                  <td className="px-4 py-3">
                    <Link
                      to={`/products/${product.id}`}
                      title={product.name}
                      className="font-medium text-brand-700 hover:underline"
                    >
                      {truncate(product.name)}
                    </Link>
                  </td>
                  <td className="num px-4 py-3">{formatPrice(product.price)}</td>
                  <td className="px-4 py-3 text-ink-500" title={formatDateTime(product.createdAt)}>
                    {formatDate(product.createdAt)}
                  </td>
                  <td className="px-4 py-3 text-ink-500" title={formatDateTime(product.updatedAt)}>
                    {formatDate(product.updatedAt)}
                  </td>
                  <td className="px-4 py-3">
                    <div className="flex justify-end gap-1">
                      {canWrite && (
                        <Button
                          variant="ghost"
                          size="sm"
                          aria-label={`Edit ${product.name}`}
                          title="Edit"
                          onClick={() => navigate(`/products/${product.id}/edit`)}
                        >
                          <PencilIcon />
                        </Button>
                      )}
                      {canDelete && (
                        <Button
                          variant="ghost"
                          size="sm"
                          aria-label={`Delete ${product.name}`}
                          title="Delete"
                          className="text-red-700 hover:bg-red-50"
                          onClick={() => setPendingDelete(product)}
                        >
                          <TrashIcon />
                        </Button>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        {data && (
          <Pagination
            data={data}
            isFetching={isFetching && isPlaceholderData}
            onPageChange={setPage}
            onPageSizeChange={setPageSize}
            onPrefetchPage={(page) => {
              if (page >= 1 && page <= data.totalPages) void prefetch({ ...query, page });
            }}
          />
        )}
      </div>

      <ConfirmDeleteModal
        product={pendingDelete}
        isPending={deleteMutation.isPending}
        onCancel={() => setPendingDelete(null)}
        onConfirm={handleDelete}
      />
    </div>
  );
}

function SortableHeader({
  column,
  label,
  activeColumn,
  direction,
  onSort,
  numeric,
}: {
  column: ProductSortField;
  label: string;
  activeColumn: ProductSortField;
  direction: 'Asc' | 'Desc';
  onSort: (column: ProductSortField) => void;
  numeric?: boolean;
}) {
  const isActive = activeColumn === column;

  return (
    <th
      scope="col"
      className={`px-4 py-3 font-medium ${numeric ? 'text-right' : ''}`}
      aria-sort={isActive ? (direction === 'Asc' ? 'ascending' : 'descending') : 'none'}
    >
      <button
        type="button"
        onClick={() => onSort(column)}
        className="inline-flex items-center gap-1 uppercase tracking-wide hover:text-ink-900"
      >
        {label}
        <span aria-hidden="true" className={isActive ? '' : 'opacity-0'}>
          {direction === 'Asc' ? '▲' : '▼'}
        </span>
      </button>
    </th>
  );
}
