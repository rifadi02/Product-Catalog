import type { ReactNode } from 'react';

const TONES = {
  brand: 'bg-brand-50 text-brand-700 ring-1 ring-brand-200',
  neutral: 'bg-slate-100 text-ink-700 ring-1 ring-slate-200',
  admin: 'bg-amber-100 text-amber-900 ring-1 ring-amber-200',
} as const;

export function Badge({
  tone = 'neutral',
  children,
}: {
  tone?: keyof typeof TONES;
  children: ReactNode;
}) {
  return <span className={`badge ${TONES[tone]}`}>{children}</span>;
}
