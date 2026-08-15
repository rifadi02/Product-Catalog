/**
 * The wire contract, transcribed from FRONTEND-PDR.md §1.4, §3.1, §6.2 and §8.
 *
 * Two id types that must never be conflated (§0.3): users are GUIDs (`string`), products are
 * 32-bit integers (`number`).
 */
import axios, { type AxiosError } from 'axios';

/* ── §1 The error contract ─────────────────────────────────────────────────── */

export interface ProblemDetails {
  type: string;
  title: string;
  status: number;
  detail: string;
  instance: string;
  code: ApiErrorCode;
  traceId: string;
  errors?: Record<string, string[]>;
}

export type ApiErrorCode =
  | 'validation_failed'
  | 'malformed_json'
  | 'invalid_credentials'
  | 'missing_token'
  | 'invalid_token'
  | 'token_expired'
  | 'refresh_token_invalid'
  | 'insufficient_role'
  | 'resource_not_found'
  | 'email_already_registered'
  | 'concurrency_conflict'
  | 'precondition_required'
  | 'rate_limited'
  | 'request_cancelled'
  | 'internal_error';

export function isProblem(e: unknown): e is AxiosError<ProblemDetails> {
  return axios.isAxiosError(e) && typeof e.response?.data?.code === 'string';
}

/* ── §2 Auth ───────────────────────────────────────────────────────────────── */

export type UserRole = 'User' | 'Admin';

export interface UserSummary {
  id: string; // GUID
  email: string;
  role: UserRole;
}

export interface AuthTokens {
  accessToken: string;
  refreshToken: string;
  tokenType: 'Bearer';
  expiresIn: number; // seconds; 900 under default configuration
  expiresAt: string; // ISO-8601 UTC
  user: UserSummary;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  email: string;
  password: string;
  confirmPassword: string;
}

/** Registration returns no tokens — the user is not logged in (§2.4). */
export interface RegisterResponse {
  id: string;
  email: string;
  role: UserRole;
  createdAt: string;
}

/* ── §3.1 The only product shape on the wire ───────────────────────────────── */

export interface Product {
  id: number; // int32, NOT a guid
  name: string; // 1–200 chars, server-trimmed
  description: string | null; // ≤2000 chars; whitespace-only becomes null
  price: number; // 0 – 999999.99, 2dp
  createdAt: string; // ISO-8601 UTC
  updatedAt: string | null; // null until the first successful PUT
}

/** A product plus the `ETag` captured from its detail GET — the edit flow needs both (§3.5). */
export interface ProductSnapshot {
  product: Product;
  /** Strong quoted token, e.g. `"8125"`. The quotes are part of the value. */
  etag: string | null;
}

export interface ProductWriteRequest {
  name: string;
  description: string | null;
  price: number;
}

/* ── §6 Paging ─────────────────────────────────────────────────────────────── */

export type ProductSortField = 'CreatedAt' | 'Name' | 'Price';
export type SortDirection = 'Asc' | 'Desc';

export interface ProductQuery {
  page: number;
  pageSize: number;
  sortBy: ProductSortField;
  direction: SortDirection;
  name?: string;
  minPrice?: number;
  maxPrice?: number;
}

export interface PagedResponse<T> {
  items: T[];
  page: number; // echoes the requested page
  pageSize: number; // echoes the requested size
  totalCount: number;
  totalPages: number; // ceil(totalCount / pageSize); 0 when empty
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}
