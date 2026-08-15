/**
 * Paging UI — PDR §6.5.
 *
 *   Showing 21–40 of 63       [ 20 ▾ ]        ‹ Prev   1  2  3  4   Next ›
 *
 * Button state comes from `hasNextPage`/`hasPreviousPage`: the server has already done the
 * arithmetic, including the empty-set case, and recomputing it here would be a second source of
 * truth that disagrees at the edges (§6.2).
 */
import { PAGE_SIZES } from '../lib/constants';
import { ELLIPSIS, pageWindow, rangeLabel } from '../lib/paging';
import type { PagedResponse } from '../api/types';

interface PaginationProps {
  data: PagedResponse<unknown>;
  onPageChange: (page: number) => void;
  onPageSizeChange: (pageSize: number) => void;
  onPrefetchPage?: (page: number) => void;
  /** True while a page is in flight — controls stay visible but inert (§3.4.8). */
  isFetching?: boolean;
}

export function Pagination({
  data,
  onPageChange,
  onPageSizeChange,
  onPrefetchPage,
  isFetching = false,
}: PaginationProps) {
  const { page, pageSize, totalCount, totalPages, hasNextPage, hasPreviousPage } = data;

  // The size selector is still useful on a single short page, so only the numbered controls go.
  const showPageControls = totalPages > 1;

  return (
    <div className="flex flex-col gap-3 border-t border-slate-200 px-4 py-3 sm:flex-row sm:items-center sm:justify-between">
      <p className="text-sm text-ink-500" aria-live="polite">
        {rangeLabel(page, pageSize, totalCount)}
      </p>

      <div className="flex items-center gap-4">
        <label className="flex items-center gap-2 text-sm text-ink-500">
          <span className="sr-only sm:not-sr-only">Per page</span>
          <select
            className="input w-auto py-1.5"
            value={pageSize}
            disabled={isFetching}
            onChange={(e) => onPageSizeChange(Number(e.target.value))}
          >
            {PAGE_SIZES.map((size) => (
              <option key={size} value={size}>
                {size}
              </option>
            ))}
          </select>
        </label>

        {showPageControls && (
          <nav aria-label="Pagination" className="flex items-center gap-1">
            <PageButton
              label="‹ Prev"
              disabled={!hasPreviousPage || isFetching}
              onClick={() => onPageChange(page - 1)}
              onHover={() => onPrefetchPage?.(page - 1)}
            />

            {pageWindow(page, totalPages).map((item, index) =>
              item === ELLIPSIS ? (
                <span key={`gap-${index}`} className="px-2 text-ink-500" aria-hidden="true">
                  …
                </span>
              ) : (
                <PageButton
                  key={item}
                  label={String(item)}
                  current={item === page}
                  disabled={isFetching}
                  onClick={() => onPageChange(item)}
                  onHover={() => onPrefetchPage?.(item)}
                />
              ),
            )}

            <PageButton
              label="Next ›"
              disabled={!hasNextPage || isFetching}
              onClick={() => onPageChange(page + 1)}
              onHover={() => onPrefetchPage?.(page + 1)}
            />
          </nav>
        )}
      </div>
    </div>
  );
}

function PageButton({
  label,
  onClick,
  onHover,
  current = false,
  disabled = false,
}: {
  label: string;
  onClick: () => void;
  onHover?: () => void;
  current?: boolean;
  disabled?: boolean;
}) {
  return (
    <button
      type="button"
      // Disabled buttons stay focusable via aria-disabled rather than being removed from the tab
      // order, so keyboard users do not lose their place at the ends of the range (§6.5).
      aria-disabled={disabled || undefined}
      aria-current={current ? 'page' : undefined}
      onClick={() => !disabled && onClick()}
      onMouseEnter={onHover}
      onFocus={onHover}
      className={`min-w-9 rounded-md px-2.5 py-1.5 text-sm font-medium transition ${
        current
          ? 'bg-brand-600 text-white'
          : disabled
            ? 'cursor-not-allowed text-slate-400'
            : 'text-ink-700 hover:bg-slate-100'
      }`}
    >
      {label}
    </button>
  );
}
