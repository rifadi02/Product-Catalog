import { forwardRef, useId } from 'react';
import type { InputHTMLAttributes, ReactNode, TextareaHTMLAttributes } from 'react';

interface FieldShellProps {
  label: string;
  error?: string;
  hint?: ReactNode;
  /** Rendered to the right of the label — the character counters live here (§3.6.5). */
  adornment?: ReactNode;
  required?: boolean;
  children: (ids: { id: string; describedBy: string | undefined; invalid: boolean }) => ReactNode;
}

export function Field({
  label,
  error,
  hint,
  adornment,
  required,
  children,
}: FieldShellProps) {
  const id = useId();
  const errorId = `${id}-error`;
  const hintId = `${id}-hint`;
  const describedBy = [error ? errorId : null, hint ? hintId : null].filter(Boolean).join(' ');

  return (
    <div>
      <div className="flex items-baseline justify-between gap-2">
        <label htmlFor={id} className="label">
          {label}
          {required && (
            <span className="text-red-600" aria-hidden="true">
              {' '}
              *
            </span>
          )}
        </label>
        {adornment}
      </div>

      {children({ id, describedBy: describedBy || undefined, invalid: Boolean(error) })}

      {hint && (
        <p id={hintId} className="mt-1 text-xs text-ink-500">
          {hint}
        </p>
      )}
      {error && (
        <p id={errorId} className="mt-1 text-sm text-red-700">
          {error}
        </p>
      )}
    </div>
  );
}

type TextInputProps = Omit<InputHTMLAttributes<HTMLInputElement>, 'id'> & {
  label: string;
  error?: string;
  hint?: ReactNode;
  adornment?: ReactNode;
};

export const TextInput = forwardRef<HTMLInputElement, TextInputProps>(function TextInput(
  { label, error, hint, adornment, className = '', required, ...rest },
  ref,
) {
  return (
    <Field label={label} error={error} hint={hint} adornment={adornment} required={required}>
      {({ id, describedBy, invalid }) => (
        <input
          {...rest}
          ref={ref}
          id={id}
          aria-invalid={invalid || undefined}
          aria-describedby={describedBy}
          className={`input ${invalid ? 'input-invalid' : ''} ${className}`}
        />
      )}
    </Field>
  );
});

type TextareaProps = Omit<TextareaHTMLAttributes<HTMLTextAreaElement>, 'id'> & {
  label: string;
  error?: string;
  hint?: ReactNode;
  adornment?: ReactNode;
};

export const Textarea = forwardRef<HTMLTextAreaElement, TextareaProps>(function Textarea(
  { label, error, hint, adornment, className = '', required, ...rest },
  ref,
) {
  return (
    <Field label={label} error={error} hint={hint} adornment={adornment} required={required}>
      {({ id, describedBy, invalid }) => (
        <textarea
          {...rest}
          ref={ref}
          id={id}
          aria-invalid={invalid || undefined}
          aria-describedby={describedBy}
          className={`input ${invalid ? 'input-invalid' : ''} ${className}`}
        />
      )}
    </Field>
  );
});

/** Appears only near the limit, so it is information rather than noise (§3.6.5). */
export function CharCounter({
  value,
  max,
  from,
}: {
  value: string;
  max: number;
  from: number;
}) {
  if (value.length < from) return null;

  return (
    <span
      className={`text-xs tabular-nums ${value.length > max ? 'text-red-700' : 'text-ink-500'}`}
      aria-live="polite"
    >
      {value.length} / {max}
    </span>
  );
}
