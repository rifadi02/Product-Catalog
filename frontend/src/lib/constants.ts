import type { ProductSortField, SortDirection } from '../api/types';

/* ── §6.1 Paging ───────────────────────────────────────────────────────────── */

/**
 * Fixed options only. `[Range(1, 100)]` rejects an oversized `pageSize` with a 400 before the
 * server's clamp is ever reached, so free input and a "show all" control are both traps (§6.1).
 */
export const PAGE_SIZES = [10, 20, 50, 100] as const;
export const DEFAULT_PAGE = 1;
export const DEFAULT_PAGE_SIZE = 20;
export const DEFAULT_SORT_BY: ProductSortField = 'CreatedAt';
export const DEFAULT_DIRECTION: SortDirection = 'Desc';

/** Only three columns are sortable — the API has no `sortBy=UpdatedAt` (§3.4.6). */
export const SORTABLE_COLUMNS: { key: ProductSortField; label: string }[] = [
  { key: 'Name', label: 'Name' },
  { key: 'Price', label: 'Price' },
  { key: 'CreatedAt', label: 'Created' },
];

/* ── §4 Field limits, mirroring the backend's Data Annotations ─────────────── */

export const NAME_MAX = 200;
export const DESCRIPTION_MAX = 2000;
export const PRICE_MAX = 999_999.99;
export const EMAIL_MAX = 256;
export const PASSWORD_MIN = 8;
export const PASSWORD_MAX = 128;

/** Character counters appear only near the limit, not from the first keystroke (§3.6.5). */
export const NAME_COUNTER_FROM = 150;
export const DESCRIPTION_COUNTER_FROM = 1800;

/** §3.4.7 — the name filter debounce. */
export const SEARCH_DEBOUNCE_MS = 300;

/* ── §0.5 Seeded accounts, rendered only under import.meta.env.DEV ─────────── */

export const DEV_ACCOUNTS = [
  { email: 'admin@demo.local', password: 'Admin#2026Demo', role: 'Admin — can delete' },
  { email: 'user@demo.local', password: 'User#2026Demo', role: 'User — can create and update' },
] as const;

/**
 * §4.2 — the backend's denylist, 18 entries, matched case-insensitively. `Password123` is
 * rejected even though it satisfies every complexity rule.
 */
export const COMMON_PASSWORDS = new Set([
  'password',
  'password1',
  'password123',
  'passw0rd',
  '12345678',
  '123456789',
  'qwerty123',
  'letmein1',
  'welcome1',
  'admin123',
  'iloveyou1',
  'abc12345',
  'monkey123',
  'dragon123',
  'football1',
  'baseball1',
  'sunshine1',
  'princess1',
]);
