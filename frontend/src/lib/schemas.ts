/**
 * Client-side validation — PDR §4, with the backend's exact message strings so the two agree
 * whichever one speaks first.
 *
 * The server is authoritative. These schemas are a UX affordance, never a gate: every form must
 * still render the server's `errors` bag (§1.3).
 *
 * Note the backend trims every inbound string before validating, so `"   "` in a required field
 * arrives as `""` and fails "required", not "min length". Every schema trims to match.
 *
 * Numeric fields are modelled as strings rather than with `z.coerce.number()`. Coercion turns an
 * empty input into `0`, which passes `min(0)` — and since `0` is a genuinely valid price (§4.3),
 * an empty box would silently create a free product. The rules and messages are identical.
 */
import { z } from 'zod';
import {
  COMMON_PASSWORDS,
  DESCRIPTION_MAX,
  EMAIL_MAX,
  NAME_MAX,
  PASSWORD_MAX,
  PASSWORD_MIN,
  PRICE_MAX,
} from './constants';
import type { ProductWriteRequest } from '../api/types';
import { roundTo2dp } from './format';

const email = z
  .string()
  .trim()
  .min(1, 'Email is required.')
  .email('Email is not a valid address.')
  .max(EMAIL_MAX, `Email must not exceed ${EMAIL_MAX} characters.`);

/* ── §4.1 Login ────────────────────────────────────────────────────────────── */

/**
 * There is no complexity rule on login — only on register. Never block a sign-in attempt
 * because the typed password looks weak; the account may predate the current rules.
 */
export const loginSchema = z.object({
  email,
  password: z.string().min(1, 'Password is required.').max(PASSWORD_MAX),
});

export type LoginFormValues = z.infer<typeof loginSchema>;

/* ── §4.2 Register ─────────────────────────────────────────────────────────── */

/** The live checklist on the register form renders from exactly these predicates (§3.3). */
export const PASSWORD_RULES = [
  { label: `At least ${PASSWORD_MIN} characters`, test: (p: string) => p.length >= PASSWORD_MIN },
  { label: 'One uppercase letter', test: (p: string) => /[A-Z]/.test(p) },
  { label: 'One lowercase letter', test: (p: string) => /[a-z]/.test(p) },
  { label: 'One digit', test: (p: string) => /[0-9]/.test(p) },
] as const;

// There is no symbol requirement and no regex on the backend — adding one client-side would
// reject valid passwords before they are ever sent (§4.2).
export const registerSchema = z
  .object({
    email,
    password: z
      .string()
      .min(PASSWORD_MIN, `Password must be between ${PASSWORD_MIN} and ${PASSWORD_MAX} characters.`)
      .max(PASSWORD_MAX, `Password must be between ${PASSWORD_MIN} and ${PASSWORD_MAX} characters.`)
      .refine((p) => /[A-Z]/.test(p), 'Password must contain at least one uppercase letter.')
      .refine((p) => /[a-z]/.test(p), 'Password must contain at least one lowercase letter.')
      .refine((p) => /[0-9]/.test(p), 'Password must contain at least one digit.')
      .refine(
        (p) => !COMMON_PASSWORDS.has(p.toLowerCase()),
        'Password is too common. Choose something less guessable.',
      ),
    confirmPassword: z.string().min(1, 'ConfirmPassword is required.'),
  })
  .refine((v) => v.password === v.confirmPassword, {
    path: ['confirmPassword'],
    message: 'Passwords do not match.',
  });

export type RegisterFormValues = z.infer<typeof registerSchema>;

/* ── §4.3 Product create / edit — identical rules on POST and PUT ──────────── */

const PRICE_RANGE_MESSAGE = `Price must be between 0 and ${PRICE_MAX}.`;

export const productSchema = z.object({
  name: z
    .string()
    .trim()
    .min(1, 'Name is required.')
    .max(NAME_MAX, `Name must not exceed ${NAME_MAX} characters.`),
  description: z
    .string()
    .trim()
    .max(DESCRIPTION_MAX, `Description must not exceed ${DESCRIPTION_MAX} characters.`),
  price: z
    .string()
    .trim()
    .min(1, 'Price is required.')
    .refine((v) => Number.isFinite(Number(v)), PRICE_RANGE_MESSAGE)
    .refine((v) => Number(v) >= 0 && Number(v) <= PRICE_MAX, PRICE_RANGE_MESSAGE),
});

export type ProductFormValues = z.infer<typeof productSchema>;

/**
 * An empty description is sent as `null`, not `""`. The server normalises whitespace-only to
 * null anyway, but sending null keeps the round-trip stable (§4.3).
 */
export function toWriteRequest(values: ProductFormValues): ProductWriteRequest {
  const description = values.description.trim();

  return {
    name: values.name.trim(),
    description: description === '' ? null : description,
    price: roundTo2dp(Number(values.price)),
  };
}

/* ── §4.4 Search filters ───────────────────────────────────────────────────── */

const optionalPrice = (label: 'minPrice' | 'maxPrice') =>
  z
    .string()
    .trim()
    .refine((v) => v === '' || Number.isFinite(Number(v)), `${label} must be a number.`)
    .refine(
      (v) => v === '' || (Number(v) >= 0 && Number(v) <= PRICE_MAX),
      `${label} must be between 0 and ${PRICE_MAX}.`,
    );

/**
 * The cross-field rule reports under the **`minPrice`** key on the server, not `maxPrice`.
 * Matching that here keeps the message under the same input whichever side produced it — and
 * validating client-side means the request is never sent (§4.4).
 */
export const searchFilterSchema = z
  .object({
    name: z.string().trim().max(NAME_MAX, `Name must not exceed ${NAME_MAX} characters.`),
    minPrice: optionalPrice('minPrice'),
    maxPrice: optionalPrice('maxPrice'),
  })
  .refine(
    (v) =>
      v.minPrice === '' || v.maxPrice === '' || Number(v.minPrice) <= Number(v.maxPrice),
    { path: ['minPrice'], message: 'minPrice must be less than or equal to maxPrice.' },
  );

export type SearchFilterValues = z.infer<typeof searchFilterSchema>;
