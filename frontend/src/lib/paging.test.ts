/** Paging arithmetic — PDR §6.2, §6.5. */
import { describe, expect, it } from 'vitest';
import { ELLIPSIS, pageWindow, rangeLabel } from './paging';

describe('rangeLabel', () => {
  it('describes the current window', () => {
    expect(rangeLabel(2, 20, 63)).toBe('Showing 21–40 of 63');
  });

  it('clamps the upper bound to the total on the last page', () => {
    expect(rangeLabel(4, 20, 63)).toBe('Showing 61–63 of 63');
  });

  it('says "No results" rather than "Showing 1–0 of 0"', () => {
    expect(rangeLabel(1, 20, 0)).toBe('No results');
  });
});

describe('pageWindow', () => {
  it('lists every page when there are seven or fewer', () => {
    expect(pageWindow(1, 4)).toEqual([1, 2, 3, 4]);
    expect(pageWindow(3, 7)).toEqual([1, 2, 3, 4, 5, 6, 7]);
  });

  it('returns nothing for an empty result — totalPages is 0, not 1', () => {
    expect(pageWindow(1, 0)).toEqual([]);
  });

  it('elides the tail when the user is near the start', () => {
    expect(pageWindow(2, 12)).toEqual([1, 2, 3, 4, ELLIPSIS, 12]);
  });

  it('elides both sides in the middle', () => {
    expect(pageWindow(6, 12)).toEqual([1, ELLIPSIS, 5, 6, 7, ELLIPSIS, 12]);
  });

  it('elides the head when the user is near the end', () => {
    expect(pageWindow(11, 12)).toEqual([1, ELLIPSIS, 9, 10, 11, 12]);
  });

  it('never renders more than seven slots', () => {
    for (let page = 1; page <= 40; page++) {
      expect(pageWindow(page, 40).length).toBeLessThanOrEqual(7);
    }
  });
});
