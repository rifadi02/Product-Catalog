/**
 * Product ids are 32-bit integers, not GUIDs (§0.3), and the route is constrained to `{id:int}`
 * with `[Range(1, int.MaxValue)]`. `/products/abc` and `/products/0` are 400s server-side; the
 * UI treats them as not-found so that state is unreachable from a pasted URL (§3.5).
 */
export function parseProductId(raw: string | undefined): number | null {
  if (!raw || !/^\d+$/.test(raw)) return null;

  const id = Number(raw);
  return Number.isSafeInteger(id) && id >= 1 ? id : null;
}
