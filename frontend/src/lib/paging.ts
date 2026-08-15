/** Pure paging arithmetic for the §6.5 controls, kept separate so it is directly testable. */

export const ELLIPSIS = 'ellipsis' as const;

const MAX_BUTTONS = 7;

/** A ≤7-button window with ellipses: `‹ Prev  1 … 4 [5] 6 … 12  Next ›`. */
export function pageWindow(page: number, totalPages: number): (number | typeof ELLIPSIS)[] {
  if (totalPages <= MAX_BUTTONS) {
    return Array.from({ length: totalPages }, (_, i) => i + 1);
  }

  const items: (number | typeof ELLIPSIS)[] = [1];

  // Two slots go to the first and last page and two to the ellipses, leaving three around the
  // current page.
  let start = Math.max(2, page - 1);
  let end = Math.min(totalPages - 1, page + 1);

  if (page <= 3) {
    start = 2;
    end = 4;
  } else if (page >= totalPages - 2) {
    start = totalPages - 3;
    end = totalPages - 1;
  }

  if (start > 2) items.push(ELLIPSIS);
  for (let p = start; p <= end; p++) items.push(p);
  if (end < totalPages - 1) items.push(ELLIPSIS);

  items.push(totalPages);
  return items;
}

export function rangeLabel(page: number, pageSize: number, totalCount: number): string {
  // "Showing 1–0 of 0" is what the naive arithmetic produces on an empty result (§6.5).
  if (totalCount === 0) return 'No results';

  const first = (page - 1) * pageSize + 1;
  const last = Math.min(page * pageSize, totalCount);

  return `Showing ${first}–${last} of ${totalCount}`;
}
