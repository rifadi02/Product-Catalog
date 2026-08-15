import type { ButtonHTMLAttributes, ReactNode } from 'react';
import { Spinner } from './Spinner';

type Variant = 'primary' | 'secondary' | 'danger' | 'ghost';

const VARIANTS: Record<Variant, string> = {
  primary: '',
  secondary: 'btn-secondary',
  danger: 'btn-danger',
  ghost: 'btn-ghost',
};

export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: Variant;
  size?: 'sm' | 'md';
  /** Renders an in-button spinner and disables the control (§3.2). */
  isPending?: boolean;
  children?: ReactNode;
}

export function Button({
  variant = 'primary',
  size = 'md',
  isPending = false,
  disabled,
  className = '',
  children,
  ...rest
}: ButtonProps) {
  return (
    <button
      type="button"
      {...rest}
      disabled={disabled || isPending}
      className={`btn ${VARIANTS[variant]} ${size === 'sm' ? 'btn-sm' : ''} ${className}`}
    >
      {isPending && <Spinner className="size-4" />}
      {children}
    </button>
  );
}
