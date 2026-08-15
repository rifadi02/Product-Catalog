/** Fixtures shaped exactly like the wire format in the PDR, including its edge cases. */
import type { AuthTokens, PagedResponse, Product, ProblemDetails, UserRole } from '../api/types';

export function makeProduct(overrides: Partial<Product> = {}): Product {
  return {
    id: 42,
    name: 'Desk Lamp',
    description: 'Warm light, adjustable arm.',
    price: 24.99,
    createdAt: '2026-08-15T09:41:12.482Z',
    updatedAt: null,
    ...overrides,
  };
}

/** The deliberate edge cases the seeded catalogue contains (§0.5). */
export const EDGE_CASE_PRODUCTS: Product[] = [
  makeProduct({
    id: 63,
    name: 'Café Latte Máquina — Ünïcode Test',
    description: 'Accents, emoji ☕, and CJK 製品 in one name.',
    price: 349.5,
    createdAt: '2026-08-15T08:12:03.117Z',
  }),
  makeProduct({
    id: 62,
    name: 'Ultra Premium Flagship',
    description: 'X'.repeat(2000),
    price: 999999.99,
    createdAt: '2026-08-15T08:12:03.115Z',
  }),
  makeProduct({
    id: 61,
    name: 'Zero-Cost Sample Kit',
    description: null,
    price: 0,
    createdAt: '2026-08-15T08:12:03.113Z',
    updatedAt: '2026-08-15T11:02:44.019Z',
  }),
];

export function makePage(
  items: Product[],
  overrides: Partial<PagedResponse<Product>> = {},
): PagedResponse<Product> {
  const page = overrides.page ?? 1;
  const pageSize = overrides.pageSize ?? 20;
  const totalCount = overrides.totalCount ?? items.length;
  const totalPages = overrides.totalPages ?? Math.ceil(totalCount / pageSize);

  return {
    items,
    page,
    pageSize,
    totalCount,
    totalPages,
    hasNextPage: overrides.hasNextPage ?? page < totalPages,
    hasPreviousPage: overrides.hasPreviousPage ?? page > 1,
    ...overrides,
  };
}

/** A structurally valid HS256 JWT with a throwaway signature — decoded, never verified (§5.1). */
export function makeAccessToken({
  role = 'User',
  email = 'user@demo.local',
  sub = '0198c3f1-4a2b-7c3d-8e9f-0a1b2c3d4e5f',
  expiresInSeconds = 900,
}: {
  role?: UserRole;
  email?: string;
  sub?: string;
  expiresInSeconds?: number;
} = {}): string {
  const now = Math.floor(Date.now() / 1000);

  const encode = (value: object) =>
    btoa(unescape(encodeURIComponent(JSON.stringify(value))))
      .replace(/\+/g, '-')
      .replace(/\//g, '_')
      .replace(/=+$/, '');

  const header = encode({ alg: 'HS256', typ: 'JWT' });
  const payload = encode({
    sub,
    email,
    role,
    jti: '0198c3f1-4a2b-7c3d-8e9f-0a1b2c3d4e60',
    iat: String(now), // a string on the wire, unlike exp/nbf (§5.1)
    nbf: now,
    exp: now + expiresInSeconds,
    iss: 'product-catalog-api',
    aud: 'product-catalog-spa',
  });

  return `${header}.${payload}.c2lnbmF0dXJl`;
}

export function makeTokens(overrides: Partial<AuthTokens> = {}): AuthTokens {
  const role = overrides.user?.role ?? 'User';

  return {
    accessToken: makeAccessToken({ role }),
    refreshToken: 'kM3nP9qR7sT1vX4zA6cE8gJ0lN2pS5uW7yB9dF1hK3m',
    tokenType: 'Bearer',
    expiresIn: 900,
    expiresAt: '2026-08-15T09:56:12.482Z',
    user: {
      id: '0198c3f1-4a2b-7c3d-8e9f-0a1b2c3d4e5f',
      email: role === 'Admin' ? 'admin@demo.local' : 'user@demo.local',
      role,
    },
    ...overrides,
  };
}

const TITLES: Record<string, string> = {
  validation_failed: 'Validation failed',
  invalid_credentials: 'Invalid credentials',
  missing_token: 'Authentication required',
  token_expired: 'Token expired',
  refresh_token_invalid: 'Invalid refresh token',
  insufficient_role: 'Forbidden',
  resource_not_found: 'Resource not found',
  email_already_registered: 'Email already registered',
  concurrency_conflict: 'Concurrency conflict',
  rate_limited: 'Too many requests',
  internal_error: 'Internal server error',
};

/** Every non-2xx response in this API is this shape — there are no others (§1.1). */
export function makeProblem(
  code: ProblemDetails['code'],
  status: number,
  extra: Partial<ProblemDetails> = {},
): ProblemDetails {
  return {
    type: 'https://datatracker.ietf.org/doc/html/rfc9110#section-15',
    title: TITLES[code] ?? 'Request failed',
    status,
    detail: 'Something the server wanted to say.',
    instance: '/api/v1/products',
    code,
    traceId: '0HN7GK3P9QVJ1:00000003',
    ...extra,
  };
}
