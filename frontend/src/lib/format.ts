/** Display formatting — PDR §0.3, §3.4.5, §4.3. */

/**
 * The catalogue has a single currency and the API never names it, so it is one constant here.
 */
export const CURRENCY = 'IDR';

/**
 * Pinned to `id-ID` rather than the viewer's locale: elsewhere a locale-appropriate rendering is
 * the point, but for a fixed currency it would spell IDR out as `IDR 1,000.00` for most viewers
 * instead of `Rp 1.000,00`. The grouping and decimal separators have to follow the symbol.
 */
const PRICE_LOCALE = 'id-ID';

/**
 * Two decimals even though rupiah is conventionally whole-number: the server stores and validates
 * a 2dp `decimal` (§4.3), and hiding the minor units would round the displayed price away from
 * what is actually stored.
 */
const priceFormatter = new Intl.NumberFormat(PRICE_LOCALE, {
  style: 'currency',
  currency: CURRENCY,
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
});

/** `0` renders as the currency's zero, never as an em-dash — a Rp 0,00 product is real (§3.4.5). */
export const formatPrice = (value: number) => priceFormatter.format(value);

/**
 * .NET serializes `DateTime` rather than `DateTimeOffset`. Values are UTC, and normally carry a
 * `Z`; a value that arrives without one would otherwise be parsed as local time and drift by the
 * viewer's offset (§0.3).
 */
export function parseApiDate(iso: string): Date {
  const hasZone = /(?:Z|[+-]\d{2}:?\d{2})$/i.test(iso);
  return new Date(hasZone ? iso : `${iso}Z`);
}

const shortDate = new Intl.DateTimeFormat(undefined, { dateStyle: 'medium' });
const fullDateTime = new Intl.DateTimeFormat(undefined, {
  dateStyle: 'medium',
  timeStyle: 'medium',
});

export function formatDate(iso: string | null): string {
  if (!iso) return '—';
  const date = parseApiDate(iso);
  return Number.isNaN(date.getTime()) ? '—' : shortDate.format(date);
}

export function formatDateTime(iso: string | null): string {
  if (!iso) return '—';
  const date = parseApiDate(iso);
  return Number.isNaN(date.getTime()) ? '—' : fullDateTime.format(date);
}

/**
 * Banker's rounding to 2dp, matching the server's `MidpointRounding.ToEven` on `decimal`
 * (§4.3): 2.345 → 2.34, 2.355 → 2.36.
 *
 * The arithmetic runs on the number's shortest decimal representation rather than on the double
 * itself. `2.355 * 100` is `235.49999999999997` in binary floating point, so a naive
 * scale-and-round would answer 2.35 and disagree with what the server stores.
 */
export function roundTo2dp(value: number): number {
  if (!Number.isFinite(value)) return value;

  const text = Math.abs(value).toString();
  if (text.includes('e') || text.includes('E')) return Number(value.toFixed(2));

  const [whole = '0', fraction = ''] = text.split('.');
  if (fraction.length <= 2) return value;

  const sign = value < 0 ? -1 : 1;
  const kept = fraction.slice(0, 2).padEnd(2, '0');
  const rest = fraction.slice(2);
  const scaled = Number(`${whole}${kept}`);

  const isMidpoint = /^50*$/.test(rest);
  const roundsUp = isMidpoint ? scaled % 2 !== 0 : Number(rest[0]) >= 5;

  return (sign * (scaled + (roundsUp ? 1 : 0))) / 100;
}

/** Table cells truncate long names; the full text stays available in `title` (§3.4.5). */
export function truncate(text: string, max = 40): string {
  return text.length <= max ? text : `${text.slice(0, max - 1)}…`;
}
