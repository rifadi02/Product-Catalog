import type { ReactNode } from 'react';

type Tone = 'error' | 'warning' | 'success' | 'info';

const TONES: Record<Tone, string> = {
  error: 'border-red-200 bg-red-50 text-red-900',
  warning: 'border-amber-200 bg-amber-50 text-amber-900',
  success: 'border-emerald-200 bg-emerald-50 text-emerald-900',
  info: 'border-slate-200 bg-slate-50 text-ink-900',
};

export interface AlertProps {
  tone?: Tone;
  title?: string;
  children?: ReactNode;
  /** Shown in mono so a user can quote it in a bug report (§1.1). */
  traceId?: string;
  action?: ReactNode;
  className?: string;
}

export function Alert({
  tone = 'error',
  title,
  children,
  traceId,
  action,
  className = '',
}: AlertProps) {
  return (
    <div
      // Errors interrupt; everything else is announced politely.
      role={tone === 'error' ? 'alert' : 'status'}
      className={`rounded-lg border px-4 py-3 text-sm ${TONES[tone]} ${className}`}
    >
      <div className="flex items-start justify-between gap-4">
        <div className="min-w-0">
          {title && <p className="font-semibold">{title}</p>}
          {children && <div className={title ? 'mt-1' : ''}>{children}</div>}
          {traceId && (
            <p className="mt-2 font-mono text-xs opacity-70">
              Reference: <span className="select-all">{traceId}</span>
            </p>
          )}
        </div>
        {action && <div className="shrink-0">{action}</div>}
      </div>
    </div>
  );
}
