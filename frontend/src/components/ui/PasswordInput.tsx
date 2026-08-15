import { forwardRef, useState } from 'react';
import type { InputHTMLAttributes, ReactNode } from 'react';
import { Field } from './Field';

type PasswordInputProps = Omit<InputHTMLAttributes<HTMLInputElement>, 'id' | 'type'> & {
  label: string;
  error?: string;
  hint?: ReactNode;
};

/** §3.2 — password fields carry a show/hide toggle. */
export const PasswordInput = forwardRef<HTMLInputElement, PasswordInputProps>(
  function PasswordInput({ label, error, hint, className = '', required, ...rest }, ref) {
    const [visible, setVisible] = useState(false);

    return (
      <Field label={label} error={error} hint={hint} required={required}>
        {({ id, describedBy, invalid }) => (
          <div className="relative">
            <input
              {...rest}
              ref={ref}
              id={id}
              type={visible ? 'text' : 'password'}
              aria-invalid={invalid || undefined}
              aria-describedby={describedBy}
              className={`input pr-16 ${invalid ? 'input-invalid' : ''} ${className}`}
            />
            <button
              type="button"
              onClick={() => setVisible((v) => !v)}
              aria-pressed={visible}
              className="absolute inset-y-0 right-0 px-3 text-xs font-medium text-ink-500 hover:text-ink-900"
            >
              {visible ? 'Hide' : 'Show'}
            </button>
          </div>
        )}
      </Field>
    );
  },
);
