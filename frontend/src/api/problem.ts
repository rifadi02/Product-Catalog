/**
 * One parser for every error the API can produce — PDR §1.
 *
 * Every non-2xx response is an RFC 7807 problem document, so there is exactly one shape to
 * handle. Switch on `code`; never on `title` (a fixed English label) and never on `type` (the
 * same constant URI on every error regardless of status).
 */
import axios from 'axios';
import type { FieldValues, Path, UseFormSetError } from 'react-hook-form';
import { isProblem, type ApiErrorCode, type ProblemDetails } from './types';

export function getProblem(error: unknown): ProblemDetails | null {
  return isProblem(error) ? (error.response?.data ?? null) : null;
}

/**
 * Every 400 with an `errors` bag maps cleanly onto a React Hook Form schema: keys are the
 * camelCased leaf of the server's model-state key, which always matches the camelCase field
 * names in the request body just sent (§1.3).
 *
 * Returns false when there was no bag, so the caller can fall back to a form-level message —
 * `email_already_registered` and the stricter server-side email pattern both arrive without one.
 */
export function applyServerErrors<T extends FieldValues>(
  problem: ProblemDetails | null,
  setError: UseFormSetError<T>,
): boolean {
  if (!problem?.errors) return false;

  let applied = false;
  for (const [field, messages] of Object.entries(problem.errors)) {
    // A single field can carry multiple messages — the password rules chain, so one weak
    // password can return four strings under "password".
    setError(field as Path<T>, { type: 'server', message: messages.join(' ') });
    applied = true;
  }
  return applied;
}

export interface ErrorInfo {
  /** Safe to render. Never the raw `detail` of a 500 — in Development that is a stack trace. */
  message: string;
  code?: ApiErrorCode;
  status?: number;
  /** Equals the X-Correlation-Id response header. Show it so users can quote it (§1.1). */
  traceId?: string;
  /** True for our own aborted requests — render nothing at all. */
  silent: boolean;
  /** Seconds from the `Retry-After` header on a 429. */
  retryAfter?: number;
  /** No `response` at all: CORS rejection, DNS failure, or a dead API (§1.5). */
  offline: boolean;
}

const OFFLINE_MESSAGE = "Can't reach the server. Try again.";
const GENERIC_MESSAGE = 'Something went wrong.';

/** The §1.2 treatment table, as a single function. */
export function describeError(error: unknown, fallback = GENERIC_MESSAGE): ErrorInfo {
  if (axios.isCancel(error)) {
    return { message: '', silent: true, offline: false };
  }

  const problem = getProblem(error);

  if (!problem) {
    const noResponse = axios.isAxiosError(error) && !error.response;
    return {
      message: noResponse ? OFFLINE_MESSAGE : fallback,
      silent: false,
      offline: noResponse,
    };
  }

  const base = {
    code: problem.code,
    status: problem.status,
    traceId: problem.traceId,
    silent: false,
    offline: false,
  };

  switch (problem.code) {
    case 'request_cancelled':
      return { ...base, message: '', silent: true };

    // The one code whose `detail` must never reach a user: in Development it carries a full
    // .NET stack trace.
    case 'internal_error':
      return { ...base, message: GENERIC_MESSAGE };

    case 'invalid_credentials':
      return { ...base, message: 'Email or password is incorrect.' };

    case 'missing_token':
    case 'invalid_token':
    case 'token_expired':
    case 'refresh_token_invalid':
      return { ...base, message: 'Your session has ended. Please sign in again.' };

    case 'insufficient_role':
      return { ...base, message: "You don't have permission to do that." };

    case 'email_already_registered':
      return { ...base, message: 'An account with this email already exists.' };

    case 'concurrency_conflict':
    case 'precondition_required':
      return { ...base, message: 'This product changed while you were editing.' };

    case 'rate_limited':
      return { ...base, message: 'Too many attempts. Please wait before trying again.' };

    case 'malformed_json':
      return { ...base, message: 'The request could not be sent. Please try again.' };

    // 4xx `detail` is human-readable and safe to show.
    default:
      return { ...base, message: problem.detail || fallback };
  }
}

/** Seconds to wait after a 429. `Retry-After` is CORS-exposed, so this is readable (§0.4). */
export function retryAfterSeconds(error: unknown): number | undefined {
  if (!axios.isAxiosError(error)) return undefined;

  const raw = error.response?.headers?.['retry-after'];
  const seconds = Number(raw);

  return Number.isFinite(seconds) && seconds > 0 ? Math.ceil(seconds) : undefined;
}

export const hasCode = (error: unknown, code: ApiErrorCode) => getProblem(error)?.code === code;

/**
 * Some 400s are our bug, not the user's: the UI owns every paging and sorting value, so a
 * rejected `pageSize` is something the user cannot act on (§3.4.2). Log it and reset to
 * defaults rather than rendering a validation error at them.
 */
export function logClientBug(context: string, error: unknown) {
  const problem = getProblem(error);
  console.error(
    `[client bug] ${context}`,
    problem ? { code: problem.code, errors: problem.errors, traceId: problem.traceId } : error,
  );
}
