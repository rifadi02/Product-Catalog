export function Skeleton({ className = '' }: { className?: string }) {
  return <div className={`animate-pulse rounded bg-slate-200 ${className}`} aria-hidden="true" />;
}

/** §3.4.8 — the first load renders skeleton rows matching `pageSize`, not a spinner. */
export function TableSkeleton({ rows, columns }: { rows: number; columns: number }) {
  return (
    <>
      {Array.from({ length: rows }, (_, row) => (
        <tr key={row} className="border-t border-slate-100">
          {Array.from({ length: columns }, (_, column) => (
            <td key={column} className="px-4 py-3">
              <Skeleton className="h-4 w-full max-w-40" />
            </td>
          ))}
        </tr>
      ))}
    </>
  );
}
