/** Access-token decoding — PDR §5.1. Decode, never verify. */
import { describe, expect, it } from 'vitest';
import { decodeAccessToken, secondsUntilExpiry } from './jwt';
import { makeAccessToken } from '../test/factories';

describe('decodeAccessToken', () => {
  it('reads the raw JWT short-name claims', () => {
    // The backend disables .NET's legacy claim remapping, so there are no SOAP-URI keys.
    const claims = decodeAccessToken(makeAccessToken({ role: 'Admin' }));

    expect(claims?.role).toBe('Admin');
    expect(claims?.iss).toBe('product-catalog-api');
    expect(claims?.aud).toBe('product-catalog-spa');
    // iat is a string while exp and nbf are numbers — that inconsistency is real.
    expect(typeof claims?.iat).toBe('string');
    expect(typeof claims?.exp).toBe('number');
    expect(typeof claims?.nbf).toBe('number');
  });

  it('decodes non-ASCII payloads as UTF-8', () => {
    const claims = decodeAccessToken(makeAccessToken({ email: 'josé.münoz@café.example' }));
    expect(claims?.email).toBe('josé.münoz@café.example');
  });

  it('returns null rather than throwing on a malformed token', () => {
    expect(decodeAccessToken('not-a-jwt')).toBeNull();
    expect(decodeAccessToken('')).toBeNull();
    expect(decodeAccessToken('a.!!!not-base64!!!.c')).toBeNull();
  });

  it('has no name claim to fall back on', () => {
    // Display names do not exist in this system — email is the only user label (§5.1).
    const claims = decodeAccessToken(makeAccessToken());
    expect(claims && 'name' in claims).toBe(false);
  });
});

describe('secondsUntilExpiry', () => {
  it('reports the remaining lifetime', () => {
    const remaining = secondsUntilExpiry(makeAccessToken({ expiresInSeconds: 900 }));
    expect(remaining).toBeGreaterThan(890);
    expect(remaining).toBeLessThanOrEqual(900);
  });

  it('goes negative once expired', () => {
    expect(secondsUntilExpiry(makeAccessToken({ expiresInSeconds: -30 }))).toBeLessThan(0);
  });

  it('returns null for tokens it cannot read', () => {
    expect(secondsUntilExpiry(null)).toBeNull();
    expect(secondsUntilExpiry('opaque-string')).toBeNull();
  });
});
