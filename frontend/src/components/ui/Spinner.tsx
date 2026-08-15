export function Spinner({ className = 'size-5' }: { className?: string }) {
  return (
    <svg className={`animate-spin ${className}`} viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <circle cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" className="opacity-25" />
      <path
        d="M12 2a10 10 0 0 1 10 10"
        stroke="currentColor"
        strokeWidth="4"
        strokeLinecap="round"
        className="opacity-90"
      />
    </svg>
  );
}

/**
 * Shown while the boot sequence resolves. Route guards render this rather than redirecting, or a
 * reload on `/products/new` bounces the user to `/login` before rehydration finishes (§5.6).
 */
export function FullPageSpinner({ label = 'Loading' }: { label?: string }) {
  return (
    <div className="flex min-h-[60vh] items-center justify-center" role="status" aria-live="polite">
      <Spinner className="size-8 text-brand-600" />
      <span className="sr-only">{label}</span>
    </div>
  );
}
