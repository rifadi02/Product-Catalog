/** Display formatting — PDR §0.3, §3.4.5, §4.3. */
import { describe, expect, it } from 'vitest';
import { formatDate, formatPrice, parseApiDate, roundTo2dp, truncate } from './format';

describe('roundTo2dp', () => {
  it("uses banker's rounding, matching the server's MidpointRounding.ToEven", () => {
    // The two examples the PDR states explicitly (§4.3).
    expect(roundTo2dp(2.345)).toBe(2.34);
    expect(roundTo2dp(2.355)).toBe(2.36);
  });

  it('rounds midpoints to the even neighbour in both directions', () => {
    expect(roundTo2dp(0.125)).toBe(0.12);
    expect(roundTo2dp(0.135)).toBe(0.14);
  });

  it('leaves values already at or under 2dp alone', () => {
    expect(roundTo2dp(24.99)).toBe(24.99);
    expect(roundTo2dp(0)).toBe(0);
    expect(roundTo2dp(999999.99)).toBe(999999.99);
  });

  it('rounds non-midpoints normally', () => {
    expect(roundTo2dp(1.2349)).toBe(1.23);
    expect(roundTo2dp(1.2351)).toBe(1.24);
  });
});

describe('formatPrice', () => {
  it('always shows two decimals, including for zero', () => {
    // 0 renders as 0,00, never as an em-dash (§3.4.5).
    expect(formatPrice(0)).toMatch(/Rp\s?0,00$/);
    expect(formatPrice(24.9)).toMatch(/Rp\s?24,90$/);
    expect(formatPrice(999999.99)).toMatch(/Rp\s?999\.999,99$/);
  });

  it('renders rupiah regardless of the viewer locale', () => {
    // The formatter pins `id-ID`, so a US or GB viewer still sees `Rp`, not `IDR`.
    expect(formatPrice(1000)).not.toContain('IDR');
  });
});

describe('parseApiDate', () => {
  it('reads an ISO-8601 UTC string', () => {
    expect(parseApiDate('2026-08-15T09:41:12.482Z').toISOString()).toBe(
      '2026-08-15T09:41:12.482Z',
    );
  });

  it('treats a zone-less .NET DateTime as UTC rather than local time', () => {
    // Otherwise the value drifts by the viewer's offset (§0.3).
    expect(parseApiDate('2026-08-15T09:41:12.482').toISOString()).toBe(
      '2026-08-15T09:41:12.482Z',
    );
  });
});

describe('formatDate', () => {
  it('renders an em-dash for null, which is what updatedAt is until the first PUT', () => {
    expect(formatDate(null)).toBe('—');
  });
});

describe('truncate', () => {
  it('leaves short names intact', () => {
    expect(truncate('Desk Lamp')).toBe('Desk Lamp');
  });

  it('preserves multi-byte characters when truncating', () => {
    // The seed data deliberately tests accents, emoji and CJK (§0.5).
    const name = 'Café Latte Máquina — Ünïcode Test ☕ 製品 extra padding here';
    expect(truncate(name)).toHaveLength(40);
    expect(truncate(name).endsWith('…')).toBe(true);
  });
});
