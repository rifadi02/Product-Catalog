/** §3.4.7 — the filter row. Purely presentational; the list page owns the state. */
import { PRICE_MAX } from '../../lib/constants';
import { Button } from '../ui/Button';

export interface FilterDraft {
  name: string;
  minPrice: string;
  maxPrice: string;
}

export function ProductFilters({
  value,
  onChange,
  onClear,
  error,
}: {
  value: FilterDraft;
  onChange: (next: FilterDraft) => void;
  onClear: () => void;
  /** The cross-field message lands under min price, matching the server's key (§4.4). */
  error?: string;
}) {
  const isDirty = Boolean(value.name || value.minPrice || value.maxPrice);

  return (
    <div className="border-b border-slate-200 px-4 py-3">
      <div className="flex flex-wrap items-end gap-3">
        <div className="min-w-52 flex-1">
          <label className="label" htmlFor="filter-name">
            Search name
          </label>
          <input
            id="filter-name"
            type="search"
            className="input"
            placeholder="Search name…"
            value={value.name}
            onChange={(e) => onChange({ ...value, name: e.target.value })}
          />
        </div>

        <div className="w-28">
          <label className="label" htmlFor="filter-min">
            Min price
          </label>
          <input
            id="filter-min"
            type="number"
            step="0.01"
            min="0"
            max={PRICE_MAX}
            className={`input num ${error ? 'input-invalid' : ''}`}
            aria-invalid={error ? true : undefined}
            aria-describedby={error ? 'filter-price-error' : undefined}
            value={value.minPrice}
            onChange={(e) => onChange({ ...value, minPrice: e.target.value })}
          />
        </div>

        <div className="w-28">
          <label className="label" htmlFor="filter-max">
            Max price
          </label>
          <input
            id="filter-max"
            type="number"
            step="0.01"
            min="0"
            max={PRICE_MAX}
            className="input num"
            value={value.maxPrice}
            onChange={(e) => onChange({ ...value, maxPrice: e.target.value })}
          />
        </div>

        <Button variant="secondary" onClick={onClear} disabled={!isDirty}>
          Clear
        </Button>
      </div>

      {error && (
        <p id="filter-price-error" className="mt-2 text-sm text-red-700" role="alert">
          {error}
        </p>
      )}
    </div>
  );
}
