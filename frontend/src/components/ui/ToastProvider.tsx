import { createContext, useCallback, useMemo, useRef, useState } from 'react';
import type { ReactNode } from 'react';

export type ToastTone = 'success' | 'error' | 'info';

export interface Toast {
  id: number;
  tone: ToastTone;
  message: string;
  /** Errors carry the correlation id so a user can quote it (§1.1). */
  traceId?: string;
}

export interface ToastApi {
  show: (toast: Omit<Toast, 'id'>) => void;
  success: (message: string) => void;
  error: (message: string, traceId?: string) => void;
  info: (message: string) => void;
  dismiss: (id: number) => void;
}

// eslint-disable-next-line react-refresh/only-export-components
export const ToastContext = createContext<ToastApi | null>(null);

const TONES: Record<ToastTone, string> = {
  success: 'border-emerald-200 bg-emerald-50 text-emerald-900',
  error: 'border-red-200 bg-red-50 text-red-900',
  info: 'border-slate-200 bg-white text-ink-900',
};

const DISMISS_AFTER_MS = 6000;

export function ToastProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<Toast[]>([]);
  const nextId = useRef(1);

  const dismiss = useCallback((id: number) => {
    setToasts((current) => current.filter((t) => t.id !== id));
  }, []);

  const show = useCallback(
    (toast: Omit<Toast, 'id'>) => {
      const id = nextId.current++;
      setToasts((current) => [...current, { ...toast, id }]);
      setTimeout(() => dismiss(id), DISMISS_AFTER_MS);
    },
    [dismiss],
  );

  const api = useMemo<ToastApi>(
    () => ({
      show,
      dismiss,
      success: (message) => show({ tone: 'success', message }),
      error: (message, traceId) => show({ tone: 'error', message, traceId }),
      info: (message) => show({ tone: 'info', message }),
    }),
    [show, dismiss],
  );

  return (
    <ToastContext.Provider value={api}>
      {children}
      <div
        className="pointer-events-none fixed inset-x-0 bottom-0 z-50 flex flex-col items-center gap-2 p-4"
        role="status"
        aria-live="polite"
      >
        {toasts.map((toast) => (
          <div
            key={toast.id}
            className={`pointer-events-auto w-full max-w-md rounded-lg border px-4 py-3 text-sm shadow-lg ${TONES[toast.tone]}`}
          >
            <div className="flex items-start justify-between gap-3">
              <div className="min-w-0">
                <p>{toast.message}</p>
                {toast.traceId && (
                  <p className="mt-1 font-mono text-xs opacity-70">
                    Reference: <span className="select-all">{toast.traceId}</span>
                  </p>
                )}
              </div>
              <button
                type="button"
                onClick={() => dismiss(toast.id)}
                className="shrink-0 text-xs font-medium opacity-70 hover:opacity-100"
              >
                Dismiss
              </button>
            </div>
          </div>
        ))}
      </div>
    </ToastContext.Provider>
  );
}
