/**
 * Access-token claims — PDR §5.1.
 *
 * Decode, never verify. There is no key to verify with, and the decoded role is only used to
 * decide what to *show*: the server re-validates the signature on every request, so a tampered
 * token buys nothing but buttons that 403.
 *
 * Claim names are the raw JWT short names. The backend disables .NET's legacy claim remapping,
 * so there are no `nameidentifier` or SOAP-URI keys. `iat` is a string while `exp` and `nbf` are
 * numbers — that inconsistency is real, not a typo.
 */
import type { UserRole } from '../api/types';

export interface AccessTokenClaims {
  sub: string; // user id (GUID)
  email: string;
  role: UserRole;
  jti: string;
  iat: string; // epoch seconds, as a STRING
  nbf: number; // epoch seconds, as a number
  exp: number; // epoch seconds, as a number
  iss: string; // "product-catalog-api"
  aud: string; // "product-catalog-spa"
}

export function decodeAccessToken(token: string): AccessTokenClaims | null {
  try {
    const payload = token.split('.')[1];
    if (!payload) return null;

    const json = atob(payload.replace(/-/g, '+').replace(/_/g, '/'));

    // The seed data contains accented and CJK names, and emails are round-tripped through the
    // token, so the payload has to be read as UTF-8 rather than Latin-1.
    const utf8 = decodeURIComponent(
      json
        .split('')
        .map((c) => `%${c.charCodeAt(0).toString(16).padStart(2, '0')}`)
        .join(''),
    );

    return JSON.parse(utf8) as AccessTokenClaims;
  } catch {
    return null;
  }
}

/** Seconds until the token expires; negative once it has. `null` when it cannot be read. */
export function secondsUntilExpiry(token: string | null): number | null {
  if (!token) return null;

  const claims = decodeAccessToken(token);
  if (!claims || typeof claims.exp !== 'number') return null;

  return claims.exp - Math.floor(Date.now() / 1000);
}
