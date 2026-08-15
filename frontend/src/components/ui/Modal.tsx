import { useCallback, useEffect, useId, useRef } from 'react';
import { createPortal } from 'react-dom';
import type { ReactNode } from 'react';

const FOCUSABLE =
  'a[href], button:not([disabled]), textarea, input, select, [tabindex]:not([tabindex="-1"])';

export interface ModalProps {
  open: boolean;
  title: string;
  onClose: () => void;
  children: ReactNode;
  footer?: ReactNode;
  /** Widen for the side-by-side conflict comparison (§3.6.4). */
  size?: 'sm' | 'lg';
}

/**
 * The dialog contract from §3.7, shared by the delete confirmation and the conflict resolver:
 * focus trapped, focus restored on close, Escape cancels, and **Enter does not confirm** — the
 * dialog is not a form and the destructive button is never the default action.
 */
export function Modal({ open, title, onClose, children, footer, size = 'sm' }: ModalProps) {
  const panel = useRef<HTMLDivElement>(null);
  const previouslyFocused = useRef<HTMLElement | null>(null);
  const titleId = useId();

  const focusables = useCallback(
    () => Array.from(panel.current?.querySelectorAll<HTMLElement>(FOCUSABLE) ?? []),
    [],
  );

  useEffect(() => {
    if (!open) return;

    previouslyFocused.current = document.activeElement as HTMLElement | null;

    // Prefer whatever the caller marked autoFocus — the delete modal points that at Cancel, so
    // a reflexive Enter dismisses rather than destroys.
    const preferred = panel.current?.querySelector<HTMLElement>('[data-autofocus]');
    (preferred ?? focusables()[0] ?? panel.current)?.focus();

    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = 'hidden';

    return () => {
      document.body.style.overflow = previousOverflow;
      previouslyFocused.current?.focus?.();
    };
  }, [open, focusables]);

  useEffect(() => {
    if (!open) return;

    function onKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        event.stopPropagation();
        onClose();
        return;
      }

      if (event.key !== 'Tab') return;

      const items = focusables();
      const first = items[0];
      const last = items[items.length - 1];
      if (!first || !last) return;

      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault();
        last.focus();
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault();
        first.focus();
      }
    }

    document.addEventListener('keydown', onKeyDown, true);
    return () => document.removeEventListener('keydown', onKeyDown, true);
  }, [open, onClose, focusables]);

  if (!open) return null;

  return createPortal(
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      <div
        className="absolute inset-0 bg-ink-900/40"
        onClick={onClose}
        aria-hidden="true"
      />
      <div
        ref={panel}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        tabIndex={-1}
        className={`card relative w-full ${size === 'lg' ? 'max-w-2xl' : 'max-w-md'} p-6`}
      >
        <h2 id={titleId} className="text-lg font-semibold">
          {title}
        </h2>
        <div className="mt-3 text-sm text-ink-700">{children}</div>
        {footer && <div className="mt-6 flex justify-end gap-2">{footer}</div>}
      </div>
    </div>,
    document.body,
  );
}
