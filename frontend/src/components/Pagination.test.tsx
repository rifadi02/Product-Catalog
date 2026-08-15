/** The paging controls — PDR §6.5. */
import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Pagination } from './Pagination';
import { makePage } from '../test/factories';

const noop = () => {};

describe('Pagination', () => {
  it('renders no numbered controls for a single page', () => {
    render(
      <Pagination
        data={makePage([], { totalCount: 3, totalPages: 1 })}
        onPageChange={noop}
        onPageSizeChange={noop}
      />,
    );

    expect(screen.queryByRole('navigation', { name: 'Pagination' })).not.toBeInTheDocument();
  });

  it('never claims "Page 1 of 0" on an empty result', () => {
    // An empty catalogue reports totalPages: 0, not 1 (§6.2).
    render(
      <Pagination
        data={makePage([], { totalCount: 0, totalPages: 0 })}
        onPageChange={noop}
        onPageSizeChange={noop}
      />,
    );

    expect(screen.getByText('No results')).toBeInTheDocument();
    expect(screen.queryByRole('navigation', { name: 'Pagination' })).not.toBeInTheDocument();
  });

  it('marks the current page and announces the range politely', () => {
    render(
      <Pagination
        data={makePage([], { page: 2, pageSize: 20, totalCount: 63, totalPages: 4 })}
        onPageChange={noop}
        onPageSizeChange={noop}
      />,
    );

    expect(screen.getByRole('button', { current: 'page' })).toHaveTextContent('2');

    const label = screen.getByText('Showing 21–40 of 63');
    expect(label).toHaveAttribute('aria-live', 'polite');
  });

  it('keeps disabled controls focusable via aria-disabled', () => {
    render(
      <Pagination
        data={makePage([], { page: 1, totalCount: 63, totalPages: 4 })}
        onPageChange={noop}
        onPageSizeChange={noop}
      />,
    );

    const prev = screen.getByRole('button', { name: '‹ Prev' });
    // Removing them from the tab order would make keyboard users lose their place (§6.5).
    expect(prev).toHaveAttribute('aria-disabled', 'true');
    expect(prev).not.toBeDisabled();
  });

  it('does not navigate when a disabled control is activated', async () => {
    const onPageChange = vi.fn();
    render(
      <Pagination
        data={makePage([], { page: 1, totalCount: 63, totalPages: 4 })}
        onPageChange={onPageChange}
        onPageSizeChange={noop}
      />,
    );

    await userEvent.click(screen.getByRole('button', { name: '‹ Prev' }));
    expect(onPageChange).not.toHaveBeenCalled();
  });

  it('uses the server booleans for button state rather than recomputing them', () => {
    // Requesting a page past the end is a 200 with items: [] and hasNextPage: false (§6.2).
    render(
      <Pagination
        data={makePage([], {
          page: 99,
          totalCount: 63,
          totalPages: 4,
          hasNextPage: false,
          hasPreviousPage: true,
        })}
        onPageChange={noop}
        onPageSizeChange={noop}
      />,
    );

    expect(screen.getByRole('button', { name: 'Next ›' })).toHaveAttribute(
      'aria-disabled',
      'true',
    );
    expect(screen.getByRole('button', { name: '‹ Prev' })).not.toHaveAttribute('aria-disabled');
  });

  it('offers only the four fixed page sizes', () => {
    // Free input would let a 101 through, which is a 400 rather than a clamp (§6.1).
    render(
      <Pagination
        data={makePage([], { totalCount: 63, totalPages: 4 })}
        onPageChange={noop}
        onPageSizeChange={noop}
      />,
    );

    const options = screen.getAllByRole('option').map((o) => o.textContent);
    expect(options).toEqual(['10', '20', '50', '100']);
  });

  it('prefetches on hover over a page button', async () => {
    const onPrefetchPage = vi.fn();
    render(
      <Pagination
        data={makePage([], { page: 1, totalCount: 63, totalPages: 4 })}
        onPageChange={noop}
        onPageSizeChange={noop}
        onPrefetchPage={onPrefetchPage}
      />,
    );

    await userEvent.hover(screen.getByRole('button', { name: 'Next ›' }));
    expect(onPrefetchPage).toHaveBeenCalledWith(2);
  });
});
